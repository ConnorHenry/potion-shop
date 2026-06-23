using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class IngredientPreparationDef
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("traits")]
	public Dictionary<string, int> Traits { get; set; } = new();

	[JsonPropertyName("risks")]
	public Dictionary<string, int> Risks { get; set; } = new();

	[JsonPropertyName("boilingGame")]
	public BoilingMiniGameDef? BoilingGame { get; set; }
}
