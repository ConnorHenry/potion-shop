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

    private static readonly Color HoverLabelColor = new(0.43f, 0.83f, 0.48f, 1f);
    private static readonly Color HoverPanelBackgroundColor = new(0.10f, 0.22f, 0.12f, 0.88f);
    private static readonly Color HoverPanelBorderColor = new(0.43f, 0.83f, 0.48f, 0.92f);
    private static readonly Color DisabledModulate = new(1f, 1f, 1f, 0.38f);

	private bool _acceptDrops = true;
    private bool _hoverHighlightActive;
    private bool _disabledVisualActive;
    private Label _dropLabel = default!;
    private StyleBox _defaultPanelStyle = default!;
    private StyleBox _hoverPanelStyle = default!;
    private Color _defaultLabelColor;
    private Color _defaultModulate;

	public void SetAcceptDrops(bool acceptDrops)
	{
		if (_acceptDrops == acceptDrops)
			return;

		_acceptDrops = acceptDrops;
        SetHoverHighlight(false);
		if (!acceptDrops)
			EmitSignal(SignalName.HoverPreviewCleared);
	}

    public void SetDisabledVisual(bool disabled)
    {
        if (_disabledVisualActive == disabled)
            return;

        _disabledVisualActive = disabled;
        if (disabled)
        {
            SetHoverHighlight(false);
            Modulate = DisabledModulate;
            return;
        }

        Modulate = _defaultModulate;
    }

    public void SetHoverHighlight(bool active)
    {
        var shouldHighlight = active && _acceptDrops && !_disabledVisualActive;
        if (_hoverHighlightActive == shouldHighlight)
            return;

        _hoverHighlightActive = shouldHighlight;
        AddThemeStyleboxOverride("panel", shouldHighlight ? _hoverPanelStyle : _defaultPanelStyle);
        _dropLabel.AddThemeColorOverride("font_color", shouldHighlight ? HoverLabelColor : _defaultLabelColor);
    }

	public override void _Ready()
	{
        _dropLabel = GetNode<Label>("DropMargin/DropLabel");
        _defaultLabelColor = _dropLabel.GetThemeColor("font_color");
        _defaultModulate = Modulate;

        var basePanelStyle = GetThemeStylebox("panel");
        if (basePanelStyle is null)
        {
            GD.PushError("CustomerSellDropBox: panel theme style was not found.");
            return;
        }

        _defaultPanelStyle = (StyleBox)basePanelStyle.Duplicate();
        _hoverPanelStyle = CreateHoverPanelStyleBox(basePanelStyle);

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

        SetHoverHighlight(false);
		EmitSignal(SignalName.ItemDropped, data.AsString());
		EmitSignal(SignalName.HoverPreviewCleared);
	}

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what == NotificationDragEnd)
        {
            SetHoverHighlight(false);
            EmitSignal(SignalName.HoverPreviewCleared);
        }
    }

	private void OnMouseExited()
	{
        SetHoverHighlight(false);
		EmitSignal(SignalName.HoverPreviewCleared);
	}

    private static StyleBox CreateHoverPanelStyleBox(StyleBox basePanelStyle)
    {
        if (basePanelStyle is not StyleBoxFlat baseFlat)
            return (StyleBox)basePanelStyle.Duplicate();

        var hoverStyle = (StyleBoxFlat)baseFlat.Duplicate();
        hoverStyle.BgColor = HoverPanelBackgroundColor;
        hoverStyle.BorderColor = HoverPanelBorderColor;
        return hoverStyle;
    }
}
