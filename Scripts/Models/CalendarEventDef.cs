namespace OccultShop.Models;

public sealed class CalendarEventDef
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Text { get; set; } = "";
	public int Day { get; set; }
	public int Month { get; set; }
	public int? Year { get; set; }
	public bool RepeatsYearly { get; set; }
	public RequirementsDef? VisibilityRequirements { get; set; }
}
