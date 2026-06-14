using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Persistence;

namespace OccultShop.Systems;

public sealed class PotionKnowledgeState
{
	private readonly HashSet<string> _knownPotions;
	private readonly List<string> _knownPotionOrder;
	private readonly HashSet<string> _knownIngredients;
	private readonly List<string> _knownIngredientOrder;
	private readonly HashSet<string> _knownIngredientPreparations;
	private readonly Dictionary<string, string> _potionDisplayNames;
	private readonly Func<string, string?> _resolveKnownIngredientId;
	private readonly Dictionary<string, int> _potionBasePrices = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> _potionRecipes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _combinationPotionItems = new(StringComparer.OrdinalIgnoreCase);

	public PotionKnowledgeState(
		HashSet<string> knownPotions,
		List<string> knownPotionOrder,
		HashSet<string> knownIngredients,
		List<string> knownIngredientOrder,
		HashSet<string> knownIngredientPreparations,
		Dictionary<string, string> potionDisplayNames,
		Func<string, string?> resolveKnownIngredientId)
	{
		_knownPotions = knownPotions;
		_knownPotionOrder = knownPotionOrder;
		_knownIngredients = knownIngredients;
		_knownIngredientOrder = knownIngredientOrder;
		_knownIngredientPreparations = knownIngredientPreparations;
		_potionDisplayNames = potionDisplayNames;
		_resolveKnownIngredientId = resolveKnownIngredientId;
	}

	public void Clear()
	{
		_knownPotions.Clear();
		_knownPotionOrder.Clear();
		_knownIngredients.Clear();
		_knownIngredientOrder.Clear();
		_knownIngredientPreparations.Clear();
		_potionDisplayNames.Clear();
		_potionBasePrices.Clear();
		_potionRecipes.Clear();
		_combinationPotionItems.Clear();
	}

	public void Restore(GameStateSnapshot snapshot)
	{
		Clear();
		RestoreKnownPotions(snapshot.KnownPotions, snapshot.KnownPotionOrder);
		RestoreKnownIngredients(snapshot.KnownIngredients, snapshot.KnownIngredientOrder);
		RestoreKnownIngredientPreparations(snapshot.KnownIngredientPreparations);
		RestorePotionDisplayNames(snapshot.PotionDisplayNames);
		RestorePotionBasePrices(snapshot.PotionBasePrices);
		RestorePotionRecipes(snapshot.PotionRecipes);
		RestoreCombinationPotionItems(snapshot.CombinationPotionItems);
	}

	public List<string> BuildKnownPotionSnapshot()
	{
		return _knownPotionOrder.Count > 0 ? new List<string>(_knownPotionOrder) : _knownPotions.ToList();
	}

	public List<string> CloneKnownPotionOrder()
	{
		return new List<string>(_knownPotionOrder);
	}

	public List<string> BuildKnownIngredientSnapshot()
	{
		return _knownIngredientOrder.Count > 0 ? new List<string>(_knownIngredientOrder) : _knownIngredients.ToList();
	}

	public List<string> CloneKnownIngredientOrder()
	{
		return new List<string>(_knownIngredientOrder);
	}

	public List<string> BuildKnownIngredientPreparationSnapshot()
	{
		var preparations = new List<string>(_knownIngredientPreparations);
		preparations.Sort(StringComparer.OrdinalIgnoreCase);
		return preparations;
	}

	public Dictionary<string, string> ClonePotionDisplayNames()
	{
		return new Dictionary<string, string>(_potionDisplayNames, StringComparer.OrdinalIgnoreCase);
	}

	public Dictionary<string, int> ClonePotionBasePrices()
	{
		return new Dictionary<string, int>(_potionBasePrices, StringComparer.OrdinalIgnoreCase);
	}

	public Dictionary<string, List<string>> ClonePotionRecipes()
	{
		var copy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionRecipes)
			copy[pair.Key] = new List<string>(pair.Value);

