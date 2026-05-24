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

	private bool _dragStarted;

	public override void _Ready()
	{
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		_dragStarted = true;

		if (string.IsNullOrWhiteSpace(IconPath))
			return Variant.CreateFrom(ItemId);

		var preview = new TextureRect
		{
			CustomMinimumSize = new Vector2(70, 70),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		preview.Texture = ResourceLoader.Load<Texture2D>(IconPath);
		SetDragPreview(preview);
		_dragStarted = true;
		ReleaseFocus();
		return Variant.CreateFrom(ItemId);
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
}
