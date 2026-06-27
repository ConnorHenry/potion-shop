using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class StationShelfInventory : Control
{
	private const float SlotWidth = 104.0f;
	private const float SlotHeight = 160.0f;
	private const float IngredientSlotWidth = 116.0f;
	private const float IngredientSlotHeight = 160.0f;
	private const int IngredientDefaultVisibleSlots = 10;
	private const int ConsumableDefaultVisibleSlots = 4;
	private const string PrepTooltipText = "Right click to prep";

	[Export] public NodePath IngredientSlotsPath = default!;
	[Export] public NodePath ConsumableSlotsPath = default!;
	[Export] public NodePath IngredientPreviousButtonPath = default!;
	[Export] public NodePath IngredientNextButtonPath = default!;
	[Export] public NodePath ConsumablePreviousButtonPath = default!;
	[Export] public NodePath ConsumableNextButtonPath = default!;
	[Export] public NodePath IngredientTraitFilterPath = new("IngredientTraitFilterRow/TraitFilter");
	[Export] public NodePath IngredientClearFilterButtonPath = new("IngredientTraitFilterRow/Clear");
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath IngredientPreparationTrayPath = new("../IngredientPreparationTray");
	[Export] public NodePath ItemDetailPanelPath = new("../StationItemDetailPanel");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public int IngredientVisibleSlots = IngredientDefaultVisibleSlots;
	[Export] public int ConsumableVisibleSlots = ConsumableDefaultVisibleSlots;
	[Export] public string SlotLayoutSettingsPath = InventorySlotLayoutSettings.DefaultResourcePath;

	private GridContainer _ingredientSlots = default!;
	private GridContainer _consumableSlots = default!;
	private Button _ingredientPreviousButton = default!;
	private Button _ingredientNextButton = default!;
	private Button _consumablePreviousButton = default!;
	private Button _consumableNextButton = default!;
	private OptionButton? _ingredientTraitFilter;
	private Button? _ingredientClearFilterButton;
	private BrewPanel _brewPanel = default!;
	private IngredientPreparationTray? _ingredientPreparationTray;
	private StationItemDetailPanel? _itemDetailPanel;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private InventorySlotLayoutSettings _slotLayoutSettings = default!;
	private int _ingredientPage;
	private int _consumablePage;
	private string? _activeIngredientTraitFilter;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"StationShelfInventory: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"StationShelfInventory: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var brewPanel = GetNodeOrNull<BrewPanel>(BrewPanelPath);
		if (brewPanel is null)
		{
			GD.PushError($"StationShelfInventory: BrewPanel was not found at '{BrewPanelPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewPanel = brewPanel;
		_slotLayoutSettings = LoadSlotLayoutSettings();
		_ingredientPreparationTray = GetNodeOrNull<IngredientPreparationTray>(IngredientPreparationTrayPath);
		if (_ingredientPreparationTray is null)
			GD.PushError($"StationShelfInventory: IngredientPreparationTray was not found at '{IngredientPreparationTrayPath}'.");
		_itemDetailPanel = GetNodeOrNull<StationItemDetailPanel>(ItemDetailPanelPath);
		if (_itemDetailPanel is null)
			GD.PushError($"StationShelfInventory: StationItemDetailPanel was not found at '{ItemDetailPanelPath}'.");

		var ingredientSlots = NodeLookup.GetRequiredNodeOrNull<GridContainer>(
			this,
			IngredientSlotsPath,
			nameof(StationShelfInventory),
			nameof(IngredientSlotsPath));
		var consumableSlots = NodeLookup.GetRequiredNodeOrNull<GridContainer>(
			this,
			ConsumableSlotsPath,
			nameof(StationShelfInventory),
			nameof(ConsumableSlotsPath));
		var ingredientPreviousButton = NodeLookup.GetRequiredNodeOrNull<Button>(
			this,
			IngredientPreviousButtonPath,
			nameof(StationShelfInventory),
			nameof(IngredientPreviousButtonPath));
		var ingredientNextButton = NodeLookup.GetRequiredNodeOrNull<Button>(
			this,
			IngredientNextButtonPath,
			nameof(StationShelfInventory),
			nameof(IngredientNextButtonPath));
		var consumablePreviousButton = NodeLookup.GetRequiredNodeOrNull<Button>(
			this,
			ConsumablePreviousButtonPath,
			nameof(StationShelfInventory),
			nameof(ConsumablePreviousButtonPath));
		var consumableNextButton = NodeLookup.GetRequiredNodeOrNull<Button>(
			this,
			ConsumableNextButtonPath,
			nameof(StationShelfInventory),
			nameof(ConsumableNextButtonPath));
		if (ingredientSlots is null ||
			consumableSlots is null ||
			ingredientPreviousButton is null ||
			ingredientNextButton is null ||
			consumablePreviousButton is null ||
			consumableNextButton is null)
		{
			return;
		}

		_ingredientSlots = ingredientSlots;
		_consumableSlots = consumableSlots;
		_ingredientPreviousButton = ingredientPreviousButton;
		_ingredientNextButton = ingredientNextButton;
		_consumablePreviousButton = consumablePreviousButton;
		_consumableNextButton = consumableNextButton;
		_ingredientTraitFilter = GetNodeOrNull<OptionButton>(IngredientTraitFilterPath);
		_ingredientClearFilterButton = GetNodeOrNull<Button>(IngredientClearFilterButtonPath);
		MouseFilter = MouseFilterEnum.Ignore;
		_ingredientPreviousButton.Pressed += ShowPreviousIngredientPage;
		_ingredientNextButton.Pressed += ShowNextIngredientPage;
		_consumablePreviousButton.Pressed += ShowPreviousConsumablePage;
		_consumableNextButton.Pressed += ShowNextConsumablePage;
		if (_ingredientTraitFilter is not null)
			_ingredientTraitFilter.ItemSelected += OnIngredientTraitSelected;
		if (_ingredientClearFilterButton is not null)
			_ingredientClearFilterButton.Pressed += ClearIngredientTraitFilter;
		_gameState.Changed += Refresh;

		Refresh();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_ingredientPreviousButton is not null)
			_ingredientPreviousButton.Pressed -= ShowPreviousIngredientPage;
		if (_ingredientNextButton is not null)
			_ingredientNextButton.Pressed -= ShowNextIngredientPage;
		if (_consumablePreviousButton is not null)
			_consumablePreviousButton.Pressed -= ShowPreviousConsumablePage;
		if (_consumableNextButton is not null)
			_consumableNextButton.Pressed -= ShowNextConsumablePage;
		if (_ingredientTraitFilter is not null)
			_ingredientTraitFilter.ItemSelected -= OnIngredientTraitSelected;
		if (_ingredientClearFilterButton is not null)
			_ingredientClearFilterButton.Pressed -= ClearIngredientTraitFilter;
	}

	public void Refresh()
	{
		if (_gameState is null || _itemCatalog is null || _ingredientSlots is null || _consumableSlots is null)
			return;

		var ingredientStacks = BuildVisibleIngredientStacks(refreshTraitOptions: true);
		var consumableStacks = BuildShelfStacks(includeIngredients: false);
		var ingredientVisibleSlots = GetSafeVisibleSlotCount(IngredientVisibleSlots, IngredientDefaultVisibleSlots);
		var consumableVisibleSlots = GetSafeVisibleSlotCount(ConsumableVisibleSlots, ConsumableDefaultVisibleSlots);

		_ingredientPage = ClampPage(_ingredientPage, ingredientStacks.Count, ingredientVisibleSlots);
		_consumablePage = ClampPage(_consumablePage, consumableStacks.Count, consumableVisibleSlots);

		RenderPage(_ingredientSlots, ingredientStacks, _ingredientPage, ingredientVisibleSlots, connectIngredientRequest: true);
		RenderPage(_consumableSlots, consumableStacks, _consumablePage, consumableVisibleSlots, connectIngredientRequest: false);
		UpdatePageButtons(ingredientStacks.Count, ingredientVisibleSlots, _ingredientPage, _ingredientPreviousButton, _ingredientNextButton);
		UpdatePageButtons(consumableStacks.Count, consumableVisibleSlots, _consumablePage, _consumablePreviousButton, _consumableNextButton);
	}

	public void RefreshSlotLayoutSettings()
	{
		_slotLayoutSettings = LoadSlotLayoutSettings(forceReload: true);
		Refresh();
	}

	public Control? GetVisibleIngredientSlot(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId) || _ingredientSlots is null)
			return null;

		foreach (var child in _ingredientSlots.GetChildren())
		{
			if (child is not InventoryItemSlot slot)
				continue;
			if (!slot.Visible)
				continue;
			if (string.Equals(slot.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
				return slot;
		}

		return null;
	}

	private List<ShelfStack> BuildVisibleIngredientStacks(bool refreshTraitOptions)
	{
		var ingredientStacks = BuildShelfStacks(includeIngredients: true);
		if (refreshTraitOptions)
			RefreshIngredientTraitFilterOptions();

		return ApplyIngredientTraitFilter(ingredientStacks);
	}

	private void RefreshIngredientTraitFilterOptions()
	{
		if (_ingredientTraitFilter is null)
		{
			_activeIngredientTraitFilter = null;
			UpdateIngredientTraitClearButtonVisibility();
			return;
		}

		var traitNames = BuildKnownIngredientBookTraitNames();
		ItemFilterUtilities.RefreshFilterOptions(_ingredientTraitFilter, traitNames, "Trait", ref _activeIngredientTraitFilter);
		UpdateIngredientTraitClearButtonVisibility();
	}

	private List<string> BuildKnownIngredientBookTraitNames()
	{
		var traitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var knownIngredientId in _gameState.KnownIngredients)
		{
			if (!_itemCatalog.TryGetItem(knownIngredientId, out var item))
				continue;
			if (!_itemCatalog.IsIngredient(knownIngredientId) || item.Treatment is not null)
				continue;

			AddIngredientBookTraitNames(item, traitNames);
		}

		var sortedTraitNames = new List<string>(traitNames);
		sortedTraitNames.Sort(StringComparer.OrdinalIgnoreCase);
		return sortedTraitNames;
	}

	private List<ShelfStack> ApplyIngredientTraitFilter(List<ShelfStack> ingredientStacks)
	{
		if (_ingredientTraitFilter is null || string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
			return ingredientStacks;

		var filteredStacks = new List<ShelfStack>();
		foreach (var stack in ingredientStacks)
		{
			if (!ShelfStackMatchesIngredientTraitFilter(stack.ItemId))
				continue;

			filteredStacks.Add(stack);
		}

		return filteredStacks;
	}

	private bool ShelfStackMatchesIngredientTraitFilter(string itemId)
	{
		if (string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
			return true;

		return TryGetKnownIngredientBookItem(itemId, out var bookItem) &&
			ItemHasIngredientBookTrait(bookItem, _activeIngredientTraitFilter);
	}

	private bool TryGetKnownIngredientBookItem(string itemId, out ItemDef bookItem)
	{
		bookItem = default!;
		if (string.IsNullOrWhiteSpace(itemId))
			return false;

		var ingredientBookItemId = itemId;
		if (_itemCatalog.TryGetPreparedIngredientInfo(itemId, out var preparedBaseIngredientId, out _))
		{
			ingredientBookItemId = preparedBaseIngredientId;
		}
		else if (_itemCatalog.TryGetItem(itemId, out var item) && item.Treatment is not null)
		{
			var baseItemId = item.Treatment.BaseItemId;
			if (string.IsNullOrWhiteSpace(baseItemId))
				return false;
			if (_itemCatalog.TryGetPreparedIngredientInfo(baseItemId, out var treatedPreparedBaseIngredientId, out _))
				ingredientBookItemId = treatedPreparedBaseIngredientId;
			else
				ingredientBookItemId = baseItemId;
		}

		return _gameState.KnowsIngredient(ingredientBookItemId) &&
			_itemCatalog.TryGetItem(ingredientBookItemId, out bookItem) &&
			_itemCatalog.IsIngredient(ingredientBookItemId) &&
			bookItem.Treatment is null;
	}

	private void AddIngredientBookTraitNames(ItemDef item, HashSet<string> traitNames)
	{
		if (!HasPreparationStats(item))
		{
			AddPositiveTraitNames(item.Traits, traitNames);
			return;
		}

		if (item.Preparations is null)
			return;

		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (!_gameState.KnowsIngredientPreparation(item.Id, option.Id))
				continue;
			if (!item.Preparations.TryGetValue(option.Id, out var preparation))
				continue;
			if (preparation is null || preparation.Traits is null)
				continue;

			AddPositiveTraitNames(preparation.Traits, traitNames);
		}
	}

	private bool ItemHasIngredientBookTrait(ItemDef item, string traitName)
	{
		if (string.IsNullOrWhiteSpace(traitName))
			return false;

		if (!HasPreparationStats(item))
			return DictionaryHasPositiveValue(item.Traits, traitName);

		if (item.Preparations is null)
			return false;

		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (!_gameState.KnowsIngredientPreparation(item.Id, option.Id))
				continue;
			if (!item.Preparations.TryGetValue(option.Id, out var preparation) || preparation is null)
				continue;
			if (DictionaryHasPositiveValue(preparation.Traits, traitName))
				return true;
		}

		return false;
	}

	private static bool HasPreparationStats(ItemDef item)
	{
		return item.Preparations is not null && item.Preparations.Count > 0;
	}

	private static void AddPositiveTraitNames(Dictionary<string, int>? values, HashSet<string> traitNames)
	{
		if (values is null)
			return;

		foreach (var trait in values)
		{
			if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
				continue;

			traitNames.Add(trait.Key);
		}
	}

	private static bool DictionaryHasPositiveValue(Dictionary<string, int>? values, string key)
	{
		if (values is null || string.IsNullOrWhiteSpace(key))
			return false;

		foreach (var pair in values)
		{
			if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
				continue;

			return pair.Value > 0;
		}

		return false;
	}

	private List<ShelfStack> BuildShelfStacks(bool includeIngredients)
	{
		var stacks = new List<ShelfStack>();

		foreach (var stack in _gameState.Inventory)
		{
			if (stack.Value <= 0)
				continue;
			if (!_itemCatalog.TryGetItem(stack.Key, out var item))
				continue;

			var isMatchingType = includeIngredients
				? _itemCatalog.IsIngredient(stack.Key)
				: _itemCatalog.IsConsumable(stack.Key);
			if (!isMatchingType)
				continue;

			stacks.Add(new ShelfStack(stack.Key, BuildShelfDisplayName(stack.Key, item, includeIngredients), item.IconPath, stack.Value));
		}

		stacks.Sort((left, right) =>
		{
			var nameCompare = string.Compare(left.Name, right.Name, System.StringComparison.OrdinalIgnoreCase);
			return nameCompare != 0
				? nameCompare
				: string.Compare(left.ItemId, right.ItemId, System.StringComparison.OrdinalIgnoreCase);
		});
		return stacks;
	}

	private string BuildShelfDisplayName(string itemId, ItemDef item, bool includeIngredients)
	{
		if (!includeIngredients)
			return item.Name;
		if (!_itemCatalog.TryGetPreparedIngredientInfo(itemId, out _, out var preparationId))
			return item.Name;

		var preparationName = IngredientPreparationCatalog.GetDisplayName(preparationId);
		if (string.IsNullOrWhiteSpace(preparationName) || NameIncludesPreparation(item.Name, preparationName))
			return item.Name;

		return $"{item.Name} ({preparationName})";
	}

	private static bool NameIncludesPreparation(string itemName, string preparationName)
	{
		return itemName.Contains($"({preparationName})", StringComparison.OrdinalIgnoreCase) ||
			itemName.Contains($"[{preparationName}]", StringComparison.OrdinalIgnoreCase);
	}

	private void RenderPage(
		GridContainer container,
		IReadOnlyList<ShelfStack> stacks,
		int page,
		int visibleSlotCount,
		bool connectIngredientRequest)
	{
		InventorySlotVisuals.ClearChildren(container);

		var startIndex = page * visibleSlotCount;
		var endIndex = Math.Min(stacks.Count, startIndex + visibleSlotCount);
		for (var i = startIndex; i < endIndex; i++)
			container.AddChild(CreateShelfSlot(stacks[i], connectIngredientRequest));
	}

	private InventoryItemSlot CreateShelfSlot(ShelfStack stack, bool connectIngredientRequest)
	{
		var profile = GetSlotProfile(connectIngredientRequest);
		var slotSize = profile.ResolveSlotSize(connectIngredientRequest
			? new Vector2(IngredientSlotWidth, IngredientSlotHeight)
			: new Vector2(SlotWidth, SlotHeight));
		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = false,
			TooltipText = connectIngredientRequest ? PrepTooltipText : stack.Name,
			ItemId = stack.ItemId,
			ItemName = stack.Name,
			IconPath = stack.IconPath,
			Quantity = stack.Quantity
		};
		var normalStyle = InventorySlotVisuals.CreateSlotStyleBox(
			new Color(0.08f, 0.055f, 0.035f, 0.08f),
			new Color(0.36f, 0.24f, 0.13f, 0.16f),
			cornerRadius: 5);
		slot.AddThemeStyleboxOverride("normal", normalStyle);
		slot.AddThemeStyleboxOverride("hover", normalStyle);
		slot.AddThemeStyleboxOverride("pressed", normalStyle);
		slot.AddThemeStyleboxOverride("disabled", InventorySlotVisuals.CreateSlotStyleBox(
			new Color(0.05f, 0.04f, 0.034f, 0.12f),
			new Color(0.22f, 0.17f, 0.12f, 0.22f),
			cornerRadius: 5));
		slot.SlotActivated += ShowItemDetail;
		if (connectIngredientRequest)
			slot.IngredientRequested += QueueIngredientFromShelf;

		var hoverOutline = InventorySlotVisuals.CreateHoverOutline(
			new Color(0.16f, 0.1f, 0.055f, 0.24f),
			new Color(0.74f, 0.48f, 0.2f, 0.72f),
			cornerRadius: 5,
			borderWidth: 1);
		slot.SetHoverOutline(hoverOutline);

		var content = new Control
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = MouseFilterEnum.Ignore
		};

		content.AddChild(JarredInventorySlotView.CreateContent(
			slotSize,
			stack.Name,
			stack.IconPath,
			stack.Quantity,
			profile.CreateJarredLayout()));
		slot.AddChild(content);
		slot.AddChild(hoverOutline);
		return slot;
	}

	private InventorySlotLayoutProfile GetSlotProfile(bool connectIngredientRequest)
	{
		if (_slotLayoutSettings is null)
			_slotLayoutSettings = LoadSlotLayoutSettings();

		return _slotLayoutSettings.GetProfile(connectIngredientRequest
			? InventorySlotLayoutKind.IngredientShelf
			: InventorySlotLayoutKind.ConsumableShelf);
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
			GD.PushError("StationShelfInventory: Cannot show item detail because StationItemDetailPanel is missing.");
			return;
		}

		_itemDetailPanel.ShowItem(itemId);
		if (_itemDetailPanel.Visible)
			_itemDetailPanel.PositionNearGlobalPoint(GetGlobalMousePosition());
	}

	private void QueueIngredientFromShelf(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;

		if (_itemCatalog.IsPreparedIngredient(itemId))
		{
			if (!_brewPanel.Visible)
				_brewPanel.ShowPanel();

			_brewPanel.TryQueueIngredient(itemId);
			return;
		}

		if (_ingredientPreparationTray is null)
		{
			GD.PushError("StationShelfInventory: Cannot send ingredient to prep station because IngredientPreparationTray is missing.");
			return;
		}

		_ingredientPreparationTray.TrySelectIngredientFromInventory(itemId);
	}

	private void ShowPreviousIngredientPage()
	{
		if (_ingredientPage <= 0)
			return;

		_ingredientPage -= 1;
		Refresh();
	}

	private void ShowNextIngredientPage()
	{
		var visibleSlots = GetSafeVisibleSlotCount(IngredientVisibleSlots, IngredientDefaultVisibleSlots);
		var maxPage = GetMaxPage(BuildVisibleIngredientStacks(refreshTraitOptions: false).Count, visibleSlots);
		if (_ingredientPage >= maxPage)
			return;

		_ingredientPage += 1;
		Refresh();
	}

	private void OnIngredientTraitSelected(long selectedIndex)
	{
		if (_ingredientTraitFilter is null)
			return;
		if (selectedIndex < 0 || selectedIndex >= _ingredientTraitFilter.ItemCount)
			return;

		var selectedTrait = _ingredientTraitFilter.GetItemText((int)selectedIndex);
		if (string.Equals(selectedTrait, "Trait", System.StringComparison.OrdinalIgnoreCase))
		{
			if (string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
			{
				UpdateIngredientTraitClearButtonVisibility();
				return;
			}

			_activeIngredientTraitFilter = null;
			_ingredientPage = 0;
			Refresh();
			return;
		}

		_activeIngredientTraitFilter = string.Equals(_activeIngredientTraitFilter, selectedTrait, System.StringComparison.OrdinalIgnoreCase)
			? null
			: selectedTrait;
		_ingredientPage = 0;
		Refresh();
	}

	private void ClearIngredientTraitFilter()
	{
		if (_ingredientTraitFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			_ingredientTraitFilter.Selected = 0;
			UpdateIngredientTraitClearButtonVisibility();
			return;
		}

		_activeIngredientTraitFilter = null;
		_ingredientPage = 0;
		Refresh();
	}

	private void UpdateIngredientTraitClearButtonVisibility()
	{
		if (_ingredientClearFilterButton is null)
			return;

		var hasActiveFilter = !string.IsNullOrWhiteSpace(_activeIngredientTraitFilter);
		_ingredientClearFilterButton.Visible = hasActiveFilter;
		_ingredientClearFilterButton.Disabled = !hasActiveFilter;
		_ingredientClearFilterButton.MouseFilter = hasActiveFilter ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
	}

	private void ShowPreviousConsumablePage()
	{
		if (_consumablePage <= 0)
			return;

		_consumablePage -= 1;
		Refresh();
	}

	private void ShowNextConsumablePage()
	{
		var visibleSlots = GetSafeVisibleSlotCount(ConsumableVisibleSlots, ConsumableDefaultVisibleSlots);
		var maxPage = GetMaxPage(BuildShelfStacks(includeIngredients: false).Count, visibleSlots);
		if (_consumablePage >= maxPage)
			return;

		_consumablePage += 1;
		Refresh();
	}

	private static void UpdatePageButtons(int totalCount, int visibleSlots, int page, Button previousButton, Button nextButton)
	{
		var maxPage = GetMaxPage(totalCount, visibleSlots);
		var hasOverflow = maxPage > 0;
		previousButton.Visible = hasOverflow;
		nextButton.Visible = hasOverflow;
		previousButton.Disabled = page <= 0;
		nextButton.Disabled = page >= maxPage;
	}

	private static int ClampPage(int page, int totalCount, int visibleSlots)
	{
		return Math.Clamp(page, 0, GetMaxPage(totalCount, visibleSlots));
	}

	private static int GetMaxPage(int totalCount, int visibleSlots)
	{
		if (totalCount <= 0 || visibleSlots <= 0)
			return 0;

		return Math.Max(0, (int)Math.Ceiling(totalCount / (double)visibleSlots) - 1);
	}

	private static int GetSafeVisibleSlotCount(int configuredValue, int fallbackValue)
	{
		return configuredValue > 0 ? configuredValue : fallbackValue;
	}

	private readonly record struct ShelfStack(string ItemId, string Name, string? IconPath, int Quantity);
}
