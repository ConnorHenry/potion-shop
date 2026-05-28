using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Controllers;

public partial class CustomerEventController : Node
{
    private readonly Random _random = new();
    private readonly List<int> _customerOrder = new();
    private int _nextCustomerOrderIndex;

    public void BeginShopDay()
    {
        RebuildCustomerOrder(0);
    }

    public CustomerInteractionDef? DrawCustomerInteraction(DataDb db, GameState state)
    {
        _ = state;

        var interactions = db.CustomerInteractions;
        if (interactions.Count == 0)
            return null;

        if (_customerOrder.Count != interactions.Count || _nextCustomerOrderIndex >= _customerOrder.Count)
            RebuildCustomerOrder(interactions.Count);

        var selectedIndex = _customerOrder[_nextCustomerOrderIndex];
        _nextCustomerOrderIndex += 1;
        var selected = interactions[selectedIndex];

        return selected;
    }

    public CustomerInteractionDef? DrawShopDayCustomerInteraction(DataDb db, GameState state)
    {
        return DrawCustomerInteraction(db, state);
    }

    private void RebuildCustomerOrder(int interactionCount)
    {
        _customerOrder.Clear();

        for (var index = 0; index < interactionCount; index += 1)
            _customerOrder.Add(index);

        for (var index = _customerOrder.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = _random.Next(index + 1);
            (_customerOrder[index], _customerOrder[swapIndex]) = (_customerOrder[swapIndex], _customerOrder[index]);
        }

        _nextCustomerOrderIndex = 0;
    }
}
