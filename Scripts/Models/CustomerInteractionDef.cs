using System;
using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class CustomerInteractionDef
{
    public const int MaxDialogueOptionsPerNode = 8;

    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? CharacterImagePath { get; set; }
    public string Pool { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public string StoryCharacterId { get; set; } = "";
    public string VisitId { get; set; } = "";
    public string DialogueStartNodeId { get; set; } = "";
    public List<CustomerDialogueNodeDef> DialogueNodes { get; set; } = new();
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    public Dictionary<string, int> DesiredTraits { get; set; } = new();
    public Dictionary<string, int> BadTraits { get; set; } = new();
    public Dictionary<string, int> RequiredMinTraits { get; set; } = new();
    public Dictionary<string, int> RequiredMaxTraits { get; set; } = new();
    public List<IngredientPortionDef> RequiredIngredientAmounts { get; set; } = new();
    public List<EffectDef> OnSuccessEffects { get; set; } = new();
    public List<EffectDef> OnFailureEffects { get; set; } = new();
    public List<EffectDef> OnSkipEffects { get; set; } = new();
    public string PotionRefusedText { get; set; } = "";
    public List<EffectDef> OnPotionRefusedEffects { get; set; } = new();
    public List<CustomerPotionResponseDef> PotionResponses { get; set; } = new();

    public bool IsStoryInteraction => !string.IsNullOrWhiteSpace(StoryCharacterId);

    public string GetStoryVisitId()
    {
        return string.IsNullOrWhiteSpace(VisitId) ? Id : VisitId;
    }

    public bool HasDialogueTree => DialogueNodes.Count > 0;

    public CustomerDialogueNodeDef? GetDialogueNode(string nodeId)
    {
        var resolvedNodeId = string.IsNullOrWhiteSpace(nodeId) ? DialogueStartNodeId : nodeId;
        if (string.IsNullOrWhiteSpace(resolvedNodeId) && DialogueNodes.Count > 0)
            return DialogueNodes[0];

        foreach (var node in DialogueNodes)
        {
            if (string.Equals(node.Id, resolvedNodeId, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        return null;
    }

    public CustomerRequestDef BuildRequest()
    {
        return new CustomerRequestDef
        {
            Id = Id,
            Description = Text,
            DesiredTraits = new Dictionary<string, int>(DesiredTraits),
            BadTraits = new Dictionary<string, int>(BadTraits),
            RequiredMinTraits = new Dictionary<string, int>(RequiredMinTraits),
            RequiredMaxTraits = new Dictionary<string, int>(RequiredMaxTraits),
            RequiredIngredientAmounts = RequiredIngredientAmounts.Select(x => x.Clone()).ToList()
        };
    }
}

public sealed class CustomerDialogueNodeDef
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<CustomerDialogueOptionDef> Options { get; set; } = new();
}

public sealed class CustomerDialogueOptionDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string ResponseText { get; set; } = "";
    public string NextNodeId { get; set; } = "";
    public string ReturnNodeId { get; set; } = "";
    public bool RevealsRequest { get; set; }
    public bool ReturnsToDialogue { get; set; }
    public bool EndsInteraction { get; set; }
    public RequirementsDef? Requires { get; set; }
    public List<EffectDef> Effects { get; set; } = new();
}

public sealed class CustomerPotionResponseDef
{
    public string Id { get; set; } = "";
    public bool? Success { get; set; }
    public string PotionItemId { get; set; } = "";
    public string Grade { get; set; } = "";
    public int? MinFinalScore { get; set; }
    public int? MaxFinalScore { get; set; }
    public int? MinMatchedDesiredTraits { get; set; }
    public int? MaxMatchedBadTraits { get; set; }
    public string Text { get; set; } = "";
    public List<EffectDef> Effects { get; set; } = new();
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

    // Hard trait thresholds that must be met by the final potion.
    // Example: "mend": 9 means Mend must be >= 9.
    public Dictionary<string, int> RequiredMinTraits { get; set; } = new();

    // Hard trait ceilings that must not be exceeded by the final potion.
    // Example: "vigor": 1 means Vigor must be <= 1.
    public Dictionary<string, int> RequiredMaxTraits { get; set; } = new();

    public List<IngredientPortionDef> RequiredIngredientAmounts { get; set; } = new();
}
