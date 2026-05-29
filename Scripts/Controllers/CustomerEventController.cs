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
    private readonly Random _random = new();
    private readonly List<string> _customerOrder = new();
    private int _nextCustomerOrderIndex;
    private string _forcedNextCustomerInteractionId = string.Empty;

    public void BeginShopDay()
    {
        _customerOrder.Clear();
        _nextCustomerOrderIndex = 0;
    }

    public void ForceNextCustomerInteraction(string interactionId)
    {
        _forcedNextCustomerInteractionId = string.IsNullOrWhiteSpace(interactionId)
            ? string.Empty
            : interactionId;
    }

    public CustomerInteractionDef? DrawCustomerInteraction(DataDb db, GameState state)
    {
        var interactions = db.CustomerInteractions;
        if (interactions.Count == 0)
            return null;

        if (TryDrawForcedInteraction(interactions, out var forcedInteraction))
            return forcedInteraction;

        var eligibleInteractions = interactions
            .Where(interaction => Requirements.Met(state, interaction.Requires))
            .ToList();
        if (eligibleInteractions.Count == 0)
            return null;

        if (!IsCustomerOrderCurrent(eligibleInteractions) || _nextCustomerOrderIndex >= _customerOrder.Count)
            RebuildCustomerOrder(eligibleInteractions);

        var selectedId = _customerOrder[_nextCustomerOrderIndex];
        _nextCustomerOrderIndex += 1;

        foreach (var candidate in eligibleInteractions)
        {
            if (string.Equals(candidate.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        GD.PushError($"CustomerEventController: Scheduled customer interaction '{selectedId}' was not eligible.");
        return null;
    }

    public CustomerInteractionDef? DrawShopDayCustomerInteraction(DataDb db, GameState state)
    {
        return DrawCustomerInteraction(db, state);
    }

    private bool TryDrawForcedInteraction(
        IReadOnlyList<CustomerInteractionDef> interactions,
        out CustomerInteractionDef? interaction)
    {
        interaction = null;
        if (string.IsNullOrWhiteSpace(_forcedNextCustomerInteractionId))
            return false;

        var forcedInteractionId = _forcedNextCustomerInteractionId;
        _forcedNextCustomerInteractionId = string.Empty;

        foreach (var candidate in interactions)
        {
            if (!string.Equals(candidate.Id, forcedInteractionId, StringComparison.OrdinalIgnoreCase))
                continue;

            interaction = candidate;
            return true;
        }

        GD.PushError($"CustomerEventController: Forced customer interaction '{forcedInteractionId}' was not found.");
        return false;
    }

    private bool IsCustomerOrderCurrent(IReadOnlyList<CustomerInteractionDef> eligibleInteractions)
    {
        if (_customerOrder.Count != eligibleInteractions.Count)
            return false;

        foreach (var interaction in eligibleInteractions)
        {
            if (!_customerOrder.Any(id => string.Equals(id, interaction.Id, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private void RebuildCustomerOrder(IReadOnlyList<CustomerInteractionDef> eligibleInteractions)
    {
        _customerOrder.Clear();

        var remainingInteractions = new List<CustomerInteractionDef>(eligibleInteractions);
        while (remainingInteractions.Count > 0)
        {
            var selectedIndex = PickWeightedIndex(remainingInteractions);
            _customerOrder.Add(remainingInteractions[selectedIndex].Id);
            remainingInteractions.RemoveAt(selectedIndex);
        }

        _nextCustomerOrderIndex = 0;
    }

    private int PickWeightedIndex(IReadOnlyList<CustomerInteractionDef> interactions)
    {
        var totalWeight = 0;
        foreach (var interaction in interactions)
            totalWeight += Math.Max(1, interaction.Weight);

        var roll = _random.Next(totalWeight);
        var accumulatedWeight = 0;
        for (var index = 0; index < interactions.Count; index += 1)
        {
            accumulatedWeight += Math.Max(1, interactions[index].Weight);
            if (roll < accumulatedWeight)
                return index;
        }

        return interactions.Count - 1;
    }
}
