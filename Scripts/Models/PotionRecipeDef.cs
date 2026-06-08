using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class PotionRecipeDef
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public List<string> IngredientIds { get; set; } = new();
	public List<IngredientPortionDef> IngredientAmounts { get; set; } = new();
	public List<string> Traits { get; set; } = new();
}
