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
	[Export] public string SlotLayoutSettingsPath = InventorySlotLayoutSettings.DefaultResourcePath;

	private GridContainer _potionSlots = default!;
	private StationItemDetailPanel? _itemDetailPanel;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private InventorySlotLayoutSettings _slotLayoutSettings = default!;

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
		_slotLayoutSettings = LoadSlotLayoutSettings();
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

		InventorySlotVisuals.ClearChildren(_potionSlots);
		foreach (var stack in BuildPotionStacks())
			_potionSlots.AddChild(CreatePotionSlot(stack));
	}

	public void RefreshSlotLayoutSettings()
	{
		_slotLayoutSettings = LoadSlotLayoutSettings(forceReload: true);
		Refresh();
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
		var profile = GetPotionSlotProfile();
		var slotSize = profile.ResolveSlotSize(new Vector2(SlotWidth, SlotHeight));
		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			TooltipText = stack.Name,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = false,
			ItemId = stack.ItemId,
			ItemName = stack.Name,
			IconPath = stack.IconPath,
			Quantity = stack.Quantity
		};
		var normalStyle = InventorySlotVisuals.CreateSlotStyleBox(
			new Color(0.08f, 0.055f, 0.035f, 0.08f),
			new Color(0.36f, 0.24f, 0.13f, 0.16f),
			cornerRadius: 6);
		slot.AddThemeStyleboxOverride("normal", normalStyle);
		slot.AddThemeStyleboxOverride("hover", normalStyle);
		slot.AddThemeStyleboxOverride("pressed", normalStyle);
		slot.AddThemeStyleboxOverride("disabled", InventorySlotVisuals.CreateSlotStyleBox(
			new Color(0.05f, 0.04f, 0.034f, 0.12f),
			new Color(0.22f, 0.17f, 0.12f, 0.22f),
			cornerRadius: 6));
		slot.SlotActivated += ShowItemDetail;

		var hoverOutline = InventorySlotVisuals.CreateHoverOutline(
			new Color(1.0f, 1.0f, 1.0f, 0.0f),
			Colors.White,
			cornerRadius: 6,
			borderWidth: 2);
		slot.SetHoverOutline(hoverOutline);

		var content = new Control
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = MouseFilterEnum.Ignore
		};

		content.AddChild(JarredInventorySlotView.CreatePotionContent(
			slotSize,
			stack.Name,
			stack.ItemId,
			stack.Quantity,
			profile.CreateJarredLayout(stack.HasActiveRisk
				? new Color(0.58f, 0.05f, 0.04f, 1.0f)
				: null)));

		slot.AddChild(content);
		slot.AddChild(hoverOutline);
		return slot;
	}

	private InventorySlotLayoutProfile GetPotionSlotProfile()
	{
		if (_slotLayoutSettings is null)
			_slotLayoutSettings = LoadSlotLayoutSettings();

		return _slotLayoutSettings.GetProfile(InventorySlotLayoutKind.PotionInventory);
	}

	private InventorySlotLayoutSettings LoadSlotLayoutSettings(bool forceReload = false)
	{
		var settings = InventorySlotLayoutSettings.Load(SlotLayoutSettingsPath, forceReload);
		settings.EnsureProfiles();
		return settings;
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

	private readonly record struct PotionStack(
		string ItemId,
		string Name,
		string? IconPath,
		int Quantity,
		bool HasActiveRisk);
}
