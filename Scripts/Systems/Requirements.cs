using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class Requirements
{
    public static bool Met(GameState state, RequirementsDef? req)
    {
        if (req is null) return true;

        if (req.GoldMin is int goldMin && state.Gold < goldMin) return false;
        if (req.DreadMin is int min && state.Dread < min) return false;
        if (req.DreadMax is int max && state.Dread > max) return false;

        if (!string.IsNullOrWhiteSpace(req.HasItemId))
        {
            var qty = req.HasItemQty ?? 1;
            if (!state.HasItem(req.HasItemId!, qty)) return false;
        }

        return true;
    }
}
