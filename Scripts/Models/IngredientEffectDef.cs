using System.Text.Json.Serialization;

namespace OccultShop.Models;

public sealed class IngredientEffectDef
{
	public const string BoostLowestOtherTraitKind = "boost_lowest_other_trait";
	public const string BoostLowestTraitIfNoRiskCarriesKind = "boost_lowest_trait_if_no_risk_carries";
	public const string BoostStrongestTraitAddRiskKind = "boost_strongest_trait_add_risk";
	public const string CopyStrongestOtherTraitKind = "copy_strongest_other_trait";
	public const string HalveOtherRisksKind = "halve_other_risks";
	public const string ReduceHighestRiskKind = "reduce_highest_risk";
	public const string SuppressSingleCarriedRiskKind = "suppress_single_carried_risk";
	public const string TemperTraitsKind = "temper_traits";
	public const string AddTraitIfRiskCarriesKind = "add_trait_if_risk_carries";

	[JsonPropertyName("kind")]
	public string Kind { get; set; } = "";

	[JsonPropertyName("family")]
	public string Family { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("description")]
	public string Description { get; set; } = "";

	[JsonPropertyName("amount")]
	public int Amount { get; set; }

	[JsonPropertyName("secondaryAmount")]
	public int SecondaryAmount { get; set; }

	[JsonPropertyName("traitId")]
	public string TraitId { get; set; } = "";

	[JsonPropertyName("riskId")]
	public string RiskId { get; set; } = "";
}
