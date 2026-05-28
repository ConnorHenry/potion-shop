using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredPotionRecipesResource : Resource
{
	[Export]
	public Godot.Collections.Array Entries { get; set; } = new();
}
