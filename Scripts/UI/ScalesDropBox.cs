using Godot;

namespace OccultShop.UI;

public partial class ScalesDropBox : PanelContainer
{
	[Signal]
	public delegate void ItemDroppedEventHandler(string itemId);

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return false;

		var value = data.AsString();
		return !ScaleWeightButton.TryParseDragData(value, out _);
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
			return;

		var value = data.AsString();
		if (ScaleWeightButton.TryParseDragData(value, out _))
			return;

		EmitSignal(SignalName.ItemDropped, value);
	}
}
