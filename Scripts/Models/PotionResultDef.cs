using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class PotionResult
{
	public Dictionary<string, int> Traits { get; set; } = new();
	public Dictionary<string, int> Risks { get; set; } = new();
	public Dictionary<string, int> PossibleRisks { get; set; } = new();

	public List<TriggeredIngredientEffectDef> TriggeredIngredientEffects { get; set; } = new();

	public int IngredientQualityScore { get; set; }
	public int EffectFitScore { get; set; }
	public int StabilityScore { get; set; }
	public int PenaltyScore { get; set; }
	public int RiskIngredientPricePenalty { get; set; }

	public float FinalScore { get; set; }
	public string Grade { get; set; } = "";

	public List<string> Notes { get; set; } = new();
}

public sealed class TriggeredIngredientEffectDef
{
	public string IngredientId { get; set; } = "";
	public string IngredientName { get; set; } = "";
	public string EffectName { get; set; } = "";
	public string Description { get; set; } = "";
	public string ResultText { get; set; } = "";
}
