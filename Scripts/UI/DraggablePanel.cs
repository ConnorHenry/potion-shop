using Godot;

namespace OccultShop.UI;

public partial class DraggablePanel : PanelContainer
{
    [Export] public NodePath DragHandlePath = default!;

    private Control _dragHandle = default!;
    private bool _dragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        _dragHandle = GetNode<Control>(DragHandlePath);
        _dragHandle.GuiInput += OnHandleGuiInput;

        // Convert from centered anchors to absolute positioning so panel can be dragged freely.
        var rect = GetGlobalRect();
        AnchorLeft = 0.0f;
        AnchorTop = 0.0f;
        AnchorRight = 0.0f;
        AnchorBottom = 0.0f;
        Position = rect.Position;
        Size = rect.Size;
    }

    private void OnHandleGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _dragging = true;
                _dragOffset = GetGlobalMousePosition() - Position;
                _dragHandle.AcceptEvent();
                return;
            }

            _dragging = false;
            _dragHandle.AcceptEvent();
            return;
        }

        if (_dragging && @event is InputEventMouseMotion)
        {
            Position = GetGlobalMousePosition() - _dragOffset;
            _dragHandle.AcceptEvent();
        }
    }
}
