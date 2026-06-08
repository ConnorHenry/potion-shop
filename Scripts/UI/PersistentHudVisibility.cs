using Godot;

namespace OccultShop.UI;

public partial class PersistentHudVisibility : Node
{
	[Export] public bool HudVisible { get; set; } = true;
}
