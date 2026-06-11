namespace OccultShop.Models;

public sealed class IngredientPortionDef
{
	public string IngredientId { get; set; } = "";
	public string ItemId { get; set; } = "";
	public string PreparationId { get; set; } = "";
	public int Grams { get; set; }

	public bool HasMeasuredAmount => Grams > 0;
	public bool HasPreparation => !string.IsNullOrWhiteSpace(PreparationId);
	public string InventoryItemId => string.IsNullOrWhiteSpace(ItemId) ? IngredientId : ItemId;

	public IngredientPortionDef Clone()
	{
		return new IngredientPortionDef
		{
			IngredientId = IngredientId,
			ItemId = ItemId,
			PreparationId = PreparationId,
			Grams = Grams
		};
	}
}
