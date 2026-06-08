using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class PotionInventoryRow : Control
{
	private const float SlotWidth = 112.0f;
	private const float SlotHeight = 168.0f;
	private const float IconSize = 62.0f;
	private const int SlotNameFontSize = 14;
	private const int VisiblePotionSlots = GameState.MaxUniquePotionInventoryQuantity;

	[Export] public NodePath PotionSlotsPath = default!;
	[Export] public NodePath InventoryPanelPath = new("../../InventoryPanel");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private GridContainer _potionSlots = default!;
	private InventoryPanel _inventoryPanel = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"PotionInventoryRow: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"PotionInventoryRow: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var inventoryPanel = GetNodeOrNull<InventoryPanel>(InventoryPanelPath);
		if (inventoryPanel is null)
		{
			GD.PushError($"PotionInventoryRow: InventoryPanel was not found at '{InventoryPanelPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_inventoryPanel = inventoryPanel;
		_potionSlots = GetRequiredNode<GridContainer>(PotionSlotsPath, nameof(PotionSlotsPath));
		if (_potionSlots is null)
			return;

		MouseFilter = MouseFilterEnum.Ignore;
		_gameState.Changed += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
	}

	public void Refresh()
	{
		if (_gameState is null || _itemCatalog is null || _potionSlots is null)
			return;

		ClearContainer(_potionSlots);
		foreach (var stack in BuildPotionStacks())
			_potionSlots.AddChild(CreatePotionSlot(stack));
	}

	private List<PotionStack> BuildPotionStacks()
	{
		var stacks = new List<PotionStack>();

		foreach (var stack in _gameState.Inventory)
		{
			if (stack.Value <= 0)
				continue;
			if (!_itemCatalog.TryGetItem(stack.Key, out var item))
				continue;
			if (!IsPotion(item))
				continue;

			stacks.Add(new PotionStack(
				stack.Key,
				DisplayName(stack.Key, item.Name),
				item.IconPath,
				stack.Value,
				HasActiveRisk(item)));

			if (stacks.Count >= VisiblePotionSlots)
				break;
		}

		return stacks;
	}

	private InventoryItemSlot CreatePotionSlot(PotionStack stack)
	{
		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = new Vector2(SlotWidth, SlotHeight),
			Size = new Vector2(SlotWidth, SlotHeight),
			TooltipText = stack.Name,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = false,
			ItemId = stack.ItemId,
			ItemName = stack.Name,
			IconPath = stack.IconPath,
			Quantity = stack.Quantity
		};
		slot.AddThemeStyleboxOverride("normal", CreateSlotStyleBox(new Color(0.082f, 0.092f, 0.103f, 0.92f), new Color(0.24f, 0.26f, 0.29f, 0.94f)));
		slot.AddThemeStyleboxOverride("hover", CreateSlotStyleBox(new Color(0.11f, 0.125f, 0.142f, 0.96f), new Color(0.34f, 0.37f, 0.41f, 0.98f)));
		slot.AddThemeStyleboxOverride("pressed", CreateSlotStyleBox(new Color(0.06f, 0.069f, 0.079f, 0.98f), new Color(0.19f, 0.21f, 0.23f, 0.98f)));
		slot.AddThemeStyleboxOverride("disabled", CreateSlotStyleBox(new Color(0.07f, 0.078f, 0.088f, 0.75f), new Color(0.18f, 0.2f, 0.22f, 0.78f)));
		slot.SlotActivated += ShowPotionDetail;

		var hoverOutline = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		hoverOutline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		hoverOutline.AddThemeStyleboxOverride("panel", CreateHoverOutlineStyleBox());
		slot.SetHoverOutline(hoverOutline);

		var content = new Control
		{
			CustomMinimumSize = new Vector2(SlotWidth, SlotHeight),
			Size = new Vector2(SlotWidth, SlotHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var icon = new TextureRect
		{
			Position = new Vector2((SlotWidth - IconSize) * 0.5f, 28.0f),
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			Size = new Vector2(IconSize, IconSize),
			Texture = UiIconLoader.LoadIcon(stack.IconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};

		var quantity = new Label
		{
			Text = stack.Quantity.ToString(),
			Position = new Vector2(8.0f, 6.0f),
			MouseFilter = MouseFilterEnum.Ignore
		};

		content.AddChild(icon);
		content.AddChild(quantity);
		content.AddChild(CreateNameBlock(stack.Name, stack.HasActiveRisk));

		slot.AddChild(content);
		slot.AddChild(hoverOutline);
		return slot;
	}

	private static Control CreateNameBlock(string itemName, bool hasActiveRisk)
	{
		var nameBlock = new Control
		{
			Position = new Vector2(6.0f, 92.0f),
			CustomMinimumSize = new Vector2(SlotWidth - 12.0f, 34.0f),
			Size = new Vector2(SlotWidth - 12.0f, 34.0f),
			MouseFilter = MouseFilterEnum.Ignore
		};

		InventoryItemTextFormatter.SplitInventoryName(itemName, out var firstLine, out var secondLine);
		nameBlock.AddChild(CreateNameLine(firstLine, 0.0f, hasActiveRisk));
		if (!string.IsNullOrWhiteSpace(secondLine))
			nameBlock.AddChild(CreateNameLine(secondLine, 15.0f, hasActiveRisk));

		return nameBlock;
	}

	private static Label CreateNameLine(string text, float topOffset, bool hasActiveRisk)
	{
		var label = new Label
		{
			Text = text,
			Position = new Vector2(0.0f, topOffset),
			CustomMinimumSize = new Vector2(SlotWidth - 12.0f, 0.0f),
			Size = new Vector2(SlotWidth - 12.0f, 18.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = false
		};
		label.AddThemeFontSizeOverride("font_size", SlotNameFontSize);
		if (hasActiveRisk)
			label.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f, 1.0f));

		return label;
	}

	private void ShowPotionDetail(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;
		if (!_itemCatalog.IsPotion(itemId))
			return;

		_inventoryPanel.OpenItemDetail(itemId);
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		var customName = _gameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private static bool IsPotion(ItemDef item)
	{
		return ItemCatalogService.HasTag(item, ItemTags.Potion);
	}

	private static bool HasActiveRisk(ItemDef item)
	{
		if (item.Risks is null || item.Risks.Count == 0)
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
				return true;
		}

		return false;
	}

	private TNode GetRequiredNode<TNode>(NodePath path, string exportName) where TNode : Node
	{
		var node = GetNodeOrNull<TNode>(path);
		if (node is null)
		{
			GD.PushError($"PotionInventoryRow: {exportName} was not found at '{path}'.");
			return default!;
		}

		return node;
	}

	private static void ClearContainer(Node container)
	{
		foreach (var child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static StyleBoxFlat CreateSlotStyleBox(Color fillColor, Color borderColor)
	{
		return new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = borderColor,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}

	private static StyleBoxFlat CreateHoverOutlineStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			BorderColor = Colors.White,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}

	private readonly record struct PotionStack(
		string ItemId,
		string Name,
		string? IconPath,
		int Quantity,
		bool HasActiveRisk);
}
