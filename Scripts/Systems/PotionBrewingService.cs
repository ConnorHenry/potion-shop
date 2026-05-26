using System;
using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class PotionBrewingService
{
    private const float QualityWeight = 0.25f;
    private const float FitWeight = 0.30f;
    private const float SynergyWeight = 0.25f;
    private const float StabilityWeight = 0.20f;

    public PotionResult BrewPotion(
        List<IngredientDef> ingredients,
        CustomerRequestDef? request,
        List<SynergyRule> synergyRules)
    {
        var result = new PotionResult();

        // 1) Validate ingredients
        var validIngredients = ValidateIngredients(ingredients, result);
        if (validIngredients.Count == 0)
        {
            result.IngredientQualityScore = 0;
            result.EffectFitScore = 0;
            result.SynergyScore = 0;
            result.StabilityScore = 0;
            result.PenaltyScore = 100;
            result.FinalScore = 0.0f;
            result.Grade = "F";
            return result;
        }

        // 2) Combine ingredient traits
        var combinedTraits = CombineTraits(validIngredients);
        result.Traits = combinedTraits;

        // 3) Combine ingredient risks
        var combinedRisks = CombineRisks(validIngredients);
        result.Risks = SelectTopEntriesByValue(combinedRisks, 2);

        // 4) Calculate ingredient quality (Q)
        result.IngredientQualityScore = CalculateIngredientQuality(validIngredients);

        // 5) Apply synergies (Y)
        var synergyEval = ApplySynergies(combinedTraits, combinedRisks, synergyRules, result);
        result.SynergyScore = synergyEval.Score;

        // 6) Calculate effect fit (F)
        result.EffectFitScore = CalculateEffectFit(request, combinedTraits, combinedRisks, result);

        // 7) Calculate stability (T)
        result.StabilityScore = CalculateStability(combinedTraits, combinedRisks, synergyEval.NegativeMagnitude);

        // 8) Calculate penalties (P)
        result.PenaltyScore = CalculatePenalties(combinedRisks, synergyEval.NegativeMagnitude, result.StabilityScore);

        // 9) Calculate final score (S)
        var finalScore =
            (QualityWeight * result.IngredientQualityScore) +
            (FitWeight * result.EffectFitScore) +
            (SynergyWeight * result.SynergyScore) +
            (StabilityWeight * result.StabilityScore) -
            result.PenaltyScore;

        result.FinalScore = MathF.Round(finalScore, 2);

        // 10) Convert final score to grade
        result.Grade = GradeFromScore(result.FinalScore);

        result.Notes.Add(
            $"Q={result.IngredientQualityScore}, F={result.EffectFitScore}, Y={result.SynergyScore}, T={result.StabilityScore}, P={result.PenaltyScore}, S={result.FinalScore}");

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

        return combined;
    }

    private static Dictionary<string, int> SelectTopEntriesByValue(
        Dictionary<string, int> values,
        int maxCount)
    {
        var selected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (values is null || values.Count == 0 || maxCount <= 0)
            return selected;

        foreach (var pair in values
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(maxCount))
        {
            selected[pair.Key] = pair.Value;
        }

        return selected;
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

    private static (int Score, int NegativeMagnitude) ApplySynergies(
        Dictionary<string, int> traits,
        Dictionary<string, int> risks,
        List<SynergyRule> synergyRules,
        PotionResult result)
    {
        if (synergyRules is null || synergyRules.Count == 0)
            return (0, 0);

        var synergyScore = 0;
        var negativeMagnitude = 0;

        foreach (var rule in synergyRules)
        {
            if (rule is null)
                continue;

            if (rule.RequiredTraits.Count == 0 && rule.RequiredRisks.Count == 0)
                continue;

            if (!HasAllRequiredValues(traits, rule.RequiredTraits))
                continue;

            if (!HasAllRequiredValues(risks, rule.RequiredRisks))
                continue;

            result.TriggeredSynergies.Add(rule.Id);
            result.TriggeredSynergyDetails.Add(new TriggeredSynergyDef
            {
                Id = rule.Id,
                RequiredTraits = new List<string>(rule.RequiredTraits),
                RequiredRisks = new List<string>(rule.RequiredRisks),
                ContributingTraits = BuildContributingTraits(traits, rule.RequiredTraits),
                ContributingRisks = BuildContributingTraits(risks, rule.RequiredRisks),
                Modifier = rule.Modifier,
                Description = rule.Description
            });

            if (!string.IsNullOrWhiteSpace(rule.Description))
                result.Notes.Add(rule.Description);

            synergyScore += rule.Modifier;
            if (rule.Modifier < 0)
                negativeMagnitude += Math.Abs(rule.Modifier);

            if (!string.IsNullOrWhiteSpace(rule.ResultTrait))
            {
                var strength = CalculateSynergyTraitStrength(traits, rule.RequiredTraits);
                AddValue(traits, rule.ResultTrait, strength);
            }

            if (!string.IsNullOrWhiteSpace(rule.AddedRisk) && rule.AddedRiskStrength > 0)
                AddValue(risks, rule.AddedRisk, rule.AddedRiskStrength);
        }

        return (ClampRange(synergyScore, -100, 100), negativeMagnitude);
    }

    private static Dictionary<string, int> BuildContributingTraits(
        Dictionary<string, int> traits,
        List<string> requiredTraits)
    {
        var contributing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var trait in requiredTraits)
        {
            if (string.IsNullOrWhiteSpace(trait))
                continue;

            if (!traits.TryGetValue(trait, out var value))
                continue;

            contributing[trait] = value;
        }

        return contributing;
    }

    private static bool HasAllRequiredValues(Dictionary<string, int> values, List<string> requiredKeys)
    {
        foreach (var key in requiredKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (!values.TryGetValue(key, out var strength) || strength <= 0)
                return false;
        }

        return true;
    }

    private static int CalculateSynergyTraitStrength(Dictionary<string, int> traits, List<string> requiredTraits)
    {
        if (requiredTraits.Count == 0)
            return 1;

        var sum = 0;
        foreach (var trait in requiredTraits)
        {
            if (!traits.TryGetValue(trait, out var value))
                value = 1;

            sum += Math.Max(1, value);
        }

        var average = sum / (float)requiredTraits.Count;
        return Math.Max(1, (int)MathF.Round(average * 0.5f));
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
                if (desired.Value <= 0)
                    continue;

                desiredWeight += desired.Value;
                traits.TryGetValue(desired.Key, out var producedStrength);
                var ratio = ClampUnit(producedStrength / (float)desired.Value);
                desiredMatch += ratio * desired.Value;
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
                if (bad.Value <= 0)
                    continue;

                badWeight += bad.Value;

                traits.TryGetValue(bad.Key, out var badTraitStrength);
                risks.TryGetValue(bad.Key, out var badRiskStrength);

                var producedBadStrength = Math.Max(0, badTraitStrength) + Math.Max(0, badRiskStrength);
                var ratio = ClampUnit(producedBadStrength / (float)bad.Value);
                badMatch += ratio * bad.Value;
            }

            badScore = badWeight <= 0.0f ? 0.0f : (badMatch / badWeight) * 100.0f;
        }

        var fitScore = desiredScore - badScore;
        return Clamp01Score((int)MathF.Round(fitScore));
    }

    private static int CalculateStability(
        Dictionary<string, int> traits,
        Dictionary<string, int> risks,
        int negativeSynergyMagnitude)
    {
        var riskLoad = SumPositiveValues(risks);
        var diversityBonus = Math.Min(10, traits.Count * 2);

        var stability = 100.0f - (riskLoad * 4.0f) - (negativeSynergyMagnitude * 0.5f) + diversityBonus;
        return Clamp01Score((int)MathF.Round(stability));
    }

    private static int CalculatePenalties(
        Dictionary<string, int> risks,
        int negativeSynergyMagnitude,
        int stabilityScore)
    {
        var riskLoad = SumPositiveValues(risks);

        var riskPenalty = riskLoad * 0.6f;
        var synergyPenalty = negativeSynergyMagnitude * 0.3f;
        var instabilityPenalty = Math.Max(0.0f, 50.0f - stabilityScore) * 0.2f;

        var totalPenalty = riskPenalty + synergyPenalty + instabilityPenalty;
        return Clamp01Score((int)MathF.Round(totalPenalty));
    }

    private static int SumPositiveValues(Dictionary<string, int> values)
    {
        var total = 0;
        foreach (var pair in values)
            total += Math.Max(0, pair.Value);

        return total;
    }

    private static void AddValue(Dictionary<string, int> values, string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key) || value == 0)
            return;

        if (!values.TryAdd(key, value))
            values[key] += value;
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
}
