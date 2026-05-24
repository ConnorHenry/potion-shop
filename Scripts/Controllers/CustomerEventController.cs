using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class CustomerEventController : Node
{
    private readonly Random _rng = new();
    private int _generatedCustomerCounter;
    private static readonly string[] PortraitPaths =
    {
        "res://Assets/Characters/store_customer.svg",
        "res://Assets/Characters/chapel_warden.svg"
    };

    public CustomerInteractionDef? DrawCustomerInteraction(DataDb db, GameState state)
    {
        var eligible = db.CustomerInteractions
            .Where(x => Requirements.Met(state, x.Requires))
            .ToList();

        if (eligible.Count == 0) return null;

        return WeightedPick(eligible, x => Math.Max(1, x.Weight));
    }

    public CustomerInteractionDef? DrawShopDayCustomerInteraction(DataDb db, GameState state)
    {
        if (TryBuildGeneratedInteraction(state, out var generated))
            return generated;

        return DrawCustomerInteraction(db, state);
    }

    private bool TryBuildGeneratedInteraction(GameState state, out CustomerInteractionDef interaction)
    {
        interaction = default!;

        var traitWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var riskWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var stack in state.Inventory)
        {
            if (stack.Value <= 0)
                continue;

            if (!ItemCatalog.TryGetItem(stack.Key, out var item))
                continue;

            if (!IsIngredientOrPotion(item))
                continue;

            foreach (var trait in item.Traits)
            {
                if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
                    continue;

                AddWeight(traitWeights, trait.Key, trait.Value);
            }

            foreach (var risk in item.Risks)
            {
                if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
                    continue;

                AddWeight(riskWeights, risk.Key, risk.Value);
            }
        }

        if (traitWeights.Count == 0)
            return false;

        var desiredCount = traitWeights.Count >= 3 ? _rng.Next(2, 4) : Math.Max(1, traitWeights.Count);
        var desiredTraits = PickWeightedTraits(traitWeights, desiredCount);
        if (desiredTraits.Count == 0)
            return false;

        var badTraits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (riskWeights.Count > 0)
        {
            var badCount = riskWeights.Count >= 2 ? _rng.Next(1, 3) : 1;
            badTraits = PickWeightedTraits(riskWeights, Math.Min(badCount, riskWeights.Count));
        }

        var desiredLabels = desiredTraits.Keys.Select(ToDisplayLabel).ToList();
        var badLabels = badTraits.Keys.Select(ToDisplayLabel).ToList();

        interaction = new CustomerInteractionDef
        {
            Id = $"generated_customer_{++_generatedCustomerCounter}",
            Title = "Customer",
            Text = BuildCustomerText(desiredLabels, badLabels),
            CharacterImagePath = PortraitPaths[_rng.Next(PortraitPaths.Length)],
            Requires = new RequirementsDef(),
            Weight = 1,
            DesiredTraits = desiredTraits,
            BadTraits = badTraits
        };

        return true;
    }

    private Dictionary<string, int> PickWeightedTraits(Dictionary<string, int> weights, int count)
    {
        var working = new Dictionary<string, int>(weights, StringComparer.OrdinalIgnoreCase);
        var selected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < count && working.Count > 0; i++)
        {
            var key = WeightedPickKey(working);
            if (string.IsNullOrWhiteSpace(key))
                break;

            var strength = BuildRequestedStrength(working[key]);
            selected[key] = strength;
            working.Remove(key);
        }

        return selected;
    }

    private string? WeightedPickKey(Dictionary<string, int> weights)
    {
        var totalWeight = 0;
        foreach (var pair in weights)
            totalWeight += Math.Max(1, pair.Value);

        if (totalWeight <= 0 || weights.Count == 0)
            return weights.Keys.FirstOrDefault();

        var roll = _rng.Next(0, totalWeight);
        var accumulator = 0;

        foreach (var pair in weights)
        {
            accumulator += Math.Max(1, pair.Value);
            if (roll < accumulator)
                return pair.Key;
        }

        return weights.Keys.FirstOrDefault();
    }

    private int BuildRequestedStrength(int sourceWeight)
    {
        var baseline = Math.Clamp((int)MathF.Round(sourceWeight / 2.0f), 1, 6);
        var variance = _rng.Next(-1, 2);
        return Math.Clamp(baseline + variance, 1, 7);
    }

    private static string BuildCustomerText(IReadOnlyList<string> desiredLabels, IReadOnlyList<string> badLabels)
    {
        var desired = desiredLabels.Count == 0 ? "something useful" : $"something with {FormatList(desiredLabels)}";
        if (badLabels.Count == 0)
            return $"A customer enters and asks for {desired}.";

        return $"A customer enters and asks for {desired}, but wants to avoid {FormatList(badLabels)}.";
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return string.Empty;
        if (values.Count == 1)
            return values[0];
        if (values.Count == 2)
            return $"{values[0]} and {values[1]}";

        return $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}";
    }

    private static string ToDisplayLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var words = key
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return key;

        for (var i = 0; i < words.Length; i++)
        {
            var lower = words[i].ToLowerInvariant();
            words[i] = lower.Length == 1
                ? lower.ToUpperInvariant()
                : char.ToUpperInvariant(lower[0]) + lower[1..];
        }

        return string.Join(" ", words);
    }

    private static bool IsIngredientOrPotion(ItemDef item)
    {
        return item.Tags.Any(tag =>
            string.Equals(tag, "ingredient", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "potion", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddWeight(Dictionary<string, int> weights, string key, int value)
    {
        var clampedValue = Math.Max(1, value);
        if (!weights.TryAdd(key, clampedValue))
            weights[key] += clampedValue;
    }

    private T WeightedPick<T>(IReadOnlyList<T> list, Func<T, int> weight)
    {
        var total = 0;
        for (var i = 0; i < list.Count; i++) total += weight(list[i]);
        var roll = _rng.Next(0, total);
        var acc = 0;
        for (var i = 0; i < list.Count; i++)
        {
            acc += weight(list[i]);
            if (roll < acc) return list[i];
        }
        return list[^1];
    }
}
