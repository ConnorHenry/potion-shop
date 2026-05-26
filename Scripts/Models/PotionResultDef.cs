using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class PotionResult
{
	public Dictionary<string, int> Traits { get; set; } = new();
	public Dictionary<string, int> Risks { get; set; } = new();

	public List<string> TriggeredSynergies { get; set; } = new();
	public List<TriggeredSynergyDef> TriggeredSynergyDetails { get; set; } = new();

	public int IngredientQualityScore { get; set; }
	public int EffectFitScore { get; set; }
	public int SynergyScore { get; set; }
	public int StabilityScore { get; set; }
	public int PenaltyScore { get; set; }

	public float FinalScore { get; set; }
	public string Grade { get; set; } = "";

	public List<string> Notes { get; set; } = new();
}

public sealed class TriggeredSynergyDef
{
	public string Id { get; set; } = "";
	public List<string> RequiredTraits { get; set; } = new();
	public List<string> RequiredRisks { get; set; } = new();
	public Dictionary<string, int> ContributingTraits { get; set; } = new();
	public Dictionary<string, int> ContributingRisks { get; set; } = new();
	public int Modifier { get; set; }
	public string Description { get; set; } = "";
}
