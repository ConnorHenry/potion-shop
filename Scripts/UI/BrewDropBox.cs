using Godot;

namespace OccultShop.UI;

public partial class BrewDropBox : PanelContainer
{
	[Signal]
	public delegate void ItemDroppedEventHandler(string itemId);

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.String;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return;

		EmitSignal(SignalName.ItemDropped, data.AsString());
	}
}
