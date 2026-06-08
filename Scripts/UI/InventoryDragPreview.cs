using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class InventoryDragPreview : Control
{
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public Vector2 DragPreviewOffset = Vector2.Zero;

	private const float PreviewSize = 140f;
	private const float DropPreviewSize = PreviewSize * 0.5f;
	private const float DropAnimationDurationSeconds = 0.32f;

	private static InventoryDragPreview? _activePreview;
	private ItemCatalogService? _itemCatalog;
	private TextureRect _icon = default!;
	private bool _dragActive;
	private bool _previewVisible;
	private bool _waitingForDragData;
	private bool _dropAnimationActive;
	private Vector2 _dropAnimationStartTopLeft;
	private Vector2 _dropAnimationEndTopLeft;
	private double _dropAnimationElapsedSeconds;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		_icon = GetNode<TextureRect>("Icon");
		_icon.MouseFilter = MouseFilterEnum.Ignore;
		_icon.Visible = false;
		SetIconSize(PreviewSize);

		_itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (_itemCatalog is null)
		{
			GD.PushError($"InventoryDragPreview: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_activePreview = this;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (_activePreview == this)
			_activePreview = null;
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
		if (_dropAnimationActive)
			UpdateDropAnimation(delta);

		if (_dragActive)
		{
			if (_waitingForDragData)
				TryShowPreview();

			if (_previewVisible)
				UpdateIconPosition();
		}

		if (!_dragActive && !_dropAnimationActive)
			SetProcess(false);
	}

	public static bool TryPlayBrewDropAnimation(string iconPath, Vector2 startCenterGlobalPosition, Vector2 endCenterGlobalPosition)
	{
		return _activePreview is not null &&
			_activePreview.StartBrewDropAnimation(iconPath, startCenterGlobalPosition, endCenterGlobalPosition);
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
		SetIconSize(PreviewSize);
		_icon.Visible = true;
		_previewVisible = true;
		_waitingForDragData = false;
		UpdateIconPosition();
	}

	private void UpdateIconPosition()
	{
		_icon.Position = GetGlobalMousePosition() - (_icon.Size * 0.5f) + DragPreviewOffset;
	}

	private void HidePreview()
	{
		_dragActive = false;
		_previewVisible = false;
		_waitingForDragData = false;
		if (_dropAnimationActive)
			return;

		SetProcess(false);
		_icon.Texture = null;
		_icon.Visible = false;
	}

	private bool StartBrewDropAnimation(string iconPath, Vector2 startCenterGlobalPosition, Vector2 endCenterGlobalPosition)
	{
		var texture = UiIconLoader.LoadIcon(iconPath);
		if (texture is null)
			return false;

		_dragActive = false;
		_previewVisible = false;
		_waitingForDragData = false;

		_icon.Texture = texture;
		SetIconSize(DropPreviewSize);
		_icon.Visible = true;
		_icon.MoveToFront();

		var halfSize = _icon.Size * 0.5f;
		_dropAnimationStartTopLeft = startCenterGlobalPosition - halfSize;
		_dropAnimationEndTopLeft = endCenterGlobalPosition - halfSize;
		_dropAnimationElapsedSeconds = 0.0;
		_dropAnimationActive = true;
		_icon.Position = _dropAnimationStartTopLeft;
		SetProcess(true);
		return true;
	}

	private void UpdateDropAnimation(double delta)
	{
		_dropAnimationElapsedSeconds += delta;
		var progress = Mathf.Clamp((float)(_dropAnimationElapsedSeconds / DropAnimationDurationSeconds), 0.0f, 1.0f);
		var easedProgress = progress * progress;
		_icon.Position = _dropAnimationStartTopLeft.Lerp(_dropAnimationEndTopLeft, easedProgress);

		if (progress < 1.0f)
			return;

		_dropAnimationActive = false;
		_icon.Texture = null;
		_icon.Visible = false;
		SetIconSize(PreviewSize);
	}

	private void SetIconSize(float size)
	{
		var iconSize = new Vector2(size, size);
		_icon.CustomMinimumSize = iconSize;
		_icon.Size = iconSize;
	}
}
