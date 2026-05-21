public sealed class IngredientDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    // 0-100 quality score
    public int Quality { get; set; }

    // Trait name + strength
    // Example: "sleep": 4, "calm": 2
    public Dictionary<string, int> Traits { get; set; } = new();

    // Risk name + strength
    // Example: "addiction": 2, "hallucination": 3
    public Dictionary<string, int> Risks { get; set; } = new();

    // Optional flavour/system tags
    // Example: "herb", "graveyard", "cold"
    public List<string> Tags { get; set; } = new();
}