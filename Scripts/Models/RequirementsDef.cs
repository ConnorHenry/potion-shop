namespace OccultShop.Models;

public sealed class RequirementsDef
{
    public int? GoldMin { get; set; }
    public int? DreadMin { get; set; }
    public int? DreadMax { get; set; }

    // Simple inventory gate (extend later)
    public string? HasItemId { get; set; }
    public int? HasItemQty { get; set; }

    public int? DayMin { get; set; }
    public int? DayMax { get; set; }
    public int? DayExact { get; set; }
    public string? HasStoryFlag { get; set; }
    public string? MissingStoryFlag { get; set; }
}
