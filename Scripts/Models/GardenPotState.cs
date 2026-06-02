using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class GardenPotState
{
	public int PotIndex { get; set; }
	public string SeedId { get; set; } = "";
	public string IngredientId { get; set; } = "";
	public int PlantedDay { get; set; }
	public int DaysGrown { get; set; }
	public int RequiredGrowthDays { get; set; }
	public int HarvestYieldMin { get; set; }
	public int HarvestYieldMax { get; set; }

	[JsonIgnore]
	public bool IsEmpty => string.IsNullOrWhiteSpace(IngredientId);

	[JsonIgnore]
	public bool IsReady => !IsEmpty && DaysGrown >= RequiredGrowthDays;
}
