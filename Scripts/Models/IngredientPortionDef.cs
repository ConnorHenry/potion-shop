namespace OccultShop.Models;

public sealed class IngredientPortionDef
{
	public string IngredientId { get; set; } = "";
	public int Grams { get; set; }

	public bool HasMeasuredAmount => Grams > 0;

	public IngredientPortionDef Clone()
	{
		return new IngredientPortionDef
		{
			IngredientId = IngredientId,
			Grams = Grams
		};
	}
}
