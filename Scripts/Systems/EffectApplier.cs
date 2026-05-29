using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class EffectApplier
{
    public static void Apply(GameState state, EffectDef e)
    {
        if (e.AddGold is int g && g != 0) state.AddGold(g);
        if (e.AddDread is int d && d != 0) state.AddDread(d);
        if (!string.IsNullOrWhiteSpace(e.AddRule)) state.AddRule(e.AddRule!);
        if (!string.IsNullOrWhiteSpace(e.AddStoryFlag)) state.AddStoryFlag(e.AddStoryFlag!);
        if (!string.IsNullOrWhiteSpace(e.RemoveStoryFlag)) state.RemoveStoryFlag(e.RemoveStoryFlag!);

        if (!string.IsNullOrWhiteSpace(e.AddItemId))
            state.AddItem(e.AddItemId!, e.AddItemQty ?? 1);

        if (!string.IsNullOrWhiteSpace(e.ConsumeItemId))
            state.ConsumeItem(e.ConsumeItemId!, e.ConsumeItemQty ?? 1);
    }
}
