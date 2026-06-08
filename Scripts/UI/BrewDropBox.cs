using Godot;

namespace OccultShop.UI;

public partial class BrewDropBox : PanelContainer
{
	[Signal]
	public delegate void ItemDroppedEventHandler(string itemId);

	[Signal]
	public delegate void ItemDroppedAtEventHandler(string itemId, Vector2 globalPosition);

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.String;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return;

		var itemId = data.AsString();
		var globalPosition = GetViewport().GetMousePosition();
		EmitSignal(SignalName.ItemDroppedAt, itemId, globalPosition);
		EmitSignal(SignalName.ItemDropped, itemId);
	}
}
