using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class PreparedIngredientDef
{
	[JsonPropertyName("baseIngredientId")]
	public string BaseIngredientId { get; set; } = "";

	[JsonPropertyName("preparationId")]
	public string PreparationId { get; set; } = "";
}
