namespace OccultShop.Models;

public sealed class GardenCropDef
{
	public string IngredientId { get; set; } = "";
	public string SeedId { get; set; } = "";
	public int GrowthDays { get; set; }
	public int HarvestYieldMin { get; set; }
	public int HarvestYieldMax { get; set; }
}
