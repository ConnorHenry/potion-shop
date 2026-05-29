using Godot;

namespace OccultShop.UI;

public partial class CustomerSellDropBox : PanelContainer
{
    [Signal]
    public delegate void ItemDroppedEventHandler(string itemId);
    [Signal]
    public delegate void ItemHoverPreviewEventHandler(string itemId);
    [Signal]
    public delegate void HoverPreviewClearedEventHandler();

	private bool _acceptDrops = true;

	public void SetAcceptDrops(bool acceptDrops)
	{
		if (_acceptDrops == acceptDrops)
			return;

		_acceptDrops = acceptDrops;
		if (!acceptDrops)
			EmitSignal(SignalName.HoverPreviewCleared);
	}

	public override void _Ready()
	{
		MouseExited += OnMouseExited;
	}

	public override void _ExitTree()
	{
		MouseExited -= OnMouseExited;
	}

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!_acceptDrops)
            return false;

        if (data.VariantType != Variant.Type.String)
            return false;

        EmitSignal(SignalName.ItemHoverPreview, data.AsString());
        return true;
    }

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (!_acceptDrops)
			return;

		if (data.VariantType != Variant.Type.String)
			return;

		EmitSignal(SignalName.ItemDropped, data.AsString());
		EmitSignal(SignalName.HoverPreviewCleared);
	}

	private void OnMouseExited()
	{
		EmitSignal(SignalName.HoverPreviewCleared);
	}
}
