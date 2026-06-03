using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class TreatmentService
{
	private readonly GameState _gameState;
	private readonly ItemCatalogService _itemCatalog;
	private readonly RuntimeContentDb _runtimeContentDb;

	public TreatmentService(GameState gameState, ItemCatalogService itemCatalog, RuntimeContentDb runtimeContentDb)
	{
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_runtimeContentDb = runtimeContentDb;
	}

	public bool CanApplyTreatment(string consumableItemId, string targetItemId, out string error)
	{
		return TryBuildTreatmentCandidate(consumableItemId, targetItemId, true, false, out _, out error);
	}

	public bool CanApplyReservedTreatment(string consumableItemId, string targetItemId, out string error)
	{
		return TryBuildTreatmentCandidate(consumableItemId, targetItemId, false, true, out _, out error);
	}

	public bool TryApplyTreatment(string consumableItemId, string targetItemId, out string treatedItemId, out string error)
	{
		treatedItemId = string.Empty;
		if (!TryBuildTreatmentCandidate(consumableItemId, targetItemId, true, false, out var candidate, out error))
			return false;

		if (!_gameState.ConsumeItem(consumableItemId, 1))
		{
			error = "Consumable is no longer available.";
			return false;
		}

		if (!_gameState.ConsumeItem(targetItemId, 1))
		{
			_gameState.AddItem(consumableItemId, 1);
			error = "Target item is no longer available.";
			return false;
		}

		AddTreatmentResult(candidate);
		treatedItemId = candidate.OutputItemId;
		error = string.Empty;
		return true;
	}

	public bool TryApplyReservedTreatment(string consumableItemId, string targetItemId, out string treatedItemId, out string error)
	{
		treatedItemId = string.Empty;
		if (!TryBuildTreatmentCandidate(consumableItemId, targetItemId, false, true, out var candidate, out error))
			return false;

		AddTreatmentResult(candidate);
		treatedItemId = candidate.OutputItemId;
		error = string.Empty;
		return true;
	}

	private bool TryBuildTreatmentCandidate(
		string consumableItemId,
		string targetItemId,
		bool requireInventory,
		bool targetAlreadyReserved,
		out TreatmentCandidate candidate,
		out string error)
	{
		candidate = default!;
		error = string.Empty;

		if (string.IsNullOrWhiteSpace(consumableItemId))
		{
			error = "Choose a consumable.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(targetItemId))
		{
			error = "Choose an item to treat.";
			return false;
		}

		if (!_itemCatalog.TryGetItem(consumableItemId, out var consumable))
		{
			error = "Consumable is not recognized.";
			return false;
		}

		if (!_itemCatalog.IsConsumable(consumableItemId))
		{
			error = "First slot only accepts consumables.";
			return false;
		}

		if (requireInventory && !_gameState.HasItem(consumableItemId, 1))
		{
			error = "Consumable is not in inventory.";
			return false;
		}

		if (!_itemCatalog.TryGetItem(targetItemId, out var target))
		{
			error = "Target item is not recognized.";
			return false;
		}

		if (requireInventory && !_gameState.HasItem(targetItemId, 1))
		{
			error = "Target item is not in inventory.";
			return false;
		}

		if (_itemCatalog.IsConsumable(targetItemId))
		{
			error = "Consumables cannot be treated.";
			return false;
		}

		if (!_itemCatalog.IsIngredient(targetItemId) && !_itemCatalog.IsPotion(targetItemId))
		{
			error = "Only ingredients and potions can be treated.";
			return false;
		}

		if (target.Treatment is not null)
		{
			error = "That item has already been treated.";
			return false;
		}

		if (!ConsumableAllowsTarget(consumable, target))
		{
			error = "That consumable cannot be used on this item.";
			return false;
		}

		if (consumable.ConsumableEffect is null)
		{
			error = "Consumable effect is missing.";
			return false;
		}

		if (!string.Equals(consumable.ConsumableEffect.Kind, ConsumableEffectDef.RemoveRiskKind, StringComparison.OrdinalIgnoreCase))
		{
			error = "Consumable effect is not supported yet.";
			return false;
		}

		if (!TrySelectRiskToRemove(target, consumable.ConsumableEffect.RiskId, out var removedRisk))
		{
			error = string.IsNullOrWhiteSpace(consumable.ConsumableEffect.RiskId)
				? "Selected item has no risks to remove."
				: "Selected item does not have that risk.";
			return false;
		}

		var outputCandidate = BuildTreatmentCandidate(consumableItemId, consumable, targetItemId, target, removedRisk);
		if (_itemCatalog.IsPotion(targetItemId) && !CanFitTreatedPotion(targetItemId, outputCandidate.OutputItemId, targetAlreadyReserved, out error))
			return false;

		candidate = outputCandidate;
		return true;
	}

	private void AddTreatmentResult(TreatmentCandidate candidate)
	{
		if (candidate.RuntimeItem is not null)
		{
			_runtimeContentDb.UpsertRuntimeItem(candidate.RuntimeItem);
			if (_itemCatalog.IsPotion(candidate.RuntimeItem.Id))
			{
				_gameState.SetPotionDisplayName(candidate.RuntimeItem.Id, candidate.RuntimeItem.Name);
				_gameState.RegisterPotionBasePrice(candidate.RuntimeItem.Id, candidate.RuntimeItem.BasePrice);
			}
		}

		_gameState.AddItem(candidate.OutputItemId, 1);
	}

	private bool ConsumableAllowsTarget(ItemDef consumable, ItemDef target)
	{
		var allowedTargetTags = consumable.ConsumableGate?.AllowedTargetTags;
		if (allowedTargetTags is null || allowedTargetTags.Count == 0)
			return true;

		foreach (var allowedTag in allowedTargetTags)
		{
			if (string.IsNullOrWhiteSpace(allowedTag))
				continue;
			if (ItemCatalogService.HasTag(target, allowedTag))
				return true;
		}

		return false;
	}

	private static bool TrySelectRiskToRemove(ItemDef target, string requestedRiskId, out string removedRisk)
	{
		removedRisk = string.Empty;
		if (target.Risks is null || target.Risks.Count == 0)
			return false;

		if (!string.IsNullOrWhiteSpace(requestedRiskId))
		{
			foreach (var risk in target.Risks)
			{
				if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
					continue;
				if (!string.Equals(risk.Key, requestedRiskId, StringComparison.OrdinalIgnoreCase))
					continue;

				removedRisk = risk.Key;
				return true;
			}

			return false;
		}

		var selected = target.Risks
			.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault();

		if (string.IsNullOrWhiteSpace(selected.Key) || selected.Value <= 0)
			return false;

		removedRisk = selected.Key;
		return true;
	}

	private TreatmentCandidate BuildTreatmentCandidate(
		string consumableItemId,
		ItemDef consumable,
		string targetItemId,
		ItemDef target,
		string removedRisk)
	{
		var risks = target.Risks is null
			? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, int>(target.Risks, StringComparer.OrdinalIgnoreCase);
		risks.Remove(removedRisk);

		if (_itemCatalog.IsPotion(targetItemId) && TryBuildPotionTreatmentCandidate(targetItemId, target, risks, removedRisk, out var potionCandidate))
			return potionCandidate;

		var tags = target.Tags?.ToList() ?? new List<string>();
		if (!tags.Any(tag => string.Equals(tag, ItemTags.Treated, StringComparison.OrdinalIgnoreCase)))
			tags.Add(ItemTags.Treated);

		var displayTargetName = GetDisplayName(targetItemId, target.Name);
		var displayConsumableName = GetDisplayName(consumableItemId, consumable.Name);
		var basePrice = ResolveItemPrice(targetItemId, target);

		var treatedItem = new ItemDef
		{
			Id = BuildTreatedItemId(targetItemId, consumableItemId, removedRisk),
			Name = $"Treated {displayTargetName}",
			IconPath = target.IconPath,
			Description = BuildTreatmentDescription(target.Description, displayConsumableName, removedRisk),
			Tags = tags,
			Quality = target.Quality,
			Traits = target.Traits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(target.Traits),
			Risks = risks,
			BasePrice = basePrice,
			Treatment = new ItemTreatmentDef
			{
				BaseItemId = targetItemId,
				ConsumableItemId = consumableItemId,
				RemovedRisk = removedRisk
			}
		};

		return new TreatmentCandidate(treatedItem.Id, treatedItem, removedRisk);
	}

	private bool TryBuildPotionTreatmentCandidate(
		string targetItemId,
		ItemDef target,
		IReadOnlyDictionary<string, int> remainingRisks,
		string removedRisk,
		out TreatmentCandidate candidate)
	{
		candidate = default!;
		var basePotionItemId = GetBasePotionItemId(targetItemId);
		var outputPotionItemId = PotionVariantIdBuilder.BuildRiskVariantItemId(basePotionItemId, remainingRisks);
		if (string.Equals(
			outputPotionItemId,
			$"{basePotionItemId}{PotionVariantIdBuilder.RiskVariantSeparator}{PotionVariantIdBuilder.CleanRiskSignature}",
			StringComparison.OrdinalIgnoreCase))
		{
			outputPotionItemId = basePotionItemId;
		}

		if (string.Equals(outputPotionItemId, targetItemId, StringComparison.OrdinalIgnoreCase))
			return false;

		if (_itemCatalog.TryGetItem(outputPotionItemId, out _))
		{
			candidate = new TreatmentCandidate(outputPotionItemId, null, removedRisk);
			return true;
		}

		var runtimePotion = new ItemDef
		{
			Id = outputPotionItemId,
			Name = GetDisplayName(basePotionItemId, target.Name),
			IconPath = target.IconPath,
			Description = target.Description,
			Tags = target.Tags?.Where(tag => !string.Equals(tag, ItemTags.Treated, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>(),
			Quality = target.Quality,
			Traits = target.Traits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(target.Traits),
			Risks = new Dictionary<string, int>(remainingRisks, StringComparer.OrdinalIgnoreCase),
			BasePrice = ResolveItemPrice(basePotionItemId, target)
		};

		candidate = new TreatmentCandidate(outputPotionItemId, runtimePotion, removedRisk);
		return true;
	}

	private bool CanFitTreatedPotion(string targetItemId, string treatedItemId, bool targetAlreadyReserved, out string error)
	{
		error = string.Empty;
		var targetQuantity = _gameState.Inventory.GetValueOrDefault(targetItemId);
		var treatedQuantity = _gameState.Inventory.GetValueOrDefault(treatedItemId);
		if (treatedQuantity + 1 > GameState.MaxPotionStackQuantity)
		{
			error = $"Treated potion stack is capped at {GameState.MaxPotionStackQuantity}.";
			return false;
		}

		var uniquePotionCountAfterTakingTarget = _gameState.CountOwnedUniquePotions();
		if (!targetAlreadyReserved && targetQuantity == 1)
			uniquePotionCountAfterTakingTarget -= 1;

		if (treatedQuantity <= 0 && uniquePotionCountAfterTakingTarget >= GameState.MaxUniquePotionInventoryQuantity)
		{
			error = "Potion inventory is full. Sell a potion before treating another unique potion.";
			return false;
		}

		return true;
	}

	private int ResolveItemPrice(string itemId, ItemDef item)
	{
		if (_gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice))
			return potionBasePrice;

		return Math.Max(0, item.BasePrice);
	}

	private string GetDisplayName(string itemId, string fallbackName)
	{
		if (_itemCatalog.IsPotion(itemId))
		{
			var customName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(customName))
				return customName;
		}

		return string.IsNullOrWhiteSpace(fallbackName) ? itemId : fallbackName;
	}

	private static string BuildTreatmentDescription(string baseDescription, string consumableName, string removedRisk)
	{
		var treatmentLine = $"Treatment: {consumableName} removed {DisplayStatName(removedRisk)}.";
		if (string.IsNullOrWhiteSpace(baseDescription))
			return treatmentLine;

		return $"{baseDescription}\n{treatmentLine}";
	}

	private static string BuildTreatedItemId(string targetItemId, string consumableItemId, string removedRisk)
	{
		return $"{targetItemId}__treated_{NormalizeVariantIdPart(consumableItemId)}_{NormalizeVariantIdPart(removedRisk)}";
	}

	private static string GetBasePotionItemId(string potionItemId)
	{
		var riskSeparatorIndex = potionItemId.IndexOf(PotionVariantIdBuilder.RiskVariantSeparator, StringComparison.OrdinalIgnoreCase);
		return riskSeparatorIndex < 0 ? potionItemId : potionItemId[..riskSeparatorIndex];
	}

	private static string NormalizeVariantIdPart(string value)
	{
		var trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
		var chars = new char[trimmed.Length];
		var count = 0;
		var previousWasSeparator = false;

		foreach (var character in trimmed)
		{
			if (char.IsLetterOrDigit(character))
			{
				chars[count] = character;
				count += 1;
				previousWasSeparator = false;
				continue;
			}

			if (previousWasSeparator || count == 0)
				continue;

			chars[count] = '_';
			count += 1;
			previousWasSeparator = true;
		}

		if (count > 0 && chars[count - 1] == '_')
			count -= 1;

		return count == 0 ? "unknown" : new string(chars, 0, count);
	}

	private static string DisplayStatName(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return string.Empty;

		var normalized = key.Replace('_', ' ').Trim();
		if (normalized.Length == 0)
			return string.Empty;

		return char.ToUpperInvariant(normalized[0]) + normalized[1..];
	}

	private readonly record struct TreatmentCandidate(string OutputItemId, ItemDef? RuntimeItem, string RemovedRisk);
}
