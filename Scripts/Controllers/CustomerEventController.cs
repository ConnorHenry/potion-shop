using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Controllers;

public partial class CustomerEventController : Node
{
    private int _nextCustomerIndex;

    public CustomerInteractionDef? DrawCustomerInteraction(DataDb db, GameState state)
    {
        _ = state;

        var interactions = db.CustomerInteractions;
        if (interactions.Count == 0)
            return null;

        var safeIndex = Math.Clamp(_nextCustomerIndex, 0, interactions.Count - 1);
        var selected = interactions[safeIndex];
        _nextCustomerIndex = (safeIndex + 1) % interactions.Count;

        return selected;
    }

    public CustomerInteractionDef? DrawShopDayCustomerInteraction(DataDb db, GameState state)
    {
        return DrawCustomerInteraction(db, state);
    }
}
