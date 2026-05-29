using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class CustomerInteractionDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? CharacterImagePath { get; set; }
    public string Pool { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public string StoryCharacterId { get; set; } = "";
    public string VisitId { get; set; } = "";
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    public Dictionary<string, int> DesiredTraits { get; set; } = new();
    public Dictionary<string, int> BadTraits { get; set; } = new();
    public List<EffectDef> OnSuccessEffects { get; set; } = new();
    public List<EffectDef> OnFailureEffects { get; set; } = new();
    public List<EffectDef> OnSkipEffects { get; set; } = new();

    public CustomerRequestDef BuildRequest()
    {
        return new CustomerRequestDef
        {
            Id = Id,
            Description = Text,
            DesiredTraits = new Dictionary<string, int>(DesiredTraits),
            BadTraits = new Dictionary<string, int>(BadTraits)
        };
    }
}

public sealed class CustomerRequestDef
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";

    // Desired effect traits
    // Example: "sleep": 5, "calm": 3
    public Dictionary<string, int> DesiredTraits { get; set; } = new();

    // Traits/risks that are bad for this request
    // Example: "addiction": 5, "rage": 4
    public Dictionary<string, int> BadTraits { get; set; } = new();
}
