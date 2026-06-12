using Godot;

namespace OccultShop.UI;

public partial class DraggablePanel : PanelContainer
{
	[Export] public NodePath DragHandlePath = new("");

	private Control? _dragHandle;
	private bool _dragFromWholePanel;
	private bool _dragging;
	private Vector2 _dragOffset;

	public override void _Ready()
	{
		if (DragHandlePath.IsEmpty)
		{
			_dragFromWholePanel = true;
			SetProcessInput(true);
		}
		else
		{
			_dragHandle = GetNode<Control>(DragHandlePath);
			_dragHandle.GuiInput += OnHandleGuiInput;
		}

		// Convert from centered anchors to absolute positioning so panel can be dragged freely.
		var rect = GetGlobalRect();
		AnchorLeft = 0.0f;
		AnchorTop = 0.0f;
		AnchorRight = 0.0f;
		AnchorBottom = 0.0f;
		Position = rect.Position;
		Size = rect.Size;
	}

	public override void _ExitTree()
	{
		if (_dragHandle is not null)
			_dragHandle.GuiInput -= OnHandleGuiInput;
	}

	public override void _Input(InputEvent @event)
	{
		if (!_dragFromWholePanel)
			return;
		if (!IsVisibleInTree())
			return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				if (!GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
					return;
				if (IsPressOnInteractiveChildControl())
					return;

				_dragging = true;
				_dragOffset = mouseButton.GlobalPosition - GlobalPosition;
				AcceptEvent();
				return;
			}

			if (!_dragging)
				return;

			_dragging = false;
			AcceptEvent();
			return;
		}

		if (_dragging && @event is InputEventMouseMotion mouseMotion)
		{
			GlobalPosition = mouseMotion.GlobalPosition - _dragOffset;
			AcceptEvent();
		}
	}

	private bool IsPressOnInteractiveChildControl()
	{
		var hoveredControl = GetViewport().GuiGetHoveredControl();
		if (hoveredControl is null)
			return false;
		if (hoveredControl == this)
			return false;
		if (!IsAncestorOf(hoveredControl))
			return false;

		return hoveredControl is BaseButton;
	}

	private void OnHandleGuiInput(InputEvent @event)
	{
		if (_dragHandle is null)
			return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				_dragging = true;
				_dragOffset = GetGlobalMousePosition() - GlobalPosition;
				_dragHandle.AcceptEvent();
				return;
			}

			_dragging = false;
			_dragHandle.AcceptEvent();
			return;
		}

		if (_dragging && @event is InputEventMouseMotion)
		{
			GlobalPosition = GetGlobalMousePosition() - _dragOffset;
			_dragHandle.AcceptEvent();
		}
	}
}
