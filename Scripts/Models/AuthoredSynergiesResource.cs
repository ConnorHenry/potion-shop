using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredSynergiesResource : Resource
{
	[Export]
	public Godot.Collections.Array Entries { get; set; } = new();
}
