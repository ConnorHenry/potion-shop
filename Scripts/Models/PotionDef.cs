using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class PotionDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Known { get; set; } = true;
    public int Cost { get; set; }
    public string OutputItemId { get; set; } = "";
    public int OutputQty { get; set; } = 1;
    public List<PotionIngredientDef> Ingredients { get; set; } = new();
}

public sealed class PotionIngredientDef
{
    public string ItemId { get; set; } = "";
    public int Qty { get; set; } = 1;
}
