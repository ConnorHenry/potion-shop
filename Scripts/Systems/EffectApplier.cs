using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public static class EffectApplier
{
    public static void Apply(GameState state, EffectDef e)
    {
        if (e.AddGold is int g && g != 0) state.AddGold(g);
        if (e.AddDread is int d && d != 0) state.AddDread(d);
        if (e.SetReputation is int setReputation) state.SetOverallReputation(setReputation);
        if (e.AddReputation is int addReputation && addReputation != 0) state.AddReputation(addReputation);
        if (!string.IsNullOrWhiteSpace(e.AddRule)) state.AddRule(e.AddRule!);
        if (!string.IsNullOrWhiteSpace(e.AddStoryFlag)) state.AddStoryFlag(e.AddStoryFlag!);
        if (!string.IsNullOrWhiteSpace(e.RemoveStoryFlag)) state.RemoveStoryFlag(e.RemoveStoryFlag!);
        if (!string.IsNullOrWhiteSpace(e.QuestId) && !string.IsNullOrWhiteSpace(e.SetQuestStatus))
            state.SetQuestStatus(e.QuestId!, e.SetQuestStatus);
        if (!string.IsNullOrWhiteSpace(e.RelationshipCharacterId))
        {
            if (e.SetRelationship is int setRelationship)
                state.SetRelationshipScore(e.RelationshipCharacterId!, setRelationship);
            if (e.AddRelationship is int addRelationship && addRelationship != 0)
                state.AddRelationship(e.RelationshipCharacterId!, addRelationship);
        }

        if (!string.IsNullOrWhiteSpace(e.AddItemId))
            state.AddItem(e.AddItemId!, e.AddItemQty ?? 1);

        if (!string.IsNullOrWhiteSpace(e.RestockItemId))
            state.RestockItemToMinimum(e.RestockItemId!, e.RestockItemQty ?? 1);

        if (!string.IsNullOrWhiteSpace(e.EnableIngredientPreparationMethodId))
        {
            state.SetIngredientPreparationMethodEnabled(e.EnableIngredientPreparationMethodId!, true);
            state.UnlockIngredientPreparationForCurrentInventory(e.EnableIngredientPreparationMethodId!);
        }

        if (!string.IsNullOrWhiteSpace(e.ConsumeItemId))
            state.ConsumeItem(e.ConsumeItemId!, e.ConsumeItemQty ?? 1);

        if (e.ConsumeEachIngredientQty is int ingredientQty)
            state.ConsumeEachIngredient(ingredientQty);
    }
}
