using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredItemsResource : Resource
{
	[Export]
	public Godot.Collections.Array Entries { get; set; } = new();
}
