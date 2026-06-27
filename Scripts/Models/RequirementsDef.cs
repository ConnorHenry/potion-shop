namespace OccultShop.Models;

public sealed class RequirementsDef
{
    public int? GoldMin { get; set; }
    public int? DreadMin { get; set; }
    public int? DreadMax { get; set; }
    public int? ReputationMin { get; set; }
    public int? ReputationMax { get; set; }

    // Simple inventory gate (extend later)
    public string? HasItemId { get; set; }
    public int? HasItemQty { get; set; }

    public int? DayMin { get; set; }
    public int? DayMax { get; set; }
    public int? DayExact { get; set; }
    public string? HasStoryFlag { get; set; }
    public string? MissingStoryFlag { get; set; }
    public string? QuestId { get; set; }
    public string? QuestStatus { get; set; }
    public string? RelationshipCharacterId { get; set; }
    public int? RelationshipMin { get; set; }
    public int? RelationshipMax { get; set; }
}
