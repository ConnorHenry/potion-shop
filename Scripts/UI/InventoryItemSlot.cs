using Godot;

namespace OccultShop.UI;

public partial class InventoryItemSlot : Button
{
	[Signal]
	public delegate void SlotActivatedEventHandler(string itemId);

	[Signal]
	public delegate void IngredientRequestedEventHandler(string itemId);

	public string ItemId { get; set; } = "";
	public string ItemName { get; set; } = "";
	public string? IconPath { get; set; }
	public int Quantity { get; set; }

	private PanelContainer? _hoverOutline;
	private bool _dragStarted;
	private bool _isHovered;

	public override void _Ready()
	{
		MouseDefaultCursorShape = CursorShape.PointingHand;
		FocusMode = FocusModeEnum.None;
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
		UpdateHoverOutline();
	}

	public override void _ExitTree()
	{
		MouseEntered -= OnMouseEntered;
		MouseExited -= OnMouseExited;
	}

	public void SetHoverOutline(PanelContainer hoverOutline)
	{
		_hoverOutline = hoverOutline;
		UpdateHoverOutline();
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		_dragStarted = true;
		var preview = new Control
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = Vector2.Zero
		};
		SetDragPreview(preview);
		ReleaseFocus();
		return Variant.CreateFrom(ItemId);
	}

	public override void _Notification(int what)
	{
		base._Notification(what);

		if (what == NotificationDragEnd)
			_dragStarted = false;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton rightMouseButton && rightMouseButton.ButtonIndex == MouseButton.Right && rightMouseButton.Pressed)
		{
			EmitSignal(SignalName.IngredientRequested, ItemId);
			AcceptEvent();
			return;
		}

		if (@event is InputEventMouseButton leftMouseButton && leftMouseButton.ButtonIndex == MouseButton.Left && !leftMouseButton.Pressed)
		{
			if (_dragStarted)
			{
				_dragStarted = false;
				AcceptEvent();
				return;
			}

			EmitSignal(SignalName.SlotActivated, ItemId);
			AcceptEvent();
			return;
		}

		base._GuiInput(@event);
	}

	private void OnMouseEntered()
	{
		_isHovered = true;
		UpdateHoverOutline();
	}

	private void OnMouseExited()
	{
		_isHovered = false;
		UpdateHoverOutline();
	}

	private void UpdateHoverOutline()
	{
		if (_hoverOutline is not null)
			_hoverOutline.Visible = _isHovered;
	}
}