		return copy;
	}

	public Dictionary<string, string> CloneCombinationPotionItems()
	{
		return new Dictionary<string, string>(_combinationPotionItems, StringComparer.OrdinalIgnoreCase);
	}

	public bool LearnPotion(string potionId)
	{
		if (string.IsNullOrWhiteSpace(potionId))
			return false;

		var knownPotionAdded = _knownPotions.Add(potionId);
		var orderAdded = false;
		if (!_knownPotionOrder.Contains(potionId))
		{
			_knownPotionOrder.Add(potionId);
			orderAdded = true;
		}

		return knownPotionAdded || orderAdded;
	}

	public bool KnowsPotion(string potionId)
	{
		return !string.IsNullOrWhiteSpace(potionId) && _knownPotions.Contains(potionId);
	}

	public bool ForgetPotion(string potionId)
	{
		if (string.IsNullOrWhiteSpace(potionId))
			return false;

		var removedKnown = _knownPotions.RemoveWhere(id => string.Equals(id, potionId, StringComparison.OrdinalIgnoreCase)) > 0;
		var removedOrder = _knownPotionOrder.RemoveAll(id => string.Equals(id, potionId, StringComparison.OrdinalIgnoreCase)) > 0;
		return removedKnown || removedOrder;
	}

	public bool AddKnownIngredient(string ingredientId)
	{
		var knownIngredientId = _resolveKnownIngredientId(ingredientId);
		if (string.IsNullOrWhiteSpace(knownIngredientId))
			return false;

		var knownIngredientAdded = _knownIngredients.Add(knownIngredientId);
		var orderAdded = false;
		if (!_knownIngredientOrder.Any(id => string.Equals(id, knownIngredientId, StringComparison.OrdinalIgnoreCase)))
		{
			_knownIngredientOrder.Add(knownIngredientId);
			orderAdded = true;
		}

		return knownIngredientAdded || orderAdded;
	}

	public bool KnowsIngredient(string ingredientId)
	{
		return !string.IsNullOrWhiteSpace(ingredientId) && _knownIngredients.Contains(ingredientId);
	}

	public bool AddKnownIngredientPreparation(string ingredientId, string preparationId)
	{
		if (!TryBuildIngredientPreparationKnowledgeKey(ingredientId, preparationId, out var key, out var knownIngredientId))
			return false;

		AddKnownIngredient(knownIngredientId);
		return _knownIngredientPreparations.Add(key);
	}

	public bool KnowsIngredientPreparation(string ingredientId, string preparationId)
	{
		return TryBuildIngredientPreparationKnowledgeKey(ingredientId, preparationId, out var key, out _) &&
			_knownIngredientPreparations.Contains(key);
	}

	public bool KnowsAnyIngredientPreparation(string ingredientId)
	{
		var knownIngredientId = _resolveKnownIngredientId(ingredientId);
		if (string.IsNullOrWhiteSpace(knownIngredientId))
			knownIngredientId = ingredientId?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(knownIngredientId))
			return false;

		var prefix = BuildIngredientPreparationKnowledgeKeyPrefix(knownIngredientId);
		foreach (var key in _knownIngredientPreparations)
		{
			if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	public bool ForgetIngredient(string ingredientId)
	{
		if (string.IsNullOrWhiteSpace(ingredientId))
			return false;

		var knownIngredientId = _resolveKnownIngredientId(ingredientId) ?? ingredientId.Trim();
		var removedKnown = _knownIngredients.RemoveWhere(id => string.Equals(id, knownIngredientId, StringComparison.OrdinalIgnoreCase)) > 0;
		var removedOrder = _knownIngredientOrder.RemoveAll(id => string.Equals(id, knownIngredientId, StringComparison.OrdinalIgnoreCase)) > 0;
		var removedPreparations = _knownIngredientPreparations.RemoveWhere(key =>
			key.StartsWith(BuildIngredientPreparationKnowledgeKeyPrefix(knownIngredientId), StringComparison.OrdinalIgnoreCase)) > 0;
		return removedKnown || removedOrder || removedPreparations;
	}

	public bool RecordPotionRecipe(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return false;

		var changed = false;
		if (!_potionRecipes.ContainsKey(potionItemId))
		{
			_potionRecipes[potionItemId] = new List<string>(ingredientIds);
			changed = true;
		}

		foreach (var ingredientId in ingredientIds)
			changed |= AddKnownIngredient(ingredientId);

		return changed;
	}

	public bool TryGetPotionRecipe(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionRecipes.TryGetValue(potionItemId, out var stored))
			return false;

		ingredientIds = new List<string>(stored);
		return true;
	}

	public bool SetPotionDisplayName(string potionId, string displayName)
	{
		if (string.IsNullOrWhiteSpace(potionId) || string.IsNullOrWhiteSpace(displayName))
			return false;

		_potionDisplayNames[potionId] = displayName;
		return true;
	}

	public bool RegisterPotionBasePrice(string potionId, int basePrice)
	{
		if (string.IsNullOrWhiteSpace(potionId) || basePrice < 0)
			return false;

		if (_potionBasePrices.ContainsKey(potionId))
			return false;

		_potionBasePrices[potionId] = basePrice;
		return true;
	}

	public bool TryGetPotionBasePrice(string potionId, out int basePrice)
	{
		return _potionBasePrices.TryGetValue(potionId, out basePrice);
	}

	public string? GetPotionDisplayName(string potionId)
	{
		return _potionDisplayNames.TryGetValue(potionId, out var displayName) ? displayName : null;
	}

	public bool TryGetPotionForCombination(string combinationKey, out string potionItemId)
	{
		return _combinationPotionItems.TryGetValue(combinationKey, out potionItemId!);
	}

	public void SetPotionForCombination(string combinationKey, string potionItemId)
	{
		if (string.IsNullOrWhiteSpace(combinationKey) || string.IsNullOrWhiteSpace(potionItemId))
			return;

		_combinationPotionItems[combinationKey] = potionItemId;
	}

	public void BackfillKnownIngredientsFromKnownRecipes()
	{
		foreach (var potionId in _knownPotions)
		{
			if (!_potionRecipes.TryGetValue(potionId, out var ingredientIds))
				continue;

			foreach (var ingredientId in ingredientIds)
				AddKnownIngredient(ingredientId);
		}
	}

	private void RestoreKnownPotions(IEnumerable<string>? knownPotions, IReadOnlyCollection<string>? knownPotionOrder)
	{
		_knownPotions.Clear();
		if (knownPotions is not null)
		{
			foreach (var potionId in knownPotions)
			{
				if (!string.IsNullOrWhiteSpace(potionId))
					_knownPotions.Add(potionId);
			}
		}

		_knownPotionOrder.Clear();
		var potionOrderSource = knownPotionOrder is { Count: > 0 }
			? knownPotionOrder
			: knownPotions;
		if (potionOrderSource is not null)
		{
			foreach (var potionId in potionOrderSource)
			{
				if (string.IsNullOrWhiteSpace(potionId))
					continue;
				if (!_knownPotions.Contains(potionId))
					continue;
				if (_knownPotionOrder.Contains(potionId))
					continue;

				_knownPotionOrder.Add(potionId);
			}
		}

		foreach (var potionId in _knownPotions)
		{
			if (_knownPotionOrder.Contains(potionId))
				continue;

			_knownPotionOrder.Add(potionId);
		}
	}

	private void RestoreKnownIngredients(IEnumerable<string>? knownIngredients, IReadOnlyCollection<string>? knownIngredientOrder)
	{
		_knownIngredients.Clear();
		if (knownIngredients is not null)
		{
			foreach (var ingredientId in knownIngredients)
				AddKnownIngredient(ingredientId);
		}

		_knownIngredientOrder.Clear();
		var ingredientOrderSource = knownIngredientOrder is { Count: > 0 }
			? knownIngredientOrder
			: knownIngredients;
		if (ingredientOrderSource is not null)
		{
			foreach (var ingredientId in ingredientOrderSource)
				AddKnownIngredient(ingredientId);
		}
	}

	private void RestoreKnownIngredientPreparations(IEnumerable<string>? knownIngredientPreparations)
	{
		_knownIngredientPreparations.Clear();
		if (knownIngredientPreparations is null)
			return;

		foreach (var key in knownIngredientPreparations)
		{
			if (string.IsNullOrWhiteSpace(key))
				continue;

			_knownIngredientPreparations.Add(key.Trim());
		}
	}

	private void RestorePotionDisplayNames(Dictionary<string, string>? potionDisplayNames)
	{
		_potionDisplayNames.Clear();
		if (potionDisplayNames is null)
			return;

		foreach (var pair in potionDisplayNames)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
				continue;

			_potionDisplayNames[pair.Key] = pair.Value;
		}
	}

	private void RestorePotionBasePrices(Dictionary<string, int>? potionBasePrices)
	{
		_potionBasePrices.Clear();
		if (potionBasePrices is null)
			return;

		foreach (var pair in potionBasePrices)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
				continue;

			_potionBasePrices[pair.Key] = pair.Value;
		}
	}

	private void RestorePotionRecipes(Dictionary<string, List<string>>? potionRecipes)
	{
		_potionRecipes.Clear();
		if (potionRecipes is null)
			return;

		foreach (var pair in potionRecipes)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
				continue;

			_potionRecipes[pair.Key] = new List<string>(pair.Value);
		}
	}

	private void RestoreCombinationPotionItems(Dictionary<string, string>? combinationPotionItems)
	{
		_combinationPotionItems.Clear();
		if (combinationPotionItems is null)
			return;

		foreach (var pair in combinationPotionItems)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
				continue;

			_combinationPotionItems[pair.Key] = pair.Value;
		}
	}

	private bool TryBuildIngredientPreparationKnowledgeKey(
		string ingredientId,
		string preparationId,
		out string key,
		out string knownIngredientId)
	{
		key = string.Empty;
		knownIngredientId = string.Empty;

		if (string.IsNullOrWhiteSpace(ingredientId) || string.IsNullOrWhiteSpace(preparationId))
			return false;

		knownIngredientId = _resolveKnownIngredientId(ingredientId) ?? ingredientId.Trim();
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (string.IsNullOrWhiteSpace(knownIngredientId) || string.IsNullOrWhiteSpace(normalizedPreparationId))
			return false;

		key = $"{knownIngredientId.Trim().ToLowerInvariant()}::{normalizedPreparationId}";
		return true;
	}

	private static string BuildIngredientPreparationKnowledgeKeyPrefix(string ingredientId)
	{
		return $"{ingredientId.Trim().ToLowerInvariant()}::";
	}
}
