using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredDataResource : Resource
{
	[Export]
	public Godot.Collections.Array Items { get; set; } = new();

	[Export]
	public Godot.Collections.Array Rules { get; set; } = new();

	[Export]
	public Godot.Collections.Array Events { get; set; } = new();

	[Export]
	public Godot.Collections.Array CustomerInteractions { get; set; } = new();

	[Export]
	public Godot.Collections.Array Synergies { get; set; } = new();
}
