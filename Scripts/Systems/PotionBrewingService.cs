using System;
using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class PotionBrewingService
{
    private const float QualityWeight = 0.30f;
    private const float FitWeight = 0.45f;
    private const float StabilityWeight = 0.25f;
    private const int MaxRiskChanceValue = 10;
    private const int RiskPresenceValue = 1;

    private readonly Func<float> _nextRiskRoll;

    public PotionBrewingService()
        : this(() => Random.Shared.NextSingle())
    {
    }

    public PotionBrewingService(Func<float> nextRiskRoll)
    {
        _nextRiskRoll = nextRiskRoll ?? (() => Random.Shared.NextSingle());
    }

    public PotionResult BrewPotion(
        List<IngredientDef> ingredients,
        CustomerRequestDef? request)
    {
        return BrewPotionInternal(ingredients, request, rollRisks: true);
    }

    public PotionResult PreviewPotion(
        List<IngredientDef> ingredients,
        CustomerRequestDef? request)
    {
        return BrewPotionInternal(ingredients, request, rollRisks: false);
    }

    public PotionResult EvaluatePotionItem(ItemDef potionItem, CustomerRequestDef? request)
    {
        var result = new PotionResult();
        if (potionItem is null || string.IsNullOrWhiteSpace(potionItem.Id))
        {
            result.Notes.Add("No valid potion item was provided.");
            result.IngredientQualityScore = 0;
            result.EffectFitScore = 0;
            result.StabilityScore = 0;
            result.PenaltyScore = 100;
            result.FinalScore = 0.0f;
            result.Grade = "F";
            return result;
        }

        result.Traits = new Dictionary<string, int>(potionItem.Traits, StringComparer.OrdinalIgnoreCase);
        result.Risks = NormalizeCarriedRisks(potionItem.Risks);
        result.PossibleRisks = new Dictionary<string, int>(result.Risks, StringComparer.OrdinalIgnoreCase);
        result.IngredientQualityScore = Clamp01Score(potionItem.Quality);
        result.EffectFitScore = CalculateEffectFit(request, result.Traits, result.Risks, result);
        result.StabilityScore = CalculateStability(result.Traits, result.Risks);
        result.PenaltyScore = CalculatePenalties(result.Risks, result.StabilityScore);

        var finalScore =
            (QualityWeight * result.IngredientQualityScore) +
            (FitWeight * result.EffectFitScore) +
            (StabilityWeight * result.StabilityScore) -
            result.PenaltyScore;

        result.FinalScore = MathF.Round(finalScore, 2);
        result.Grade = GradeFromScore(result.FinalScore);
        result.Notes.Add(
            $"Q={result.IngredientQualityScore}, F={result.EffectFitScore}, T={result.StabilityScore}, P={result.PenaltyScore}, S={result.FinalScore}");

        return result;
    }

    private PotionResult BrewPotionInternal(
        List<IngredientDef> ingredients,
        CustomerRequestDef? request,
        bool rollRisks)
    {
        var result = new PotionResult();

        // 1) Validate ingredients
        var validIngredients = ValidateIngredients(ingredients, result);
        if (validIngredients.Count == 0)
        {
            result.IngredientQualityScore = 0;
            result.EffectFitScore = 0;
            result.StabilityScore = 0;
            result.PenaltyScore = 100;
            result.FinalScore = 0.0f;
            result.Grade = "F";
            return result;
        }

        // 2) Combine ingredient traits
        var combinedTraits = CombineTraits(validIngredients);
        result.Traits = combinedTraits;

		// 3) Combine ingredient risks as chance values, then apply visible ingredient effects
		var possibleRisks = CombineRisks(validIngredients);
		ApplyPreRiskIngredientEffects(validIngredients, combinedTraits, possibleRisks, result);
		ForceFailedBoilingRiskChances(validIngredients, possibleRisks);

		// 4) Roll actual carried risks, then apply conditional ingredient effects
		var carriedRisks = rollRisks
			? RollCarriedRisks(possibleRisks)
			: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (rollRisks)
			ForceFailedBoilingCarriedRisks(validIngredients, carriedRisks);
		ApplyPostRiskIngredientEffects(validIngredients, combinedTraits, carriedRisks, possibleRisks, result, rollRisks);
		if (rollRisks)
			ForceFailedBoilingCarriedRisks(validIngredients, carriedRisks);
		result.RiskIngredientPricePenalty = CalculateRiskIngredientPricePenalty(validIngredients, carriedRisks);

        // 5) Calculate ingredient quality (Q)
        result.IngredientQualityScore = CalculateIngredientQuality(validIngredients);

        result.Risks = carriedRisks;
        result.PossibleRisks = possibleRisks;

        // 6) Calculate effect fit (F)
        result.EffectFitScore = CalculateEffectFit(request, combinedTraits, carriedRisks, result);

        // 7) Calculate stability (T)
        result.StabilityScore = CalculateStability(combinedTraits, carriedRisks);

        // 8) Calculate penalties (P)
        result.PenaltyScore = CalculatePenalties(carriedRisks, result.StabilityScore);

        // 9) Calculate final score (S)
        var finalScore =
            (QualityWeight * result.IngredientQualityScore) +
            (FitWeight * result.EffectFitScore) +
            (StabilityWeight * result.StabilityScore) -
            result.PenaltyScore;

        result.FinalScore = MathF.Round(finalScore, 2);

        // 10) Convert final score to grade
        result.Grade = GradeFromScore(result.FinalScore);

        result.Notes.Add(
            $"Q={result.IngredientQualityScore}, F={result.EffectFitScore}, T={result.StabilityScore}, P={result.PenaltyScore}, S={result.FinalScore}");

        return result;
    }

    private static List<IngredientDef> ValidateIngredients(List<IngredientDef> ingredients, PotionResult result)
    {
        var valid = new List<IngredientDef>();
        if (ingredients is null)
        {
            result.Notes.Add("No ingredients were provided.");
            return valid;
        }

        foreach (var ingredient in ingredients)
        {
            if (ingredient is null)
                continue;

            if (string.IsNullOrWhiteSpace(ingredient.Id))
                continue;

            valid.Add(ingredient);
        }

        if (valid.Count == 0)
            result.Notes.Add("No valid ingredients were provided.");

        return valid;
    }

    private static Dictionary<string, int> CombineTraits(List<IngredientDef> ingredients)
    {
        var combined = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var ingredient in ingredients)
        {
            foreach (var trait in ingredient.Traits)
            {
                if (string.IsNullOrWhiteSpace(trait.Key))
                    continue;

                if (!combined.TryAdd(trait.Key, trait.Value))
                    combined[trait.Key] += trait.Value;
            }
        }

        return combined;
    }

	private static Dictionary<string, int> CombineRisks(List<IngredientDef> ingredients)
	{
		var combined = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var ingredient in ingredients)
        {
            foreach (var risk in ingredient.Risks)
            {
                if (string.IsNullOrWhiteSpace(risk.Key))
                    continue;

                if (!combined.TryAdd(risk.Key, risk.Value))
                    combined[risk.Key] += risk.Value;
            }
        }

		ClampRiskChances(combined);
		return combined;
	}

	private static void ForceFailedBoilingRiskChances(
		List<IngredientDef> ingredients,
		Dictionary<string, int> possibleRisks)
	{
		foreach (var ingredient in ingredients)
		{
			if (!IsFailedBoilingIngredient(ingredient))
				continue;

			foreach (var risk in ingredient.Risks)
			{
				if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
					continue;

				possibleRisks[risk.Key] = MaxRiskChanceValue;
			}
		}
	}

	private static void ForceFailedBoilingCarriedRisks(
		List<IngredientDef> ingredients,
		Dictionary<string, int> carriedRisks)
	{
		foreach (var ingredient in ingredients)
		{
			if (!IsFailedBoilingIngredient(ingredient))
				continue;

			foreach (var risk in ingredient.Risks)
			{
				if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
					continue;

				carriedRisks[risk.Key] = RiskPresenceValue;
			}
		}
	}

	private static bool IsFailedBoilingIngredient(IngredientDef ingredient)
	{
		return ingredient.Tags is not null &&
			ingredient.Tags.Any(tag => string.Equals(tag, ItemTags.FailedBoiling, StringComparison.OrdinalIgnoreCase));
	}

    private static void ApplyPreRiskIngredientEffects(
        List<IngredientDef> ingredients,
        Dictionary<string, int> traits,
        Dictionary<string, int> possibleRisks,
        PotionResult result)
    {
        foreach (var entry in BuildOrderedIngredientEffectEntries(ingredients))
        {
            var ingredient = entry.Ingredient;
            var effect = entry.Effect;

            switch (effect.Kind)
            {
                case IngredientEffectDef.BoostLowestOtherTraitKind:
                    ApplyBoostLowestOtherTrait(ingredients, ingredient, effect, traits, result);
                    break;
                case IngredientEffectDef.BoostStrongestTraitAddRiskKind:
                    ApplyBoostStrongestTraitAddRisk(ingredient, effect, traits, possibleRisks, result);
                    break;
                case IngredientEffectDef.CopyStrongestOtherTraitKind:
                    ApplyCopyStrongestOtherTrait(ingredients, ingredient, effect, traits, result);
                    break;
                case IngredientEffectDef.HalveOtherRisksKind:
                    ApplyHalveOtherRisks(ingredients, ingredient, effect, possibleRisks, result);
                    break;
                case IngredientEffectDef.ReduceHighestRiskKind:
                    ApplyReduceHighestRisk(ingredient, effect, possibleRisks, result);
                    break;
                case IngredientEffectDef.TemperTraitsKind:
                    ApplyTemperTraits(ingredient, effect, traits, result);
                    break;
            }
        }
    }

    private static void ApplyPostRiskIngredientEffects(
        List<IngredientDef> ingredients,
        Dictionary<string, int> traits,
        Dictionary<string, int> carriedRisks,
        Dictionary<string, int> possibleRisks,
        PotionResult result,
        bool rollRisks)
    {
        foreach (var entry in BuildOrderedIngredientEffectEntries(ingredients))
        {
            var ingredient = entry.Ingredient;
            var effect = entry.Effect;

            switch (effect.Kind)
            {
                case IngredientEffectDef.BoostLowestTraitIfNoRiskCarriesKind:
                    ApplyBoostLowestTraitIfNoRiskCarries(ingredient, effect, traits, carriedRisks, possibleRisks, result, rollRisks);
                    break;
                case IngredientEffectDef.SuppressSingleCarriedRiskKind:
                    ApplySuppressSingleCarriedRisk(ingredient, effect, carriedRisks, result, rollRisks);
                    break;
                case IngredientEffectDef.AddTraitIfRiskCarriesKind:
                    ApplyAddTraitIfRiskCarries(ingredient, effect, traits, carriedRisks, result, rollRisks);
                    break;
            }
        }
    }

    private static List<IngredientEffectEntry> BuildOrderedIngredientEffectEntries(List<IngredientDef> ingredients)
    {
        var entries = new List<IngredientEffectEntry>();
        foreach (var ingredient in ingredients)
        {
            if (ingredient.IngredientEffects is null || ingredient.IngredientEffects.Count == 0)
                continue;

            foreach (var effect in ingredient.IngredientEffects)
            {
                if (effect is null || string.IsNullOrWhiteSpace(effect.Kind))
                    continue;

                entries.Add(new IngredientEffectEntry(ingredient, effect));
            }
        }

        entries.Sort(CompareIngredientEffectEntries);
        return entries;
    }

    private static int CompareIngredientEffectEntries(IngredientEffectEntry left, IngredientEffectEntry right)
    {
        var ingredientComparison = string.Compare(left.Ingredient.Id, right.Ingredient.Id, StringComparison.OrdinalIgnoreCase);
        if (ingredientComparison != 0)
            return ingredientComparison;

        var kindComparison = string.Compare(left.Effect.Kind, right.Effect.Kind, StringComparison.OrdinalIgnoreCase);
        if (kindComparison != 0)
            return kindComparison;

        return string.Compare(left.Effect.Name, right.Effect.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBoostLowestOtherTrait(
        List<IngredientDef> ingredients,
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        PotionResult result)
    {
        var otherTraits = CombineTraitsExcept(ingredients, source);
        if (!TryGetLowestPositiveValue(otherTraits, out var selectedTrait))
            return;

        var amount = GetEffectAmount(effect, 1);
        AddValue(traits, selectedTrait.Key, amount);
        RecordTriggeredIngredientEffect(source, effect, result, $"{DisplayStatName(selectedTrait.Key)} +{amount}");
    }

    private static void ApplyBoostStrongestTraitAddRisk(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        Dictionary<string, int> possibleRisks,
        PotionResult result)
    {
        if (!TryGetHighestPositiveValue(traits, out var selectedTrait))
            return;

        var amount = GetEffectAmount(effect, 1);
        AddValue(traits, selectedTrait.Key, amount);

        var riskAmount = effect.SecondaryAmount > 0 ? effect.SecondaryAmount : amount;
        if (!string.IsNullOrWhiteSpace(effect.RiskId) && riskAmount > 0)
            AddRiskChance(possibleRisks, effect.RiskId, riskAmount);

        var resultText = string.IsNullOrWhiteSpace(effect.RiskId) || riskAmount <= 0
            ? $"{DisplayStatName(selectedTrait.Key)} +{amount}"
            : $"{DisplayStatName(selectedTrait.Key)} +{amount}, {DisplayStatName(effect.RiskId)} risk +{riskAmount}";
        RecordTriggeredIngredientEffect(source, effect, result, resultText);
    }

    private static void ApplyCopyStrongestOtherTrait(
        List<IngredientDef> ingredients,
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        PotionResult result)
    {
        var otherTraits = CombineTraitsExcept(ingredients, source);
        if (!TryGetHighestPositiveValue(otherTraits, out var selectedTrait))
            return;

        var amount = GetEffectAmount(effect, Math.Max(1, (int)MathF.Ceiling(selectedTrait.Value * 0.5f)));
        AddValue(traits, selectedTrait.Key, amount);
        RecordTriggeredIngredientEffect(source, effect, result, $"{DisplayStatName(selectedTrait.Key)} +{amount}");
    }

    private static void ApplyHalveOtherRisks(
        List<IngredientDef> ingredients,
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> possibleRisks,
        PotionResult result)
    {
        var otherRisks = CombineRisksExcept(ingredients, source);
        var totalReduction = 0;
        foreach (var risk in otherRisks.OrderBy(x => x.Key))
        {
            var halvedRisk = (risk.Value + 1) / 2;
            var reduction = Math.Max(0, risk.Value - halvedRisk);
            if (reduction <= 0)
                continue;

            totalReduction += ReduceRiskChance(possibleRisks, risk.Key, reduction);
        }

        if (totalReduction <= 0)
            return;

        RecordTriggeredIngredientEffect(source, effect, result, $"Other risk chances -{totalReduction}");
    }

    private static void ApplyReduceHighestRisk(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> possibleRisks,
        PotionResult result)
    {
        if (!TryGetHighestPositiveValue(possibleRisks, out var selectedRisk))
            return;

        var amount = GetEffectAmount(effect, 1);
        var actualReduction = ReduceRiskChance(possibleRisks, selectedRisk.Key, amount);
        if (actualReduction <= 0)
            return;

        RecordTriggeredIngredientEffect(source, effect, result, $"{DisplayStatName(selectedRisk.Key)} risk -{actualReduction}");
    }

    private static void ApplyTemperTraits(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        PotionResult result)
    {
        if (!TryGetHighestPositiveValue(traits, out var highestTrait))
            return;
        if (!TryGetLowestPositiveValue(traits, out var lowestTrait))
            return;
        if (string.Equals(highestTrait.Key, lowestTrait.Key, StringComparison.OrdinalIgnoreCase))
            return;

        var amount = GetEffectAmount(effect, 1);
        var reduction = ReduceTraitToMinimumOne(traits, highestTrait.Key, amount);
        AddValue(traits, lowestTrait.Key, amount);
        RecordTriggeredIngredientEffect(
            source,
            effect,
            result,
            $"{DisplayStatName(lowestTrait.Key)} +{amount}, {DisplayStatName(highestTrait.Key)} -{reduction}");
    }

    private static void ApplyBoostLowestTraitIfNoRiskCarries(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        Dictionary<string, int> carriedRisks,
        Dictionary<string, int> possibleRisks,
        PotionResult result,
        bool rollRisks)
    {
        if (carriedRisks.Count > 0)
            return;
        if (!rollRisks && possibleRisks.Count > 0)
            return;
        if (!TryGetLowestPositiveValue(traits, out var selectedTrait))
            return;

        var amount = GetEffectAmount(effect, 1);
        AddValue(traits, selectedTrait.Key, amount);
        RecordTriggeredIngredientEffect(source, effect, result, $"{DisplayStatName(selectedTrait.Key)} +{amount}; no risk carried");
    }

    private static void ApplySuppressSingleCarriedRisk(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> carriedRisks,
        PotionResult result,
        bool rollRisks)
    {
        if (!rollRisks || carriedRisks.Count != 1)
            return;

        var selectedRisk = carriedRisks
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .OrderBy(x => x.Key)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(selectedRisk.Key))
            return;

        carriedRisks.Remove(selectedRisk.Key);
        RecordTriggeredIngredientEffect(source, effect, result, $"Suppressed {DisplayStatName(selectedRisk.Key)}");
    }

    private static void ApplyAddTraitIfRiskCarries(
        IngredientDef source,
        IngredientEffectDef effect,
        Dictionary<string, int> traits,
        Dictionary<string, int> carriedRisks,
        PotionResult result,
        bool rollRisks)
    {
        if (!rollRisks || carriedRisks.Count == 0 || string.IsNullOrWhiteSpace(effect.TraitId))
            return;

        var amount = GetEffectAmount(effect, 1);
        AddValue(traits, effect.TraitId, amount);
        RecordTriggeredIngredientEffect(source, effect, result, $"{DisplayStatName(effect.TraitId)} +{amount}; risk carried");
    }

    private static Dictionary<string, int> CombineTraitsExcept(List<IngredientDef> ingredients, IngredientDef excludedIngredient)
    {
        var combined = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ingredient in ingredients)
        {
            if (ReferenceEquals(ingredient, excludedIngredient))
                continue;

            foreach (var trait in ingredient.Traits)
            {
                if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
                    continue;

                AddValue(combined, trait.Key, trait.Value);
            }
        }

        return combined;
    }

    private static Dictionary<string, int> CombineRisksExcept(List<IngredientDef> ingredients, IngredientDef excludedIngredient)
    {
        var combined = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ingredient in ingredients)
        {
            if (ReferenceEquals(ingredient, excludedIngredient))
                continue;

            foreach (var risk in ingredient.Risks)
            {
                if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
                    continue;

                AddRiskChance(combined, risk.Key, risk.Value);
            }
        }

        return combined;
    }

    private static bool TryGetHighestPositiveValue(
        Dictionary<string, int> values,
        out KeyValuePair<string, int> selected)
    {
        selected = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(selected.Key) && selected.Value > 0;
    }

    private static bool TryGetLowestPositiveValue(
        Dictionary<string, int> values,
        out KeyValuePair<string, int> selected)
    {
        selected = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .OrderBy(x => x.Value)
            .ThenBy(x => x.Key)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(selected.Key) && selected.Value > 0;
    }

    private static int GetEffectAmount(IngredientEffectDef effect, int fallback)
    {
        return effect.Amount > 0 ? effect.Amount : Math.Max(1, fallback);
    }

    private static int ReduceRiskChance(Dictionary<string, int> values, string key, int amount)
    {
        if (string.IsNullOrWhiteSpace(key) || amount <= 0)
            return 0;
        if (!values.TryGetValue(key, out var currentValue) || currentValue <= 0)
            return 0;

        var newValue = Math.Max(0, currentValue - amount);
        var actualReduction = currentValue - newValue;
        if (newValue <= 0)
            values.Remove(key);
        else
            values[key] = newValue;

        return actualReduction;
    }

    private static int ReduceTraitToMinimumOne(Dictionary<string, int> values, string key, int amount)
    {
        if (string.IsNullOrWhiteSpace(key) || amount <= 0)
            return 0;
        if (!values.TryGetValue(key, out var currentValue) || currentValue <= 1)
            return 0;

        var newValue = Math.Max(1, currentValue - amount);
        var actualReduction = currentValue - newValue;
        values[key] = newValue;
        return actualReduction;
    }

    private static void RecordTriggeredIngredientEffect(
        IngredientDef source,
        IngredientEffectDef effect,
        PotionResult result,
        string resultText)
    {
        var effectName = !string.IsNullOrWhiteSpace(effect.Name)
            ? effect.Name
            : !string.IsNullOrWhiteSpace(effect.Family)
                ? effect.Family
                : effect.Kind;

        result.TriggeredIngredientEffects.Add(new TriggeredIngredientEffectDef
        {
            IngredientId = source.Id,
            IngredientName = source.Name,
            EffectName = effectName,
            Description = effect.Description,
            ResultText = resultText
        });

        result.Notes.Add($"{source.Name}: {effectName} ({resultText})");
    }

    private Dictionary<string, int> RollCarriedRisks(Dictionary<string, int> possibleRisks)
    {
        var carriedRisks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (possibleRisks is null || possibleRisks.Count == 0)
            return carriedRisks;

        foreach (var pair in possibleRisks.OrderBy(x => x.Key))
        {
            if (DoesRiskCarry(pair.Value))
                carriedRisks[pair.Key] = RiskPresenceValue;
        }

        return carriedRisks;
    }

    private bool DoesRiskCarry(int chanceValue)
    {
        var clampedChanceValue = ClampRange(chanceValue, 0, MaxRiskChanceValue);
        if (clampedChanceValue <= 0)
            return false;
        if (clampedChanceValue >= MaxRiskChanceValue)
            return true;

        return ClampUnit(_nextRiskRoll()) < clampedChanceValue / (float)MaxRiskChanceValue;
    }

    private static int CalculateIngredientQuality(List<IngredientDef> ingredients)
    {
        if (ingredients.Count == 0)
            return 0;

        var total = 0.0f;
        foreach (var ingredient in ingredients)
            total += Clamp01Score(ingredient.Quality);

        return (int)MathF.Round(total / ingredients.Count);
    }

    private static int CalculateEffectFit(
        CustomerRequestDef? request,
        Dictionary<string, int> traits,
        Dictionary<string, int> risks,
        PotionResult result)
    {
        if (request is null)
        {
            result.Notes.Add("No customer request supplied. Effect fit defaulted to 50.");
            return 50;
        }

        var desiredScore = 100.0f;
        if (request.DesiredTraits.Count > 0)
        {
            var desiredWeight = 0.0f;
            var desiredMatch = 0.0f;

            foreach (var desired in request.DesiredTraits)
            {
                var desiredWeightForTrait = GetTraitRangeWeight(desired.Value);
                if (desiredWeightForTrait <= 0)
                    continue;

                desiredWeight += desiredWeightForTrait;
                traits.TryGetValue(desired.Key, out var producedStrength);
                desiredMatch += CalculateDesiredRangeFit(producedStrength, desired.Value) * desiredWeightForTrait;
            }

            desiredScore = desiredWeight <= 0.0f ? 100.0f : (desiredMatch / desiredWeight) * 100.0f;
        }

        var badScore = 0.0f;
        if (request.BadTraits.Count > 0)
        {
            var badWeight = 0.0f;
            var badMatch = 0.0f;

            foreach (var bad in request.BadTraits)
            {
                var badWeightForTrait = GetTraitRangeWeight(bad.Value);
                if (badWeightForTrait <= 0)
                    continue;

                badWeight += badWeightForTrait;

                traits.TryGetValue(bad.Key, out var badTraitStrength);
                risks.TryGetValue(bad.Key, out var badRiskStrength);

                var producedBadStrength = Math.Max(0, badTraitStrength) + Math.Max(0, badRiskStrength);
                badMatch += CalculateBadRangeViolation(producedBadStrength, bad.Value) * badWeightForTrait;
            }

            badScore = badWeight <= 0.0f ? 0.0f : (badMatch / badWeight) * 100.0f;
        }

        var fitScore = desiredScore - badScore;
        return Clamp01Score((int)MathF.Round(fitScore));
    }

    private static float CalculateDesiredRangeFit(int producedStrength, CustomerTraitRangeDef? range)
    {
        if (range is null)
            return 1.0f;

        var produced = Math.Max(0, producedStrength);
        if (range.Min is int min && produced < min)
            return min <= 0 ? 1.0f : ClampUnit(produced / (float)min);

        if (range.Max is int max && produced > max)
            return max <= 0 ? 0.0f : ClampUnit(max / (float)produced);

        return 1.0f;
    }

    private static float CalculateBadRangeViolation(int producedStrength, CustomerTraitRangeDef? range)
    {
        if (range is null)
            return 0.0f;

        var produced = Math.Max(0, producedStrength);
        if (range.Min is int min && produced < min)
            return min <= 0 ? 0.0f : ClampUnit((min - produced) / (float)min);

        if (range.Max is int max && produced > max)
            return max <= 0 ? 1.0f : ClampUnit((produced - max) / (float)max);

        return 0.0f;
    }

    private static int GetTraitRangeWeight(CustomerTraitRangeDef? range)
    {
        if (range is null)
            return 0;

        if (range.Min is int min && min > 0)
            return min;

        if (range.Max is int max)
            return Math.Max(1, max);

        return 0;
    }

    private static int CalculateStability(
        Dictionary<string, int> traits,
        Dictionary<string, int> risks)
    {
        var riskLoad = SumPositiveValues(risks);
        var diversityBonus = Math.Min(10, traits.Count * 2);

        var stability = 100.0f - (riskLoad * 4.0f) + diversityBonus;
        return Clamp01Score((int)MathF.Round(stability));
    }

    private static int CalculatePenalties(
        Dictionary<string, int> risks,
        int stabilityScore)
    {
        var riskLoad = SumPositiveValues(risks);

        var riskPenalty = riskLoad * 0.6f;
        var instabilityPenalty = Math.Max(0.0f, 50.0f - stabilityScore) * 0.2f;

        var totalPenalty = riskPenalty + instabilityPenalty;
        return Clamp01Score((int)MathF.Round(totalPenalty));
    }

    private static int SumPositiveValues(Dictionary<string, int> values)
    {
        var total = 0;
        foreach (var pair in values)
            total += Math.Max(0, pair.Value);

        return total;
    }

    private static int CalculateRiskIngredientPricePenalty(
        List<IngredientDef> ingredients,
        Dictionary<string, int> carriedRisks)
    {
        if (ingredients.Count == 0 || carriedRisks.Count == 0)
            return 0;

        var penalty = 0;
        foreach (var ingredient in ingredients)
        {
            if (ingredient.Risks is null || ingredient.Risks.Count == 0)
                continue;

            foreach (var risk in ingredient.Risks)
            {
                if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
                    continue;

                if (!carriedRisks.TryGetValue(risk.Key, out var carriedValue) || carriedValue <= 0)
                    continue;

                penalty += Math.Max(0, ingredient.BasePrice);
                break;
            }
        }

        return Math.Max(0, penalty);
    }

    private static void AddValue(Dictionary<string, int> values, string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key) || value == 0)
            return;

        if (!values.TryAdd(key, value))
            values[key] += value;
    }

    private static void AddRiskChance(Dictionary<string, int> values, string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key) || value <= 0)
            return;

        if (!values.TryAdd(key, value))
            values[key] += value;

        values[key] = ClampRange(values[key], 0, MaxRiskChanceValue);
    }

    private static void ClampRiskChances(Dictionary<string, int> values)
    {
        var keysToRemove = new List<string>();
        var keys = values.Keys.ToList();
        foreach (var key in keys)
        {
            var value = values[key];
            if (string.IsNullOrWhiteSpace(key) || value <= 0)
            {
                keysToRemove.Add(key);
                continue;
            }

            values[key] = ClampRange(value, 0, MaxRiskChanceValue);
        }

        foreach (var key in keysToRemove)
            values.Remove(key);
    }

    private static Dictionary<string, int> NormalizeCarriedRisks(Dictionary<string, int>? risks)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (risks is null)
            return normalized;

        foreach (var pair in risks)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                continue;

            normalized[pair.Key] = RiskPresenceValue;
        }

        return normalized;
    }

    private static int Clamp01Score(int score)
    {
        return ClampRange(score, 0, 100);
    }

    private static int ClampRange(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static float ClampUnit(float value)
    {
        if (value < 0.0f)
            return 0.0f;
        if (value > 1.0f)
            return 1.0f;
        return value;
    }

    private static string GradeFromScore(float score)
    {
        if (score >= 95.0f) return "A+";
        if (score >= 90.0f) return "A";
        if (score >= 85.0f) return "A-";
        if (score >= 80.0f) return "B+";
        if (score >= 75.0f) return "B";
        if (score >= 70.0f) return "B-";
        if (score >= 65.0f) return "C+";
        if (score >= 60.0f) return "C";
        if (score >= 55.0f) return "C-";
        if (score >= 50.0f) return "D";
        return "F";
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

    private readonly record struct IngredientEffectEntry(IngredientDef Ingredient, IngredientEffectDef Effect);
}
