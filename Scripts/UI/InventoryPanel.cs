using System.Linq;
using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class InventoryPanel : Control
{
	private const float SlotSize = 90.0f;
	private const float IconSize = 70.0f;

	[Export] public NodePath ItemsContainerPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath ItemDetailPanelPath = default!;
	[Export] public NodePath ItemDetailImagePath = default!;
	[Export] public NodePath ItemDetailNamePath = default!;
	[Export] public NodePath ItemDetailPricePath = default!;
	[Export] public NodePath ItemDetailDescriptionPath = default!;
	[Export] public NodePath ItemDetailCloseButtonPath = default!;

	private GridContainer _items = default!;
	private Button _closeButton = default!;
	private Control _itemDetailPanel = default!;
	private TextureRect _itemDetailImage = default!;
	private Label _itemDetailName = default!;
	private Label _itemDetailPrice = default!;
	private RichTextLabel _itemDetailDescription = default!;
	private Button _itemDetailCloseButton = default!;

	public override void _Ready()
	{
		_items = GetNode<GridContainer>(ItemsContainerPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		_itemDetailPanel = GetNode<Control>(ItemDetailPanelPath);
		_itemDetailImage = GetNode<TextureRect>(ItemDetailImagePath);
		_itemDetailName = GetNode<Label>(ItemDetailNamePath);
		_itemDetailPrice = GetNode<Label>(ItemDetailPricePath);
		_itemDetailDescription = GetNode<RichTextLabel>(ItemDetailDescriptionPath);
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_itemDetailCloseButton.Pressed += HideItemDetail;
		GameState.Changed += Refresh;

		Visible = false;
		_itemDetailPanel.Visible = false;
		Refresh();
	}

	public override void _ExitTree()
	{
		GameState.Changed -= Refresh;
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (Visible)
			Refresh();
	}

	public void HidePanel()
	{
		HideItemDetail();
		Visible = false;
	}

	private void Refresh()
	{
		if (_items is null)
			return;

		foreach (var child in _items.GetChildren())
			child.QueueFree();

		if (GameState.Inventory.Count == 0)
		{
			_items.AddChild(new Label { Text = "Empty" });
			return;
		}

		foreach (var stack in GameState.Inventory.OrderBy(x => ItemName(x.Key)))
		{
			_items.AddChild(CreateSlot(stack.Key, stack.Value));
		}
	}

	private Control CreateSlot(string itemId, int quantity)
	{
		var item = DataDb.Items.TryGetValue(itemId, out var def) ? def : null;

		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			TooltipText = item?.Name ?? itemId,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = true,
			ItemId = itemId,
			ItemName = item?.Name ?? itemId,
			IconPath = item?.IconPath,
			Quantity = quantity
		};
		slot.SlotActivated += ShowItemDetail;

		var content = new Control
		{
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var icon = new TextureRect
		{
			Position = new Vector2(10, 10),
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		icon.Texture = LoadIcon(item?.IconPath);

		var qty = new Label
		{
			Text = quantity.ToString(),
			Position = new Vector2(4, 2),
			MouseFilter = MouseFilterEnum.Ignore
		};

		content.AddChild(icon);
		content.AddChild(qty);
		slot.AddChild(content);
		return slot;
	}

	private void ShowItemDetail(string itemId)
	{
		if (!DataDb.Items.TryGetValue(itemId, out var item))
			return;

		_itemDetailImage.Texture = LoadIcon(item.IconPath);
		_itemDetailName.Text = item.Name;
		_itemDetailPrice.Text = $"Sale Price: {item.BasePrice} gold";
		_itemDetailDescription.Text = item.Description;
		_itemDetailPanel.Visible = true;
	}

	private void HideItemDetail()
	{
		_itemDetailImage.Texture = null;
		_itemDetailPanel.Visible = false;
	}

	private static Texture2D? LoadIcon(string? iconPath)
	{
		if (string.IsNullOrWhiteSpace(iconPath))
			return null;

		return ResourceLoader.Load<Texture2D>(iconPath);
	}

	private static string ItemName(string itemId)
	{
		return DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
	}

	private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
