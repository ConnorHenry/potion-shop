namespace OccultShop.Models;

public sealed class StoryCustomerVisitRecord
{
	public string VisitKey { get; set; } = "";
	public string StoryCharacterId { get; set; } = "";
	public string VisitId { get; set; } = "";
	public string InteractionId { get; set; } = "";
	public int ScheduledDay { get; set; }
	public bool HasArrived { get; set; }
	public int ArrivalDay { get; set; }
	public string LastOutcome { get; set; } = "";
	public int OutcomeDay { get; set; }
}
