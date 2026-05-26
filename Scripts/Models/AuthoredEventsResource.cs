using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredEventsResource : Resource
{
	[Export]
	public Godot.Collections.Array Entries { get; set; } = new();
}
