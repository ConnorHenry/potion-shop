using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class AuthoredDataValidator
{
	private const int PotionRecipeIngredientCount = 3;
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
		IReadOnlyList<EventCardDef> events,
		IReadOnlyList<CustomerInteractionDef> customerInteractions,
		IReadOnlyList<PotionRecipeDef> potionRecipes)
	{
		ValidateItemDefinitions(items);
		ValidatePotionRecipes(items, potionRecipes);
		ValidateEventCards(items, rules, events);
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

			ValidateIngredientEffects(item.Id, item.IngredientEffects);

			if (item.Treatment is not null)
				ValidateTreatmentMetadata(items, item.Id, item.Treatment);
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

			var recipePortions = BuildRecipePortionsForValidation(recipe, normalizedIngredientIds);
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

	private static void ValidateEventCards(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IReadOnlyList<EventCardDef> events)
	{
		foreach (var eventCard in events)
		{
			var context = $"Event '{eventCard.Id}'";
			ValidateRequirements(items, eventCard.Requires, $"{context} requirements");

			if (eventCard.Choices.Count == 0)
				PushDataWarning($"{context} has no choices.");

			foreach (var choice in eventCard.Choices)
			{
				var choiceContext = $"{context} choice '{choice.Label}'";
				ValidateRequirements(items, choice.Requires, $"{choiceContext} requirements");
				ValidateEffects(items, rules, choice.Effects, $"{choiceContext} effects");
			}
		}
	}

	private static void ValidateCustomerInteractions(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IReadOnlyList<CustomerInteractionDef> customerInteractions)
	{
		var knownTraitIds = BuildKnownTraitIds(items);
		foreach (var interaction in customerInteractions)
		{
			var context = $"Customer interaction '{interaction.Id}'";
			ValidateRequirements(items, interaction.Requires, $"{context} requirements");
			ValidateTraitThresholds(interaction.RequiredMinTraits, knownTraitIds, $"{context} required minimum traits");
			ValidateTraitThresholds(interaction.RequiredMaxTraits, knownTraitIds, $"{context} required maximum traits");
			ValidateIngredientAmounts(items, interaction.RequiredIngredientAmounts, $"{context} required ingredient amounts");
			ValidateEffects(items, rules, interaction.OnSuccessEffects, $"{context} success effects");
			ValidateEffects(items, rules, interaction.OnFailureEffects, $"{context} failure effects");
			ValidateEffects(items, rules, interaction.OnSkipEffects, $"{context} skip effects");
			ValidateEffects(items, rules, interaction.OnPotionRefusedEffects, $"{context} potion refused effects");
			ValidatePotionResponses(items, rules, interaction);
			ValidateDialogueTree(items, rules, interaction);
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
		CustomerInteractionDef interaction)
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

				ValidateRequirements(items, option.Requires, $"{optionContext} requirements");
				ValidateEffects(items, rules, option.Effects, $"{optionContext} effects");

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
					string.IsNullOrWhiteSpace(option.ResponseText))
				{
					PushDataWarning($"{optionContext} has no response, target node, request reveal, or terminal action.");
				}
			}
		}
	}

	private static void ValidatePotionResponses(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		CustomerInteractionDef interaction)
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

			ValidateEffects(items, rules, response.Effects, $"{responseContext} effects");
		}
	}

	private static void ValidateEffects(
		IReadOnlyDictionary<string, ItemDef> items,
		IReadOnlyDictionary<string, RuleDef> rules,
		IEnumerable<EffectDef> effects,
		string context)
	{
		var effectIndex = 0;
		foreach (var effect in effects)
		{
			effectIndex++;
			var effectContext = $"{context} #{effectIndex}";

			if (!string.IsNullOrWhiteSpace(effect.AddRule))
				ValidateKnownRuleReference(rules, effect.AddRule, $"{effectContext} add rule");

			if (!string.IsNullOrWhiteSpace(effect.AddItemId))
				ValidateKnownItemReference(items, effect.AddItemId, $"{effectContext} add item");

			if (!string.IsNullOrWhiteSpace(effect.ConsumeItemId))
				ValidateKnownItemReference(items, effect.ConsumeItemId, $"{effectContext} consume item");
		}
	}

	private static void ValidateRequirements(
		IReadOnlyDictionary<string, ItemDef> items,
		RequirementsDef? requirements,
		string context)
	{
		if (requirements is null)
			return;

		if (!string.IsNullOrWhiteSpace(requirements.HasItemId))
			ValidateKnownItemReference(items, requirements.HasItemId, $"{context} required item");
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
			PushDataWarning($"{context} should define exactly {PotionRecipeIngredientCount} ingredient amounts when exact grams are used.");
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

			if (portion.Grams <= 0)
			{
				PushDataWarning($"{context} ingredient amount '{portion.IngredientId}' must be greater than 0g.");
				return new List<IngredientPortionDef>();
			}

			if (!normalizedIngredientIds.Any(id => string.Equals(id, portion.IngredientId, StringComparison.OrdinalIgnoreCase)))
			{
				PushDataWarning($"{context} ingredient amount '{portion.IngredientId}' is not listed in ingredientIds.");
				return new List<IngredientPortionDef>();
			}

			portions.Add(new IngredientPortionDef
			{
				IngredientId = portion.IngredientId,
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
			if (ingredientAmount.Grams <= 0)
				PushDataWarning($"{context} for '{ingredientAmount.IngredientId}' must be greater than 0g.");
		}
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
