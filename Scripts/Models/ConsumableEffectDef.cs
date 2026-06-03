using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class ConsumableEffectDef
{
	public const string RemoveRiskKind = "remove_risk";

	public string Kind { get; set; } = "";
	public string RiskId { get; set; } = "";
	public string Description { get; set; } = "";
}

public sealed class ConsumableGateDef
{
	public List<string> AllowedTargetTags { get; set; } = new();
}

public sealed class ItemTreatmentDef
{
	public string BaseItemId { get; set; } = "";
	public string ConsumableItemId { get; set; } = "";
	public string RemovedRisk { get; set; } = "";
}
