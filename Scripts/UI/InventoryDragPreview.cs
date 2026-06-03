using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class InventoryDragPreview : Control
{
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public Vector2 DragPreviewOffset = new(5f, 5f);

	private const float PreviewSize = 70f;

	private ItemCatalogService? _itemCatalog;
	private TextureRect _icon = default!;
	private bool _dragActive;
	private bool _previewVisible;
	private bool _waitingForDragData;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		_icon = GetNode<TextureRect>("Icon");
		_icon.MouseFilter = MouseFilterEnum.Ignore;
		_icon.Visible = false;
		_icon.CustomMinimumSize = new Vector2(PreviewSize, PreviewSize);
		_icon.Size = new Vector2(PreviewSize, PreviewSize);

		_itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (_itemCatalog is null)
		{
			GD.PushError($"InventoryDragPreview: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		SetProcess(false);
	}

	public override void _Notification(int what)
	{
		base._Notification(what);

		if (what == NotificationDragBegin)
		{
			_dragActive = true;
			_waitingForDragData = true;
			SetProcess(true);
			TryShowPreview();
			return;
		}

		if (what == NotificationDragEnd)
			HidePreview();
	}

	public override void _Process(double delta)
	{
		if (!_dragActive)
			return;

		if (_waitingForDragData)
			TryShowPreview();

		if (!_previewVisible)
			return;

		_icon.Position = GetGlobalMousePosition() + DragPreviewOffset;
	}

	private void TryShowPreview()
	{
		if (_itemCatalog is null)
		{
			HidePreview();
			return;
		}

		var dragData = GetViewport().GuiGetDragData();
		if (dragData.VariantType != Variant.Type.String)
			return;

		var itemId = dragData.AsString();
		if (string.IsNullOrWhiteSpace(itemId))
		{
			HidePreview();
			return;
		}

		if (!_itemCatalog.TryGetItem(itemId, out var item))
		{
			HidePreview();
			return;
		}

		var texture = UiIconLoader.LoadIcon(item.IconPath);
		if (texture is null)
		{
			HidePreview();
			return;
		}

		_icon.Texture = texture;
		_icon.Visible = true;
		_previewVisible = true;
		_waitingForDragData = false;
		_icon.Position = GetGlobalMousePosition() + DragPreviewOffset;
	}

	private void HidePreview()
	{
		_dragActive = false;
		_previewVisible = false;
		_waitingForDragData = false;
		SetProcess(false);
		_icon.Texture = null;
		_icon.Visible = false;
	}
}
