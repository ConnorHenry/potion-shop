using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredCalendarEventsResource : Resource
{
	[Export]
	public Godot.Collections.Array Entries { get; set; } = new();
}
