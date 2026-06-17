using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class CustomerInteractionDef
{
    public const int MaxDialogueOptionsPerNode = 8;

    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<CustomerDialogueLineDef> Lines { get; set; } = new();
    public string? CharacterImagePath { get; set; }
    public Dictionary<string, string> CharacterImagePaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Pool { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public string StoryCharacterId { get; set; } = "";
    public string VisitId { get; set; } = "";
    public string DialogueStartNodeId { get; set; } = "";
    public List<CustomerDialogueNodeDef> DialogueNodes { get; set; } = new();
    public RequirementsDef? Requires { get; set; }
    public int Weight { get; set; } = 1;
    [JsonConverter(typeof(DesiredTraitRangeDictionaryJsonConverter))]
    public Dictionary<string, CustomerTraitRangeDef> DesiredTraits { get; set; } = new();
    [JsonConverter(typeof(BadTraitRangeDictionaryJsonConverter))]
    public Dictionary<string, CustomerTraitRangeDef> BadTraits { get; set; } = new();
    public Dictionary<string, int> RequiredMinTraits { get; set; } = new();
    public Dictionary<string, int> RequiredMaxTraits { get; set; } = new();
    public List<IngredientPortionDef> RequiredIngredientAmounts { get; set; } = new();
    public List<EffectDef> OnSuccessEffects { get; set; } = new();
    public List<EffectDef> OnFailureEffects { get; set; } = new();
    public List<EffectDef> OnSkipEffects { get; set; } = new();
    public string PotionRefusedText { get; set; } = "";
    public List<CustomerDialogueLineDef> PotionRefusedLines { get; set; } = new();
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
            DesiredTraits = CustomerTraitRangeDef.CloneDictionary(DesiredTraits),
            BadTraits = CustomerTraitRangeDef.CloneDictionary(BadTraits),
            RequiredMinTraits = new Dictionary<string, int>(RequiredMinTraits),
            RequiredMaxTraits = new Dictionary<string, int>(RequiredMaxTraits),
            RequiredIngredientAmounts = RequiredIngredientAmounts.Select(x => x.Clone()).ToList()
        };
    }
}

[JsonConverter(typeof(CustomerTraitRangeDefJsonConverter))]
public sealed class CustomerTraitRangeDef
{
    public int? Min { get; set; }
    public int? Max { get; set; }

    public bool HasMin => Min is not null;
    public bool HasMax => Max is not null;

    public CustomerTraitRangeDef Clone()
    {
        return new CustomerTraitRangeDef
        {
            Min = Min,
            Max = Max
        };
    }

    public static Dictionary<string, CustomerTraitRangeDef> CloneDictionary(
        IReadOnlyDictionary<string, CustomerTraitRangeDef>? source)
    {
        var clone = new Dictionary<string, CustomerTraitRangeDef>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
            return clone;

        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                continue;

            clone[pair.Key] = pair.Value.Clone();
        }

        return clone;
    }
}

public sealed class CustomerTraitRangeDefJsonConverter : JsonConverter<CustomerTraitRangeDef>
{
    public override CustomerTraitRangeDef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return CustomerTraitRangeJsonConverterHelpers.ReadRange(ref reader, legacyIntIsMinimum: true) ?? new CustomerTraitRangeDef();
    }

    public override void Write(Utf8JsonWriter writer, CustomerTraitRangeDef value, JsonSerializerOptions options)
    {
        CustomerTraitRangeJsonConverterHelpers.WriteRange(writer, value);
    }
}

public sealed class DesiredTraitRangeDictionaryJsonConverter : CustomerTraitRangeDictionaryJsonConverter
{
    protected override bool LegacyIntIsMinimum => true;
}

public sealed class BadTraitRangeDictionaryJsonConverter : CustomerTraitRangeDictionaryJsonConverter
{
    protected override bool LegacyIntIsMinimum => false;
}

public abstract class CustomerTraitRangeDictionaryJsonConverter : JsonConverter<Dictionary<string, CustomerTraitRangeDef>>
{
    protected abstract bool LegacyIntIsMinimum { get; }

    public override Dictionary<string, CustomerTraitRangeDef> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var result = new Dictionary<string, CustomerTraitRangeDef>(StringComparer.OrdinalIgnoreCase);
        if (reader.TokenType == JsonTokenType.Null)
            return result;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Trait ranges must be a JSON object.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Trait range entries must use property names.");

