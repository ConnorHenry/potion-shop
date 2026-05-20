using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class CustomerInteractionDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? CharacterImagePath { get; set; }
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    public List<CustomerChoiceDef> Choices { get; set; } = new();
}

public sealed class CustomerChoiceDef
{
    public string Label { get; set; } = "";
    public string? ItemId { get; set; }
    public bool IsFallback { get; set; }
    public bool IsRefuse { get; set; }
    public RequirementsDef? Requires { get; set; }
    public List<EffectDef> Effects { get; set; } = new();
}
