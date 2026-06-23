namespace OccultShop.Models;

// Keep this as a "flat union" for MVP: one JSON object can set one or more fields.
public sealed class EffectDef
{
    public int? AddGold { get; set; }
    public int? AddDread { get; set; }
    public string? AddRule { get; set; }
    public string? AddStoryFlag { get; set; }
    public string? RemoveStoryFlag { get; set; }

    public string? AddItemId { get; set; }
    public int? AddItemQty { get; set; }
    public string? RestockItemId { get; set; }
    public int? RestockItemQty { get; set; }
    public string? EnableIngredientPreparationMethodId { get; set; }

    public string? ConsumeItemId { get; set; }
    public int? ConsumeItemQty { get; set; }
    public int? ConsumeEachIngredientQty { get; set; }
}
