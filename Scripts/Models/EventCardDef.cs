using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class EventCardDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? CharacterImagePath { get; set; }
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    public List<EventChoiceDef> Choices { get; set; } = new();
}

public sealed class EventChoiceDef
{
    public string Label { get; set; } = "";
    public RequirementsDef? Requires { get; set; }
    public List<EffectDef> Effects { get; set; } = new();
}
