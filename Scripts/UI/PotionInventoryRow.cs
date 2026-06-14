using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class PotionInventoryRow : Control
{
	private const float SlotWidth = 112.0f;
	private const float SlotHeight = 168.0f;
	private const int VisiblePotionSlots = GameState.MaxUniquePotionInventoryQuantity;

	[Export] public NodePath PotionSlotsPath = default!;
	[Export] public NodePath ItemDetailPanelPath = new("../StationItemDetailPanel");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private GridContainer _potionSlots = default!;
	private StationItemDetailPanel? _itemDetailPanel;
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

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_itemDetailPanel = GetNodeOrNull<StationItemDetailPanel>(ItemDetailPanelPath);
		if (_itemDetailPanel is null)
			GD.PushError($"PotionInventoryRow: StationItemDetailPanel was not found at '{ItemDetailPanelPath}'.");
		var potionSlots = NodeLookup.GetRequiredNodeOrNull<GridContainer>(
			this,
			PotionSlotsPath,
			nameof(PotionInventoryRow),
			nameof(PotionSlotsPath));
		if (potionSlots is null)
			return;

		_potionSlots = potionSlots;
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
		var normalStyle = CreateSlotStyleBox(new Color(0.08f, 0.055f, 0.035f, 0.08f), new Color(0.36f, 0.24f, 0.13f, 0.16f));
		slot.AddThemeStyleboxOverride("normal", normalStyle);
		slot.AddThemeStyleboxOverride("hover", normalStyle);
		slot.AddThemeStyleboxOverride("pressed", normalStyle);
		slot.AddThemeStyleboxOverride("disabled", CreateSlotStyleBox(new Color(0.05f, 0.04f, 0.034f, 0.12f), new Color(0.22f, 0.17f, 0.12f, 0.22f)));
		slot.SlotActivated += ShowItemDetail;

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

		content.AddChild(JarredInventorySlotView.CreatePotionContent(
			new Vector2(SlotWidth, SlotHeight),
			stack.Name,
			stack.ItemId,
			stack.Quantity,
			new JarredInventorySlotLayout
			{
				ArtSize = new Vector2(SlotWidth, SlotHeight),
				NameColor = stack.HasActiveRisk
					? new Color(0.58f, 0.05f, 0.04f, 1.0f)
					: new Color(0.13f, 0.075f, 0.032f, 1.0f),
				NameFontSize = 12,
				MinimumNameFontSize = 9,
				SingleLineCharacterLimit = 10,
				QuantityFontSize = 16,
				UseReadableNamePlaque = true,
				UseGeneratedLabelTexture = true,
				GeneratedLabelRectRatio = new Rect2(new Vector2(0.03f, 0.634f), new Vector2(0.94f, 0.34f)),
				GeneratedNameRectRatio = new Rect2(new Vector2(0.08f, 0.657f), new Vector2(0.84f, 0.20f)),
				GeneratedQuantityRectRatio = new Rect2(new Vector2(0.36f, 0.858f), new Vector2(0.28f, 0.17f))
			}));

		slot.AddChild(content);
		slot.AddChild(hoverOutline);
		return slot;
	}

	private void ShowItemDetail(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;

		if (_itemDetailPanel is null)
		{
			GD.PushError("PotionInventoryRow: Cannot show item detail because StationItemDetailPanel is missing.");
			return;
		}

		_itemDetailPanel.ShowItem(itemId);
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
