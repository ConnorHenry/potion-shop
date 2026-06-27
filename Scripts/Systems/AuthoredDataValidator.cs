using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class AuthoredDataValidator
{
	private const int PotionRecipeIngredientCount = 3;
	private const int LowPreparationTraitMin = 2;
	private const int LowPreparationTraitMax = 4;
	private const int HighPreparationTraitMin = 5;
	private const int HighPreparationTraitMax = 6;
	private const int MaxRiskChanceValue = 10;
	private static readonly Regex StoryStateIdPattern = new("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

	private static readonly HashSet<string> KnownIngredientEffectKinds = new(StringComparer.OrdinalIgnoreCase)
	{
		IngredientEffectDef.BoostLowestOtherTraitKind,
		IngredientEffectDef.BoostLowestTraitIfNoRiskCarriesKind,
		IngredientEffectDef.BoostStrongestTraitAddRiskKind,
		IngredientEffectDef.CopyStrongestOtherTraitKind,
		IngredientEffectDef.HalveOtherRisksKind,
		IngredientEffectDef.ReduceHighestRiskKind,
		IngredientEffectDef.SuppressSingleCarriedRiskKind,
		IngredientEffectDef.TemperTraitsKind,
		IngredientEffectDef.AddTraitIfRiskCarriesKind
	};

	public static void Validate(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IReadOnlyList<CalendarEventDef> calendarEvents,
		IReadOnlyList<CustomerInteractionDef> customerInteractions,
		IReadOnlyList<PotionRecipeDef> potionRecipes)
	{
		ValidateItemDefinitions(items);
		ValidatePotionRecipes(items, potionRecipes);
		ValidateCalendarEvents(items, calendarEvents);
		ValidateCustomerInteractions(items, rules, customerInteractions);
	}

	private static void ValidateItemDefinitions(IReadOnlyDictionary<string, ItemDef> items)
	{
		foreach (var item in items.Values)
		{
			if (string.IsNullOrWhiteSpace(item.Name))
				PushDataWarning($"Item '{item.Id}' has no display name.");

			if (item.ConsumableEffect is not null)
				ValidateConsumableEffect(item.Id, item.ConsumableEffect);

			if (HasTag(item, ItemTags.Ingredient) && item.Treatment is null && item.PreparedIngredient is null)
				ValidateIngredientPreparations(item);

			ValidateIngredientEffects(item.Id, item.IngredientEffects);

			if (item.Treatment is not null)
				ValidateTreatmentMetadata(items, item.Id, item.Treatment);
		}
	}

	private static void ValidateIngredientPreparations(ItemDef item)
	{
		if (item.Preparations is null || item.Preparations.Count == 0)
		{
			PushDataWarning($"Ingredient '{item.Id}' has no preparation definitions.");
			return;
		}

		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (!item.Preparations.ContainsKey(option.Id))
				PushDataWarning($"Ingredient '{item.Id}' is missing the '{option.Id}' preparation.");
		}

		foreach (var preparation in item.Preparations)
		{
			if (string.IsNullOrWhiteSpace(preparation.Key))
			{
				PushDataWarning($"Ingredient '{item.Id}' has an empty preparation id.");
				continue;
			}

			if (!IngredientPreparationCatalog.IsKnownPreparationId(preparation.Key))
				PushDataWarning($"Ingredient '{item.Id}' has unknown preparation '{preparation.Key}'.");

			if (preparation.Value is null)
			{
				PushDataWarning($"Ingredient '{item.Id}' preparation '{preparation.Key}' has no data.");
				continue;
			}

			if ((preparation.Value.Traits is null || preparation.Value.Traits.Count == 0) &&
				(preparation.Value.Risks is null || preparation.Value.Risks.Count == 0))
			{
				PushDataWarning($"Ingredient '{item.Id}' preparation '{preparation.Key}' has no traits or risks.");
			}
		}

		ValidateTwoTraitPreparationContract(item);
	}

	private static void ValidateTwoTraitPreparationContract(ItemDef item)
	{
		if (item.Preparations is null || item.Preparations.Count == 0)
			return;

		if (!TryGetAuthoredPreparation(item, IngredientPreparationCatalog.RawPreparationId, out var raw) ||
			!TryGetAuthoredPreparation(item, IngredientPreparationCatalog.SteepedPreparationId, out var steeped) ||
			!TryGetAuthoredPreparation(item, IngredientPreparationCatalog.CrushedPreparationId, out var crushed) ||
			!TryGetAuthoredPreparation(item, IngredientPreparationCatalog.BoiledPreparationId, out var boiled))
		{
			return;
		}

		var hasRawTrait = TryGetSinglePositiveTrait(item.Id, IngredientPreparationCatalog.RawPreparationId, raw, out var rawTrait);
		var hasSteepedTrait = TryGetSinglePositiveTrait(item.Id, IngredientPreparationCatalog.SteepedPreparationId, steeped, out var steepedTrait);
		var hasCrushedTrait = TryGetSinglePositiveTrait(item.Id, IngredientPreparationCatalog.CrushedPreparationId, crushed, out var crushedTrait);
		var hasBoiledTrait = TryGetSinglePositiveTrait(item.Id, IngredientPreparationCatalog.BoiledPreparationId, boiled, out var boiledTrait);
		if (!hasRawTrait || !hasSteepedTrait || !hasCrushedTrait || !hasBoiledTrait)
			return;

		ValidateTraitValueRange(item.Id, IngredientPreparationCatalog.RawPreparationId, rawTrait, LowPreparationTraitMin, LowPreparationTraitMax);
		ValidateTraitValueRange(item.Id, IngredientPreparationCatalog.SteepedPreparationId, steepedTrait, HighPreparationTraitMin, HighPreparationTraitMax);
		ValidateTraitValueRange(item.Id, IngredientPreparationCatalog.CrushedPreparationId, crushedTrait, LowPreparationTraitMin, LowPreparationTraitMax);
		ValidateTraitValueRange(item.Id, IngredientPreparationCatalog.BoiledPreparationId, boiledTrait, HighPreparationTraitMin, HighPreparationTraitMax);

		if (!string.Equals(rawTrait.Key, steepedTrait.Key, StringComparison.OrdinalIgnoreCase))
			PushDataWarning($"Ingredient '{item.Id}' raw and steeped preparations should use the same first trait.");

		if (!string.Equals(crushedTrait.Key, boiledTrait.Key, StringComparison.OrdinalIgnoreCase))
			PushDataWarning($"Ingredient '{item.Id}' crushed and boiled preparations should use the same second trait.");

		if (string.Equals(rawTrait.Key, crushedTrait.Key, StringComparison.OrdinalIgnoreCase))
			PushDataWarning($"Ingredient '{item.Id}' should expose two distinct preparation traits.");

		ValidateNoPreparationRisks(item.Id, IngredientPreparationCatalog.RawPreparationId, raw);
		ValidateNoPreparationRisks(item.Id, IngredientPreparationCatalog.CrushedPreparationId, crushed);
		ValidateSteepedPreparationRisks(item.Id, steeped);
		ValidateBoiledPreparationRisks(item.Id, boiled);
		ValidateBoilingMiniGameConfig(item.Id, boiled);
	}

	private static bool TryGetAuthoredPreparation(ItemDef item, string preparationId, out IngredientPreparationDef preparation)
	{
		preparation = default!;
		if (item.Preparations is null)
			return false;

		if (item.Preparations.TryGetValue(preparationId, out preparation!) && preparation is not null)
			return true;

		return false;
	}

	private static bool TryGetSinglePositiveTrait(
		string itemId,
		string preparationId,
		IngredientPreparationDef preparation,
		out KeyValuePair<string, int> trait)
	{
		trait = default;
		var positiveTraits = preparation.Traits?
			.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
			.ToList() ?? new List<KeyValuePair<string, int>>();

		if (positiveTraits.Count != 1)
		{
			PushDataWarning($"Ingredient '{itemId}' preparation '{preparationId}' should define exactly one positive trait.");
			return false;
		}

		trait = positiveTraits[0];
		return true;
	}

	private static void ValidateTraitValueRange(
		string itemId,
		string preparationId,
		KeyValuePair<string, int> trait,
		int min,
		int max)
	{
		if (trait.Value < min || trait.Value > max)
		{
			PushDataWarning(
				$"Ingredient '{itemId}' preparation '{preparationId}' trait '{trait.Key}' should be between {min} and {max}.");
		}
	}

	private static void ValidateNoPreparationRisks(
		string itemId,
		string preparationId,
		IngredientPreparationDef preparation)
	{
		var riskCount = CountPositiveRisks(preparation);
		if (riskCount > 0)
			PushDataWarning($"Ingredient '{itemId}' preparation '{preparationId}' should not define risks.");
	}

	private static void ValidateSteepedPreparationRisks(
		string itemId,
		IngredientPreparationDef steeped)
	{
		var steepedRiskCount = CountPositiveRisks(steeped);
		if (steepedRiskCount > 1)
			PushDataWarning($"Ingredient '{itemId}' steeped preparation should define no more than one risk.");

		ValidateRiskChanceValues(itemId, IngredientPreparationCatalog.SteepedPreparationId, steeped);
	}

	private static void ValidateBoiledPreparationRisks(
		string itemId,
		IngredientPreparationDef boiled)
	{
		var boiledRiskCount = CountPositiveRisks(boiled);
		if (boiledRiskCount != 1)
			PushDataWarning($"Ingredient '{itemId}' boiled preparation should define exactly one risk.");

		ValidateRiskChanceValues(itemId, IngredientPreparationCatalog.BoiledPreparationId, boiled);
	}

	private static void ValidateBoilingMiniGameConfig(string itemId, IngredientPreparationDef boiled)
	{
		var boilingGame = boiled.BoilingGame;
		if (boilingGame is null)
		{
			PushDataWarning($"Ingredient '{itemId}' boiled preparation is missing boiling mini game data.");
			return;
		}

		if (string.IsNullOrWhiteSpace(boilingGame.FailureRiskId))
			PushDataWarning($"Ingredient '{itemId}' boiling mini game is missing failureRiskId.");

		if (!IsValidUnitRange(boilingGame.TemperatureTargetMin, boilingGame.TemperatureTargetMax))
			PushDataWarning($"Ingredient '{itemId}' boiling temperature target should be a 0-1 range with min below max.");

		if (boilingGame.TemperatureHoldSeconds <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling temperatureHoldSeconds should be greater than 0.");

		if (boilingGame.HeatLockSeconds <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling heatLockSeconds should be greater than 0.");

		if (boilingGame.HeatRiseRate <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling heatRiseRate should be greater than 0.");

		if (boilingGame.HeatFallRate <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling heatFallRate should be greater than 0.");

		if (boilingGame.DonenessDurationSeconds <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling donenessDurationSeconds should be greater than 0.");

		if (!IsValidUnitRange(boilingGame.DonenessWindowStart, boilingGame.DonenessWindowEnd))
			PushDataWarning($"Ingredient '{itemId}' boiling doneness window should be a 0-1 range with start below end.");

		if (boilingGame.StirringHoldSeconds <= 0.0f)
			PushDataWarning($"Ingredient '{itemId}' boiling stirringHoldSeconds should be greater than 0.");
	}

	private static bool IsValidUnitRange(float min, float max)
	{
		return min >= 0.0f && max <= 1.0f && min < max;
	}

	private static int CountPositiveRisks(IngredientPreparationDef preparation)
	{
		if (preparation.Risks is null || preparation.Risks.Count == 0)
			return 0;

		var count = 0;
		foreach (var risk in preparation.Risks)
		{
			if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
				count += 1;
		}

		return count;
	}

	private static void ValidateRiskChanceValues(
		string itemId,
		string preparationId,
		IngredientPreparationDef preparation)
	{
		if (preparation.Risks is null)
			return;

		foreach (var risk in preparation.Risks)
		{
			if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
				continue;

			if (risk.Value > MaxRiskChanceValue)
			{
				PushDataWarning(
					$"Ingredient '{itemId}' preparation '{preparationId}' risk '{risk.Key}' should be between 1 and {MaxRiskChanceValue}.");
			}
		}
	}

	private static void ValidateIngredientEffects(string itemId, IReadOnlyList<IngredientEffectDef>? effects)
	{
		if (effects is null || effects.Count == 0)
			return;

		foreach (var effect in effects)
		{
			if (effect is null || string.IsNullOrWhiteSpace(effect.Kind))
			{
				PushDataWarning($"Item '{itemId}' has an ingredient effect without a kind.");
				continue;
			}

			if (!KnownIngredientEffectKinds.Contains(effect.Kind))
				PushDataWarning($"Item '{itemId}' has unknown ingredient effect kind '{effect.Kind}'.");

			if (string.Equals(effect.Kind, IngredientEffectDef.BoostStrongestTraitAddRiskKind, StringComparison.OrdinalIgnoreCase) &&
				string.IsNullOrWhiteSpace(effect.RiskId))
				PushDataWarning($"Item '{itemId}' effect '{effect.Kind}' should define a risk id.");

			if (string.Equals(effect.Kind, IngredientEffectDef.AddTraitIfRiskCarriesKind, StringComparison.OrdinalIgnoreCase) &&
				string.IsNullOrWhiteSpace(effect.TraitId))
				PushDataWarning($"Item '{itemId}' effect '{effect.Kind}' should define a trait id.");
		}
	}

	private static void ValidateConsumableEffect(string itemId, ConsumableEffectDef effect)
	{
		if (string.Equals(effect.Kind, ConsumableEffectDef.RemoveRiskKind, StringComparison.OrdinalIgnoreCase) &&
			string.IsNullOrWhiteSpace(effect.RiskId))
		{
			PushDataWarning($"Item '{itemId}' removes a risk but does not define a risk id.");
		}
	}

	private static void ValidateTreatmentMetadata(
		IReadOnlyDictionary<string, ItemDef> items,
		string itemId,
		ItemTreatmentDef treatment)
	{
		if (string.IsNullOrWhiteSpace(treatment.BaseItemId))
			PushDataWarning($"Item '{itemId}' has treatment metadata without a base item id.");
		else
			ValidateKnownItemReference(items, treatment.BaseItemId, $"Item '{itemId}' treatment base item");

		if (string.IsNullOrWhiteSpace(treatment.ConsumableItemId))
			PushDataWarning($"Item '{itemId}' has treatment metadata without a consumable item id.");
		else
			ValidateKnownItemReference(items, treatment.ConsumableItemId, $"Item '{itemId}' treatment consumable item");

		if (string.IsNullOrWhiteSpace(treatment.RemovedRisk))
			PushDataWarning($"Item '{itemId}' has treatment metadata without a removed risk id.");
	}

	private static void ValidatePotionRecipes(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyList<PotionRecipeDef> potionRecipes)
	{
		var recipeIdsByCombination = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var recipe in potionRecipes)
		{
			var context = $"Potion recipe '{recipe.Id}'";
			if (recipe.IngredientIds is null || recipe.IngredientIds.Count != PotionRecipeIngredientCount)
			{
				PushDataWarning($"{context} should define exactly {PotionRecipeIngredientCount} ingredient ids.");
				continue;
			}

			var normalizedIngredientIds = recipe.IngredientIds
				.Select(id => id.Trim())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.ToList();

			if (normalizedIngredientIds.Count != PotionRecipeIngredientCount)
			{
				PushDataWarning($"{context} includes empty ingredient ids.");
				continue;
			}

			if (ContainsDuplicateText(normalizedIngredientIds))
			{
				PushDataWarning($"{context} includes duplicate ingredient ids.");
				continue;
			}

			foreach (var ingredientId in normalizedIngredientIds)
				ValidateIngredientReference(items, ingredientId, context);

			var recipePortions = BuildRecipePortionsForValidation(items, recipe, normalizedIngredientIds);
			if (recipePortions.Count == 0)
				continue;

			var combinationKey = PotionRecipeLookup.BuildCombinationKey(recipePortions);
			if (recipeIdsByCombination.TryGetValue(combinationKey, out var existingRecipeId))
			{
				PushDataWarning($"{context} duplicates ingredient combination '{combinationKey}' already used by recipe '{existingRecipeId}'.");
				continue;
			}

			recipeIdsByCombination[combinationKey] = recipe.Id;
		}
	}

	private static void ValidateCalendarEvents(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyList<CalendarEventDef> calendarEvents)
	{
		foreach (var calendarEvent in calendarEvents)
		{
			var context = $"Calendar event '{calendarEvent.Id}'";
			if (calendarEvent.Day is < 1 or > GameCalendar.DaysPerMonth)
				PushDataWarning($"{context} has day {calendarEvent.Day}; expected 1-{GameCalendar.DaysPerMonth}.");
			if (calendarEvent.Month is < 1 or > GameCalendar.MonthsPerYear)
				PushDataWarning($"{context} has month {calendarEvent.Month}; expected 1-{GameCalendar.MonthsPerYear}.");
			if (!calendarEvent.RepeatsYearly && calendarEvent.Year is null)
				PushDataWarning($"{context} must define a year or set repeatsYearly.");
			if (calendarEvent.Year is int year && year < 1)
				PushDataWarning($"{context} has year {year}; expected 1 or greater.");

			ValidateRequirements(items, calendarEvent.VisibilityRequirements, $"{context} visibility requirements");
		}
	}

	private static void ValidateCustomerInteractions(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IReadOnlyList<CustomerInteractionDef> customerInteractions)
	{
		var knownTraitIds = BuildKnownTraitIds(items);
		var knownRiskIds = BuildKnownRiskIds(items);
		var knownStoryCharacterIds = BuildKnownStoryCharacterIds(customerInteractions);
		foreach (var interaction in customerInteractions)
		{
			var context = $"Customer interaction '{interaction.Id}'";
			ValidateStoryStateId(interaction.StoryCharacterId, $"{context} story character id");
			ValidateRequirements(items, interaction.Requires, $"{context} requirements", knownStoryCharacterIds);
			ValidateTraitRanges(interaction.DesiredTraits, knownTraitIds, $"{context} desired traits");
			ValidateTraitRanges(interaction.BadTraits, knownTraitIds, knownRiskIds, $"{context} bad traits");
			ValidateTraitThresholds(interaction.RequiredMinTraits, knownTraitIds, $"{context} required minimum traits");
			ValidateTraitThresholds(interaction.RequiredMaxTraits, knownTraitIds, $"{context} required maximum traits");
			ValidateIngredientAmounts(items, interaction.RequiredIngredientAmounts, $"{context} required ingredient amounts");
			ValidateEffects(items, rules, interaction.OnArrivalEffects, $"{context} arrival effects", knownStoryCharacterIds);
			ValidateEffects(items, rules, interaction.OnSuccessEffects, $"{context} success effects", knownStoryCharacterIds);
			ValidateEffects(items, rules, interaction.OnFailureEffects, $"{context} failure effects", knownStoryCharacterIds);
			ValidateEffects(items, rules, interaction.OnSkipEffects, $"{context} skip effects", knownStoryCharacterIds);
			ValidateEffects(items, rules, interaction.OnPotionRefusedEffects, $"{context} potion refused effects", knownStoryCharacterIds);
			ValidateCharacterImageKeys(interaction);
			ValidatePotionResponses(items, rules, interaction, knownStoryCharacterIds);
			ValidateDialogueTree(items, rules, interaction, knownStoryCharacterIds);
		}
	}

	private static void ValidateCharacterImageKeys(CustomerInteractionDef interaction)
	{
		var knownImageKeys = new HashSet<string>(
			interaction.CharacterImagePaths?.Keys
				.Where(key => !string.IsNullOrWhiteSpace(key))
				.Select(key => key.Trim()) ?? Enumerable.Empty<string>(),
			StringComparer.OrdinalIgnoreCase);

		var context = $"Customer interaction '{interaction.Id}'";
		ValidateDialogueLineImageKeys(interaction.Lines, knownImageKeys, $"{context} lines");
		ValidateDialogueLineImageKeys(interaction.PotionRefusedLines, knownImageKeys, $"{context} potion refused lines");
		foreach (var response in interaction.PotionResponses)
			ValidateDialogueLineImageKeys(response.Lines, knownImageKeys, $"{context} potion response '{response.Id}' lines");

		foreach (var node in interaction.DialogueNodes)
		{
			ValidateDialogueLineImageKeys(node.Lines, knownImageKeys, $"{context} dialogue node '{node.Id}' lines");
			foreach (var option in node.Options)
				ValidateDialogueLineImageKeys(option.ResponseLines, knownImageKeys, $"{context} option '{option.Id}' response lines");
		}
	}

	private static void ValidateDialogueLineImageKeys(
		IReadOnlyList<CustomerDialogueLineDef>? lines,
		HashSet<string> knownImageKeys,
		string context)
	{
		if (lines is null || lines.Count == 0)
			return;

		foreach (var line in lines)
		{
			if (line is null || string.IsNullOrWhiteSpace(line.CharacterImageKey))
				continue;

			var imageKey = line.CharacterImageKey.Trim();
			if (!knownImageKeys.Contains(imageKey))
				PushDataWarning($"{context} references unknown character image key '{imageKey}'.");
		}
	}

	private static HashSet<string> BuildKnownTraitIds(IReadOnlyDictionary<string, ItemDef> items)
	{
		var knownTraitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in items.Values)
		{
			if (item.Traits is not null)
			{
				foreach (var traitId in item.Traits.Keys)
				{
					if (!string.IsNullOrWhiteSpace(traitId))
						knownTraitIds.Add(traitId);
				}
			}

			if (item.Preparations is not null)
			{
				foreach (var preparation in item.Preparations.Values)
				{
					if (preparation?.Traits is null)
						continue;

					foreach (var traitId in preparation.Traits.Keys)
					{
						if (!string.IsNullOrWhiteSpace(traitId))
							knownTraitIds.Add(traitId);
					}
				}
			}

			if (item.IngredientEffects is null)
				continue;

			foreach (var effect in item.IngredientEffects)
			{
				if (!string.IsNullOrWhiteSpace(effect.TraitId))
					knownTraitIds.Add(effect.TraitId);
			}
		}

		return knownTraitIds;
	}

	private static HashSet<string> BuildKnownRiskIds(IReadOnlyDictionary<string, ItemDef> items)
	{
		var knownRiskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in items.Values)
		{
			if (item.Risks is not null)
			{
				foreach (var riskId in item.Risks.Keys)
				{
					if (!string.IsNullOrWhiteSpace(riskId))
						knownRiskIds.Add(riskId);
				}
			}

			if (item.Preparations is not null)
			{
				foreach (var preparation in item.Preparations.Values)
				{
					if (preparation?.Risks is null)
						continue;

					foreach (var riskId in preparation.Risks.Keys)
					{
						if (!string.IsNullOrWhiteSpace(riskId))
							knownRiskIds.Add(riskId);
					}
				}
			}

			if (item.IngredientEffects is null)
				continue;

			foreach (var effect in item.IngredientEffects)
			{
				if (!string.IsNullOrWhiteSpace(effect.RiskId))
					knownRiskIds.Add(effect.RiskId);
			}
		}

		return knownRiskIds;
	}

	private static HashSet<string> BuildKnownStoryCharacterIds(IReadOnlyList<CustomerInteractionDef> customerInteractions)
	{
		var knownStoryCharacterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var interaction in customerInteractions)
		{
			if (!string.IsNullOrWhiteSpace(interaction.StoryCharacterId))
				knownStoryCharacterIds.Add(interaction.StoryCharacterId.Trim());
		}

		return knownStoryCharacterIds;
	}

	private static void ValidateTraitRanges(
		IReadOnlyDictionary<string, CustomerTraitRangeDef>? ranges,
		HashSet<string> knownTraitIds,
		string context)
	{
		ValidateTraitRanges(ranges, knownTraitIds, null, context);
	}

	private static void ValidateTraitRanges(
		IReadOnlyDictionary<string, CustomerTraitRangeDef>? ranges,
		HashSet<string> knownTraitIds,
		HashSet<string>? knownRiskIds,
		string context)
	{
		if (ranges is null || ranges.Count == 0)
			return;

		foreach (var range in ranges)
		{
			if (string.IsNullOrWhiteSpace(range.Key))
			{
				PushDataWarning($"{context} includes an empty trait id.");
				continue;
			}

			if (range.Value is null)
			{
				PushDataWarning($"{context} for '{range.Key}' has no range.");
				continue;
			}

			if (!range.Value.HasMin && !range.Value.HasMax)
				PushDataWarning($"{context} for '{range.Key}' has no min or max.");

			if (range.Value.Min is int min && min < 0)
				PushDataWarning($"{context} minimum for '{range.Key}' is negative.");

			if (range.Value.Max is int max && max < 0)
				PushDataWarning($"{context} maximum for '{range.Key}' is negative.");

			if (range.Value.Min is int minValue && range.Value.Max is int maxValue && minValue > maxValue)
				PushDataWarning($"{context} for '{range.Key}' has min greater than max.");

			var isKnownTrait = knownTraitIds.Contains(range.Key);
			var isKnownRisk = knownRiskIds is not null && knownRiskIds.Contains(range.Key);
			if (!isKnownTrait && !isKnownRisk)
			{
				var expectedKind = knownRiskIds is null ? "trait" : "trait or risk";
				PushDataWarning($"{context} references unknown {expectedKind} '{range.Key}'.");
			}
		}
	}

	private static void ValidateTraitThresholds(
		IReadOnlyDictionary<string, int>? thresholds,
		HashSet<string> knownTraitIds,
		string context)
	{
		if (thresholds is null || thresholds.Count == 0)
			return;

		foreach (var threshold in thresholds)
		{
			if (string.IsNullOrWhiteSpace(threshold.Key))
			{
				PushDataWarning($"{context} includes an empty trait id.");
				continue;
			}

			if (threshold.Value < 0)
				PushDataWarning($"{context} for '{threshold.Key}' is negative.");

			if (!knownTraitIds.Contains(threshold.Key))
				PushDataWarning($"{context} references unknown trait '{threshold.Key}'.");
		}
	}

	private static void ValidateDialogueTree(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		CustomerInteractionDef interaction,
		HashSet<string> knownStoryCharacterIds)
	{
		if (interaction.DialogueNodes.Count == 0)
		{
			if (!string.IsNullOrWhiteSpace(interaction.DialogueStartNodeId))
				PushDataWarning($"Customer interaction '{interaction.Id}' defines a dialogue start node but has no dialogue nodes.");

			return;
		}

		var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var node in interaction.DialogueNodes)
		{
			if (!nodeIds.Add(node.Id))
				PushDataWarning($"Customer interaction '{interaction.Id}' has duplicate dialogue node id '{node.Id}'.");
		}

		if (!string.IsNullOrWhiteSpace(interaction.DialogueStartNodeId) &&
			!nodeIds.Contains(interaction.DialogueStartNodeId))
		{
			PushDataWarning($"Customer interaction '{interaction.Id}' starts at missing dialogue node '{interaction.DialogueStartNodeId}'.");
		}

		var optionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var node in interaction.DialogueNodes)
		{
			if (node.Options.Count > CustomerInteractionDef.MaxDialogueOptionsPerNode)
			{
				PushDataWarning(
					$"Customer interaction '{interaction.Id}' dialogue node '{node.Id}' defines {node.Options.Count} options; only {CustomerInteractionDef.MaxDialogueOptionsPerNode} are shown.");
			}

			foreach (var option in node.Options)
			{
				var optionContext = $"Customer interaction '{interaction.Id}' dialogue node '{node.Id}' option '{option.Id}'";
				if (!string.IsNullOrWhiteSpace(option.Id) && !optionIds.Add(option.Id))
					PushDataWarning($"{optionContext} duplicates a dialogue option id used elsewhere in this interaction.");

				ValidateRequirements(items, option.Requires, $"{optionContext} requirements", knownStoryCharacterIds);
				ValidateEffects(items, rules, option.Effects, $"{optionContext} effects", knownStoryCharacterIds);

				if (!option.EndsInteraction &&
					!string.IsNullOrWhiteSpace(option.NextNodeId) &&
					!nodeIds.Contains(option.NextNodeId))
				{
					PushDataWarning($"{optionContext} points to missing dialogue node '{option.NextNodeId}'.");
				}

				if (!string.IsNullOrWhiteSpace(option.ReturnNodeId) && !nodeIds.Contains(option.ReturnNodeId))
					PushDataWarning($"{optionContext} returns to missing dialogue node '{option.ReturnNodeId}'.");

				if (option.RevealsRequest && option.EndsInteraction)
					PushDataWarning($"{optionContext} both reveals the potion request and ends the interaction.");

				if (option.ReturnsToDialogue && option.EndsInteraction)
					PushDataWarning($"{optionContext} both returns to dialogue and ends the interaction.");

				if (!option.RevealsRequest &&
					!option.ReturnsToDialogue &&
					!option.EndsInteraction &&
					string.IsNullOrWhiteSpace(option.NextNodeId) &&
					string.IsNullOrWhiteSpace(option.ResponseText) &&
					option.ResponseLines.Count == 0)
				{
					PushDataWarning($"{optionContext} has no response, target node, request reveal, or terminal action.");
				}
			}
		}
	}

	private static void ValidatePotionResponses(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		CustomerInteractionDef interaction,
		HashSet<string> knownStoryCharacterIds)
	{
		foreach (var response in interaction.PotionResponses)
		{
			var responseContext = string.IsNullOrWhiteSpace(response.Id)
				? $"Customer interaction '{interaction.Id}' potion response"
				: $"Customer interaction '{interaction.Id}' potion response '{response.Id}'";

			if (!string.IsNullOrWhiteSpace(response.PotionItemId))
				ValidateKnownItemReference(items, response.PotionItemId, $"{responseContext} potion item");

			if (response.MinFinalScore is int minScore && response.MaxFinalScore is int maxScore && minScore > maxScore)
				PushDataWarning($"{responseContext} has minFinalScore greater than maxFinalScore.");

			if (response.MinMatchedDesiredTraits is int minMatchedDesiredTraits && minMatchedDesiredTraits < 0)
				PushDataWarning($"{responseContext} has a negative minMatchedDesiredTraits.");

			if (response.MaxMatchedBadTraits is int maxMatchedBadTraits && maxMatchedBadTraits < 0)
				PushDataWarning($"{responseContext} has a negative maxMatchedBadTraits.");

			ValidateEffects(items, rules, response.Effects, $"{responseContext} effects", knownStoryCharacterIds);
		}
	}

	private static void ValidateEffects(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IEnumerable<EffectDef> effects,
		string context,
		HashSet<string>? knownStoryCharacterIds = null)
	{
		var effectIndex = 0;
		foreach (var effect in effects)
		{
			effectIndex++;
			var effectContext = $"{context} #{effectIndex}";

			if (!string.IsNullOrWhiteSpace(effect.AddRule))
				ValidateKnownRuleReference(rules, effect.AddRule, $"{effectContext} add rule");

			ValidateStoryFlagName(effect.AddStoryFlag, $"{effectContext} add story flag");
			ValidateStoryFlagName(effect.RemoveStoryFlag, $"{effectContext} remove story flag");
			ValidateStoryScore(effect.SetReputation, $"{effectContext} set reputation");
			ValidateQuestStatusEffect(effect, effectContext);
			ValidateRelationshipEffect(effect, effectContext, knownStoryCharacterIds);

			if (!string.IsNullOrWhiteSpace(effect.AddItemId))
				ValidateKnownItemReference(items, effect.AddItemId, $"{effectContext} add item");

			if (!string.IsNullOrWhiteSpace(effect.RestockItemId))
				ValidateKnownItemReference(items, effect.RestockItemId, $"{effectContext} restock item");

			if (!string.IsNullOrWhiteSpace(effect.EnableIngredientPreparationMethodId) &&
				!IngredientPreparationCatalog.IsKnownPreparationId(effect.EnableIngredientPreparationMethodId))
			{
				PushDataWarning($"{effectContext} enables unknown ingredient preparation method '{effect.EnableIngredientPreparationMethodId}'.");
			}

			if (!string.IsNullOrWhiteSpace(effect.ConsumeItemId))
				ValidateKnownItemReference(items, effect.ConsumeItemId, $"{effectContext} consume item");
		}
	}

	private static void ValidateRequirements(
		IReadOnlyDictionary<string, ItemDef> items,
		RequirementsDef? requirements,
		string context,
		HashSet<string>? knownStoryCharacterIds = null)
	{
		if (requirements is null)
			return;

		if (!string.IsNullOrWhiteSpace(requirements.HasItemId))
			ValidateKnownItemReference(items, requirements.HasItemId, $"{context} required item");

		ValidateStoryFlagName(requirements.HasStoryFlag, $"{context} required story flag");
		ValidateStoryFlagName(requirements.MissingStoryFlag, $"{context} missing story flag");
		ValidateStoryScore(requirements.ReputationMin, $"{context} reputation minimum");
		ValidateStoryScore(requirements.ReputationMax, $"{context} reputation maximum");
		if (requirements.ReputationMin is int reputationMin &&
			requirements.ReputationMax is int reputationMax &&
			reputationMin > reputationMax)
		{
			PushDataWarning($"{context} has reputation minimum greater than maximum.");
		}

		ValidateQuestRequirement(requirements, context);
		ValidateRelationshipRequirement(requirements, context, knownStoryCharacterIds);
	}

	private static void ValidateQuestRequirement(RequirementsDef requirements, string context)
	{
		if (string.IsNullOrWhiteSpace(requirements.QuestId) &&
			string.IsNullOrWhiteSpace(requirements.QuestStatus))
		{
			return;
		}

		ValidateStoryStateId(requirements.QuestId, $"{context} quest id");
		if (string.IsNullOrWhiteSpace(requirements.QuestId))
			PushDataWarning($"{context} defines a quest status without a quest id.");
		if (string.IsNullOrWhiteSpace(requirements.QuestStatus))
			PushDataWarning($"{context} defines a quest id without a quest status.");
		else if (!Enum.TryParse<QuestStatus>(requirements.QuestStatus.Trim(), ignoreCase: true, out _))
			PushDataWarning($"{context} references unknown quest status '{requirements.QuestStatus}'.");
	}

	private static void ValidateRelationshipRequirement(
		RequirementsDef requirements,
		string context,
		HashSet<string>? knownStoryCharacterIds)
	{
		if (requirements.RelationshipMin is null &&
			requirements.RelationshipMax is null &&
			string.IsNullOrWhiteSpace(requirements.RelationshipCharacterId))
		{
			return;
		}

		ValidateStoryStateId(requirements.RelationshipCharacterId, $"{context} relationship character id");
		ValidateKnownStoryCharacterReference(requirements.RelationshipCharacterId, $"{context} relationship character", knownStoryCharacterIds);
		ValidateStoryScore(requirements.RelationshipMin, $"{context} relationship minimum");
		ValidateStoryScore(requirements.RelationshipMax, $"{context} relationship maximum");
		if (requirements.RelationshipMin is int relationshipMin &&
			requirements.RelationshipMax is int relationshipMax &&
			relationshipMin > relationshipMax)
		{
			PushDataWarning($"{context} has relationship minimum greater than maximum.");
		}

		if ((requirements.RelationshipMin is not null || requirements.RelationshipMax is not null) &&
			string.IsNullOrWhiteSpace(requirements.RelationshipCharacterId))
		{
			PushDataWarning($"{context} defines a relationship range without a relationship character id.");
		}
	}

	private static void ValidateQuestStatusEffect(EffectDef effect, string context)
	{
		if (string.IsNullOrWhiteSpace(effect.QuestId) &&
			string.IsNullOrWhiteSpace(effect.SetQuestStatus))
		{
			return;
		}

		ValidateStoryStateId(effect.QuestId, $"{context} quest id");
		if (string.IsNullOrWhiteSpace(effect.QuestId))
			PushDataWarning($"{context} sets a quest status without a quest id.");
		if (string.IsNullOrWhiteSpace(effect.SetQuestStatus))
			PushDataWarning($"{context} defines a quest id without a set quest status.");
		else if (!Enum.TryParse<QuestStatus>(effect.SetQuestStatus.Trim(), ignoreCase: true, out _))
			PushDataWarning($"{context} sets unknown quest status '{effect.SetQuestStatus}'.");
	}

	private static void ValidateRelationshipEffect(
		EffectDef effect,
		string context,
		HashSet<string>? knownStoryCharacterIds)
	{
		if (effect.AddRelationship is null &&
			effect.SetRelationship is null &&
			string.IsNullOrWhiteSpace(effect.RelationshipCharacterId))
		{
			return;
		}

		ValidateStoryStateId(effect.RelationshipCharacterId, $"{context} relationship character id");
		ValidateKnownStoryCharacterReference(effect.RelationshipCharacterId, $"{context} relationship character", knownStoryCharacterIds);
		ValidateStoryScore(effect.SetRelationship, $"{context} set relationship");
		if ((effect.AddRelationship is not null || effect.SetRelationship is not null) &&
			string.IsNullOrWhiteSpace(effect.RelationshipCharacterId))
		{
			PushDataWarning($"{context} changes relationship without a relationship character id.");
		}
	}

	private static void ValidateStoryFlagName(string? storyFlag, string context)
	{
		ValidateStoryStateId(storyFlag, context);
	}

	private static void ValidateStoryStateId(string? value, string context)
	{
		if (string.IsNullOrWhiteSpace(value))
			return;

		var trimmedValue = value.Trim();
		if (!StoryStateIdPattern.IsMatch(trimmedValue))
			PushDataWarning($"{context} '{trimmedValue}' should use lower_snake_case letters and numbers.");
	}

	private static void ValidateStoryScore(int? value, string context)
	{
		if (value is null)
			return;
		if (value < GameState.MinStoryScore || value > GameState.MaxStoryScore)
			PushDataWarning($"{context} should be between {GameState.MinStoryScore} and {GameState.MaxStoryScore}.");
	}

	private static void ValidateKnownStoryCharacterReference(
		string? storyCharacterId,
		string context,
		HashSet<string>? knownStoryCharacterIds)
	{
		if (string.IsNullOrWhiteSpace(storyCharacterId) || knownStoryCharacterIds is null)
			return;

		var normalizedCharacterId = storyCharacterId.Trim();
		if (!knownStoryCharacterIds.Contains(normalizedCharacterId))
			PushDataWarning($"{context} references unknown story character '{normalizedCharacterId}'.");
	}

	private static void ValidateIngredientReference(IReadOnlyDictionary<string, ItemDef> items, string ingredientId, string context)
	{
		if (!items.TryGetValue(ingredientId, out var item))
		{
			PushDataWarning($"{context} references unknown ingredient '{ingredientId}'.");
			return;
		}

		if (!HasTag(item, ItemTags.Ingredient))
			PushDataWarning($"{context} references item '{ingredientId}', but it is not tagged as an ingredient.");
	}

	private static List<IngredientPortionDef> BuildRecipePortionsForValidation(
		IReadOnlyDictionary<string, ItemDef> items,
		PotionRecipeDef recipe,
		IReadOnlyList<string> normalizedIngredientIds)
	{
		var context = $"Potion recipe '{recipe.Id}'";
		if (recipe.IngredientAmounts is null || recipe.IngredientAmounts.Count == 0)
		{
			return normalizedIngredientIds
				.Select(id => new IngredientPortionDef
				{
					IngredientId = id,
					Grams = 0
				})
				.ToList();
		}

		if (recipe.IngredientAmounts.Count != PotionRecipeIngredientCount)
		{
			PushDataWarning($"{context} should define exactly {PotionRecipeIngredientCount} ingredient portion requirements.");
			return new List<IngredientPortionDef>();
		}

		var portions = new List<IngredientPortionDef>();
		foreach (var portion in recipe.IngredientAmounts)
		{
			if (portion is null || string.IsNullOrWhiteSpace(portion.IngredientId))
			{
				PushDataWarning($"{context} includes an empty ingredient amount id.");
				return new List<IngredientPortionDef>();
			}

			if (portion.Grams <= 0 && string.IsNullOrWhiteSpace(portion.PreparationId))
			{
				PushDataWarning($"{context} ingredient amount '{portion.IngredientId}' must define grams or a preparation.");
				return new List<IngredientPortionDef>();
			}

			ValidateIngredientPreparationReference(items, portion.IngredientId, portion.PreparationId, context);

			if (!normalizedIngredientIds.Any(id => string.Equals(id, portion.IngredientId, StringComparison.OrdinalIgnoreCase)))
			{
				PushDataWarning($"{context} ingredient amount '{portion.IngredientId}' is not listed in ingredientIds.");
				return new List<IngredientPortionDef>();
			}

			portions.Add(new IngredientPortionDef
			{
				IngredientId = portion.IngredientId,
				ItemId = portion.ItemId,
				PreparationId = portion.PreparationId,
				Grams = portion.Grams
			});
		}

		if (ContainsDuplicateText(portions.Select(x => x.IngredientId)))
		{
			PushDataWarning($"{context} includes duplicate ingredient amount ids.");
			return new List<IngredientPortionDef>();
		}

		return portions;
	}

	private static void ValidateIngredientAmounts(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyList<IngredientPortionDef>? ingredientAmounts,
		string context)
	{
		if (ingredientAmounts is null || ingredientAmounts.Count == 0)
			return;

		foreach (var ingredientAmount in ingredientAmounts)
		{
			if (ingredientAmount is null)
				continue;

			ValidateIngredientReference(items, ingredientAmount.IngredientId, context);
			ValidateIngredientPreparationReference(items, ingredientAmount.IngredientId, ingredientAmount.PreparationId, context);
			if (ingredientAmount.Grams <= 0 && string.IsNullOrWhiteSpace(ingredientAmount.PreparationId))
				PushDataWarning($"{context} for '{ingredientAmount.IngredientId}' must define grams or a preparation.");
		}
	}

	private static void ValidateIngredientPreparationReference(
		IReadOnlyDictionary<string, ItemDef> items,
		string ingredientId,
		string preparationId,
		string context)
	{
		if (string.IsNullOrWhiteSpace(preparationId))
			return;

		if (!IngredientPreparationCatalog.IsKnownPreparationId(preparationId))
		{
			PushDataWarning($"{context} for '{ingredientId}' references unknown preparation '{preparationId}'.");
			return;
		}

		if (!items.TryGetValue(ingredientId, out var item) || item.Preparations is null || item.Preparations.Count == 0)
			return;

		if (!item.Preparations.ContainsKey(preparationId))
			PushDataWarning($"{context} for '{ingredientId}' references missing preparation '{preparationId}'.");
	}

	private static void ValidateKnownItemReference(IReadOnlyDictionary<string, ItemDef> items, string itemId, string context)
	{
		if (!items.ContainsKey(itemId))
			PushDataWarning($"{context} references unknown item '{itemId}'.");
	}

	private static void ValidateKnownRuleReference(IReadOnlyDictionary<string, RuleDef> rules, string ruleId, string context)
	{
		if (!rules.ContainsKey(ruleId))
			PushDataWarning($"{context} references unknown rule '{ruleId}'.");
	}

	private static bool ContainsDuplicateText(IEnumerable<string> values)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var value in values)
		{
			if (!seen.Add(value))
				return true;
		}

		return false;
	}

	private static bool HasTag(ItemDef item, string tag)
	{
		if (item.Tags is null || string.IsNullOrWhiteSpace(tag))
			return false;

		return item.Tags.Any(existingTag => string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase));
	}

	private static void PushDataWarning(string message)
	{
		GD.PushWarning($"DataDb: {message}");
	}
}
