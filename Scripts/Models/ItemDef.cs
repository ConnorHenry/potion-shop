using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OccultShop.Models;

[JsonConverter(typeof(ItemDefJsonConverter))]
public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? IconPath { get; set; }
    public string Description { get; set; } = "";
    public bool StartsKnownInIngredientBook { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Quality { get; set; } = 50;
    public Dictionary<string, int> Traits { get; set; } = new();
    public Dictionary<string, int> Risks { get; set; } = new();
    public List<IngredientEffectDef> IngredientEffects { get; set; } = new();
    public int BasePrice { get; set; }
    public ConsumableEffectDef? ConsumableEffect { get; set; }
    public ConsumableGateDef? ConsumableGate { get; set; }
    public ItemTreatmentDef? Treatment { get; set; }
}