            var traitId = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Trait range entry ended unexpectedly.");

            var range = CustomerTraitRangeJsonConverterHelpers.ReadRange(ref reader, LegacyIntIsMinimum);
            if (string.IsNullOrWhiteSpace(traitId) || range is null)
                continue;

            result[traitId] = range;
        }

        throw new JsonException("Trait range object ended unexpectedly.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, CustomerTraitRangeDef> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value is not null)
        {
            foreach (var pair in value)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                    continue;

                writer.WritePropertyName(pair.Key);
                CustomerTraitRangeJsonConverterHelpers.WriteRange(writer, pair.Value);
            }
        }

        writer.WriteEndObject();
    }
}

internal static class CustomerTraitRangeJsonConverterHelpers
{
    public static CustomerTraitRangeDef? ReadRange(ref Utf8JsonReader reader, bool legacyIntIsMinimum)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
        {
            var legacyValue = reader.GetInt32();
            return legacyIntIsMinimum
                ? new CustomerTraitRangeDef { Min = legacyValue }
                : new CustomerTraitRangeDef { Max = legacyValue };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        var range = new CustomerTraitRangeDef();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return range.HasMin || range.HasMax ? range : null;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Trait range fields must use property names.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Trait range field ended unexpectedly.");

            if (string.Equals(propertyName, "min", StringComparison.OrdinalIgnoreCase))
            {
                range.Min = ReadNullableInt(ref reader);
                continue;
            }

            if (string.Equals(propertyName, "max", StringComparison.OrdinalIgnoreCase))
            {
                range.Max = ReadNullableInt(ref reader);
                continue;
            }

            reader.Skip();
        }

        throw new JsonException("Trait range object ended unexpectedly.");
    }

    public static void WriteRange(Utf8JsonWriter writer, CustomerTraitRangeDef? range)
    {
        writer.WriteStartObject();
        if (range is not null)
        {
            if (range.Min is int min)
                writer.WriteNumber("min", min);

            if (range.Max is int max)
                writer.WriteNumber("max", max);
        }

        writer.WriteEndObject();
    }

    private static int? ReadNullableInt(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.Number)
        {
            reader.Skip();
            return null;
        }

        return reader.GetInt32();
    }
}

public sealed class CustomerDialogueNodeDef
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<CustomerDialogueLineDef> Lines { get; set; } = new();
    public List<CustomerDialogueOptionDef> Options { get; set; } = new();
}

public sealed class CustomerDialogueLineDef
{
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public string CharacterImageKey { get; set; } = "";
}

public sealed class CustomerDialogueOptionDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string ResponseText { get; set; } = "";
    public List<CustomerDialogueLineDef> ResponseLines { get; set; } = new();
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
    public List<CustomerDialogueLineDef> Lines { get; set; } = new();
    public List<EffectDef> Effects { get; set; } = new();
}

public sealed class CustomerRequestDef
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";

    // Desired effect trait ranges.
    // Authored customer requests should use both min and max so overshooting can fail.
    // Example: "calm": { min: 2, max: 4 }, "clarity": { min: 1, max: 3 }
    [JsonConverter(typeof(DesiredTraitRangeDictionaryJsonConverter))]
    public Dictionary<string, CustomerTraitRangeDef> DesiredTraits { get; set; } = new();

    // Trait/risk ranges that are bad for this request when exceeded.
    // Example: "drowsiness": { max: 1 }, "confusion": { max: 0 }
    [JsonConverter(typeof(BadTraitRangeDictionaryJsonConverter))]
    public Dictionary<string, CustomerTraitRangeDef> BadTraits { get; set; } = new();

    // Hard trait thresholds that must be met by the final potion.
    // Example: "mend": 9 means Mend must be >= 9.
    public Dictionary<string, int> RequiredMinTraits { get; set; } = new();

    // Hard trait ceilings that must not be exceeded by the final potion.
    // Example: "vigor": 1 means Vigor must be <= 1.
    public Dictionary<string, int> RequiredMaxTraits { get; set; } = new();

    public List<IngredientPortionDef> RequiredIngredientAmounts { get; set; } = new();
}
