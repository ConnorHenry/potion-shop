using System;
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
    public string DialogueStartNodeId { get; set; } = "";
    public List<CustomerDialogueNodeDef> DialogueNodes { get; set; } = new();
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    public Dictionary<string, int> DesiredTraits { get; set; } = new();
    public Dictionary<string, int> BadTraits { get; set; } = new();
    public List<EffectDef> OnSuccessEffects { get; set; } = new();
    public List<EffectDef> OnFailureEffects { get; set; } = new();
    public List<EffectDef> OnSkipEffects { get; set; } = new();

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
            BadTraits = new Dictionary<string, int>(BadTraits)
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
    public bool EndsInteraction { get; set; }
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
}
