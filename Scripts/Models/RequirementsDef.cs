namespace OccultShop.Models;

public sealed class RequirementsDef
{
    public int? GoldMin { get; set; }
    public int? DreadMin { get; set; }
    public int? DreadMax { get; set; }

    // Simple inventory gate (extend later)
    public string? HasItemId { get; set; }
    public int? HasItemQty { get; set; }
}
