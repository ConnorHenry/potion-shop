using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class InventoryPanel : Control
{
	private const float SlotWidth = 112.0f;
	private const float SlotHeight = 168.0f;
	private const float IconSize = 62.0f;
	private const float SlotTraitTagHeight = 22.0f;
	private const int SlotNameFontSize = 14;
	private const float InventoryPanelMinimumWidth = 450.0f;
	private static bool ShowInventorySlotTooltips = false;

	[Signal]
	public delegate void ItemDetailShownEventHandler(string itemId);

	[Export] public NodePath PotionsContainerPath = default!;
	[Export] public NodePath ConsumablesContainerPath = default!;
	[Export] public NodePath IngredientsContainerPath = default!;
	[Export] public NodePath PotionsSectionHeaderPath = new("Panel/Margin/VBox/PotionsSectionHeader");
	[Export] public NodePath ConsumablesSectionHeaderPath = new("Panel/Margin/VBox/ConsumablesSectionHeader");
	[Export] public NodePath IngredientsSectionHeaderPath = new("Panel/Margin/VBox/IngredientsSectionHeader");
	[Export] public NodePath PotionsHeaderRowPath = new("Panel/Margin/VBox/PotionsHeaderRow");
	[Export] public NodePath IngredientsHeaderRowPath = new("Panel/Margin/VBox/IngredientsHeaderRow");
	[Export] public NodePath PotionsScrollPath = new("Panel/Margin/VBox/PotionsScroll");
	[Export] public NodePath ConsumablesScrollPath = new("Panel/Margin/VBox/ConsumablesScroll");
	[Export] public NodePath IngredientsScrollPath = new("Panel/Margin/VBox/IngredientsScroll");
	[Export] public NodePath PotionsTraitFilterPath = default!;
	[Export] public NodePath PotionsRiskFilterPath = default!;
	[Export] public NodePath PotionsClearFilterButtonPath = default!;
	[Export] public NodePath IngredientsSortButtonPath = default!;
	[Export] public NodePath IngredientsTypeFilterPath = new NodePath("");
	[Export] public NodePath IngredientsTraitFilterPath = default!;
	[Export] public NodePath IngredientsRiskFilterPath = default!;
	[Export] public NodePath IngredientsClearFilterButtonPath = default!;
	[Export] public NodePath ItemDetailPanelPath = default!;
	[Export] public NodePath ItemDetailImagePath = default!;
	[Export] public NodePath ItemDetailNamePath = default!;
	[Export] public NodePath ItemDetailTypeTagPath = default!;
	[Export] public NodePath ItemDetailPricePath = default!;
	[Export] public NodePath ItemDetailTraitsHeaderPath = default!;
	[Export] public NodePath ItemDetailTraitsPath = default!;
	[Export] public NodePath ItemDetailRisksHeaderPath = default!;
	[Export] public NodePath ItemDetailRisksPath = default!;
	[Export] public NodePath ItemDetailDescriptionPath = default!;
	[Export] public NodePath ItemDetailOwnedPath = default!;
	[Export] public NodePath ItemDetailKnownRecipesPath = default!;
	[Export] public NodePath ItemDetailBrewButtonPath = default!;
	[Export] public NodePath ItemDetailAddToPotionBookButtonPath = default!;
	[Export] public NodePath ItemDetailDiscardButtonPath = default!;
	[Export] public NodePath ItemDetailCloseButtonPath = default!;
	[Export] public NodePath ItemDetailTopCloseButtonPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public NodePath BrewPanelPath = new("../PotionBrewingStationView/BrewPanel");

	private GridContainer _potions = default!;
	private GridContainer _consumables = default!;
	private GridContainer _ingredients = default!;
	private Button? _potionsSectionHeader;
	private Button? _consumablesSectionHeader;
	private Button? _ingredientsSectionHeader;
	private Control? _potionsHeaderRow;
	private Control? _ingredientsHeaderRow;
	private Control? _potionsScroll;
	private Control? _consumablesScroll;
	private Control? _ingredientsScroll;
	private OptionButton? _potionsTraitFilter;
	private OptionButton? _potionsRiskFilter;
	private Button? _potionsClearFilterButton;
	private Button _ingredientsSortButton = default!;
	private OptionButton? _ingredientsTypeFilter;
	private OptionButton? _ingredientsTraitFilter;
	private OptionButton? _ingredientsRiskFilter;
	private Button? _ingredientsClearFilterButton;
	private Control _itemDetailPanel = default!;
	private TextureRect _itemDetailImage = default!;
	private Label _itemDetailName = default!;
	private Label _itemDetailTypeTag = default!;
	private Label _itemDetailPrice = default!;
	private Label _itemDetailTraitsHeader = default!;
	private RichTextLabel _itemDetailTraits = default!;
	private Label _itemDetailRisksHeader = default!;
	private RichTextLabel _itemDetailRisks = default!;
	private RichTextLabel _itemDetailDescription = default!;
	private Label _itemDetailOwned = default!;
	private VBoxContainer _itemDetailKnownRecipes = default!;
	private Button _itemDetailBrewButton = default!;
	private Button? _itemDetailAddToPotionBookButton;
	private Button _itemDetailDiscardButton = default!;
	private Button _itemDetailCloseButton = default!;
	private Button? _itemDetailTopCloseButton;
	private BrewPanel? _brewPanel;
	private string? _currentItemId;
	private bool _itemDetailResizeQueued;
	private bool _ingredientsAscending = true;
	private string? _activePotionTraitFilter;
	private string? _activePotionRiskFilter;
	private string? _activeIngredientTraitFilter;
	private string? _activeIngredientRiskFilter;
	private string? _activeIngredientTypeFilter;
	private bool _potionsCollapsed;
	private bool _consumablesCollapsed;
	private bool _ingredientsCollapsed;
	private PotionInventoryBrewService _brewService = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private ConfirmationDialog? _pendingConsumableDialog;
	private OptionButton? _pendingConsumableChoice;
	private readonly List<string> _pendingConsumableDiscardIds = new();

	private static readonly InventoryItemTagDisplayRule[] ItemTagDisplayRules = InventoryItemTextFormatter.ItemTagDisplayRules;

	private static readonly List<string> IngredientTypeFilterOptions = new()
	{
		"Herb",
		"Liquid",
		"Catalyst"
	};

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"InventoryPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"InventoryPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewService = new PotionInventoryBrewService(_gameState, _itemCatalog);
		ApplyMinimumPanelWidth();

		_potions = GetNode<GridContainer>(PotionsContainerPath);
		_consumables = GetNode<GridContainer>(ConsumablesContainerPath);
		_ingredients = GetNode<GridContainer>(IngredientsContainerPath);
		_potionsSectionHeader = GetNodeOrNull<Button>(PotionsSectionHeaderPath);
		_consumablesSectionHeader = GetNodeOrNull<Button>(ConsumablesSectionHeaderPath);
		_ingredientsSectionHeader = GetNodeOrNull<Button>(IngredientsSectionHeaderPath);
		_potionsHeaderRow = GetNodeOrNull<Control>(PotionsHeaderRowPath);
		_ingredientsHeaderRow = GetNodeOrNull<Control>(IngredientsHeaderRowPath);
		_potionsScroll = GetNodeOrNull<Control>(PotionsScrollPath);
		_consumablesScroll = GetNodeOrNull<Control>(ConsumablesScrollPath);
		_ingredientsScroll = GetNodeOrNull<Control>(IngredientsScrollPath);
		_potionsTraitFilter = GetNodeOrNull<OptionButton>(PotionsTraitFilterPath);
		_potionsRiskFilter = GetNodeOrNull<OptionButton>(PotionsRiskFilterPath);
		_potionsClearFilterButton = GetNodeOrNull<Button>(PotionsClearFilterButtonPath);
		_ingredientsSortButton = GetNode<Button>(IngredientsSortButtonPath);
		var hasIngredientsTypeFilterPath = IngredientsTypeFilterPath is not null && !IngredientsTypeFilterPath.IsEmpty;
		_ingredientsTypeFilter = !hasIngredientsTypeFilterPath
			? GetNodeOrNull<OptionButton>(new NodePath("Panel/Margin/VBox/IngredientsHeaderRow/TypeFilter"))
			: GetNodeOrNull<OptionButton>(IngredientsTypeFilterPath);
		_ingredientsTraitFilter = GetNodeOrNull<OptionButton>(IngredientsTraitFilterPath);
		_ingredientsRiskFilter = GetNodeOrNull<OptionButton>(IngredientsRiskFilterPath);
		_ingredientsClearFilterButton = GetNodeOrNull<Button>(IngredientsClearFilterButtonPath);
		_itemDetailPanel = GetNode<Control>(ItemDetailPanelPath);
		_itemDetailImage = GetNode<TextureRect>(ItemDetailImagePath);
		_itemDetailName = GetNode<Label>(ItemDetailNamePath);
		_itemDetailTypeTag = GetNode<Label>(ItemDetailTypeTagPath);
		_itemDetailPrice = GetNode<Label>(ItemDetailPricePath);
		_itemDetailTraitsHeader = GetNode<Label>(ItemDetailTraitsHeaderPath);
		_itemDetailTraits = GetNode<RichTextLabel>(ItemDetailTraitsPath);
		_itemDetailRisksHeader = GetNode<Label>(ItemDetailRisksHeaderPath);
		_itemDetailRisks = GetNode<RichTextLabel>(ItemDetailRisksPath);
		_itemDetailDescription = GetNode<RichTextLabel>(ItemDetailDescriptionPath);
		_itemDetailOwned = GetNode<Label>(ItemDetailOwnedPath);
		_itemDetailKnownRecipes = GetNode<VBoxContainer>(ItemDetailKnownRecipesPath);
		_itemDetailDescription.BbcodeEnabled = true;
		_itemDetailBrewButton = GetNode<Button>(ItemDetailBrewButtonPath);
		_itemDetailAddToPotionBookButton = ItemDetailAddToPotionBookButtonPath is null || ItemDetailAddToPotionBookButtonPath.IsEmpty
			? GetNodeOrNull<Button>(new NodePath("../InventoryItemDetail/Panel/Margin/VBox/Actions/AddToBook"))
			: GetNodeOrNull<Button>(ItemDetailAddToPotionBookButtonPath);
		_itemDetailDiscardButton = GetNode<Button>(ItemDetailDiscardButtonPath);
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);
		_itemDetailTopCloseButton = ItemDetailTopCloseButtonPath is null || ItemDetailTopCloseButtonPath.IsEmpty
			? null
			: GetNodeOrNull<Button>(ItemDetailTopCloseButtonPath);
		_brewPanel = GetNodeOrNull<BrewPanel>(BrewPanelPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.ZIndex = 2000;
		CreatePendingConsumableDialog();
		if (_potionsSectionHeader is not null)
			_potionsSectionHeader.Pressed += TogglePotionsSection;
		if (_consumablesSectionHeader is not null)
			_consumablesSectionHeader.Pressed += ToggleConsumablesSection;
		if (_ingredientsSectionHeader is not null)
			_ingredientsSectionHeader.Pressed += ToggleIngredientsSection;
		_ingredientsSortButton.Pressed += ToggleIngredientsSort;
		if (_potionsTraitFilter is not null)
			_potionsTraitFilter.ItemSelected += OnPotionTraitSelected;
		if (_potionsRiskFilter is not null)
			_potionsRiskFilter.ItemSelected += OnPotionRiskSelected;
		if (_potionsClearFilterButton is not null)
			_potionsClearFilterButton.Pressed += ClearPotionFilters;
		if (_ingredientsTraitFilter is not null)
			_ingredientsTraitFilter.ItemSelected += OnIngredientTraitSelected;
		if (_ingredientsTypeFilter is not null)
			_ingredientsTypeFilter.ItemSelected += OnIngredientTypeSelected;
		if (_ingredientsRiskFilter is not null)
			_ingredientsRiskFilter.ItemSelected += OnIngredientRiskSelected;
		if (_ingredientsClearFilterButton is not null)
			_ingredientsClearFilterButton.Pressed += ClearIngredientFilters;
		_itemDetailBrewButton.Pressed += TryUseSelectedItem;
		if (_itemDetailAddToPotionBookButton is not null)
			_itemDetailAddToPotionBookButton.Pressed += TryAddSelectedPotionToBook;
		_itemDetailDiscardButton.Pressed += TryDiscardSelectedPotion;
		_itemDetailCloseButton.Pressed += HideItemDetail;
		if (_itemDetailTopCloseButton is not null)
			_itemDetailTopCloseButton.Pressed += HideItemDetail;
		_gameState.Changed += Refresh;

		Visible = true;
		_itemDetailPanel.Visible = false;
		UpdateClearFilterButtonVisibility();
		UpdateSortButtonLabels();
		RefreshIngredientTypeFilterOptions();
		Refresh();
		UpdatePendingConsumableDialog();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_potionsSectionHeader is not null)
			_potionsSectionHeader.Pressed -= TogglePotionsSection;
		if (_consumablesSectionHeader is not null)
			_consumablesSectionHeader.Pressed -= ToggleConsumablesSection;
		if (_ingredientsSectionHeader is not null)
			_ingredientsSectionHeader.Pressed -= ToggleIngredientsSection;
		if (_ingredientsSortButton is not null)
			_ingredientsSortButton.Pressed -= ToggleIngredientsSort;
		if (_potionsTraitFilter is not null)
			_potionsTraitFilter.ItemSelected -= OnPotionTraitSelected;
		if (_potionsRiskFilter is not null)
			_potionsRiskFilter.ItemSelected -= OnPotionRiskSelected;
		if (_potionsClearFilterButton is not null)
			_potionsClearFilterButton.Pressed -= ClearPotionFilters;
		if (_ingredientsTraitFilter is not null)
			_ingredientsTraitFilter.ItemSelected -= OnIngredientTraitSelected;
		if (_ingredientsTypeFilter is not null)
			_ingredientsTypeFilter.ItemSelected -= OnIngredientTypeSelected;
		if (_ingredientsRiskFilter is not null)
			_ingredientsRiskFilter.ItemSelected -= OnIngredientRiskSelected;
		if (_ingredientsClearFilterButton is not null)
			_ingredientsClearFilterButton.Pressed -= ClearIngredientFilters;
		if (_itemDetailBrewButton is not null)
			_itemDetailBrewButton.Pressed -= TryUseSelectedItem;
		if (_itemDetailAddToPotionBookButton is not null)
			_itemDetailAddToPotionBookButton.Pressed -= TryAddSelectedPotionToBook;
		if (_itemDetailDiscardButton is not null)
			_itemDetailDiscardButton.Pressed -= TryDiscardSelectedPotion;
		if (_itemDetailCloseButton is not null)
			_itemDetailCloseButton.Pressed -= HideItemDetail;
		if (_itemDetailTopCloseButton is not null)
			_itemDetailTopCloseButton.Pressed -= HideItemDetail;
	}

	public Control? GetVisibleItemSlot(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return null;

		return FindVisibleItemSlot(_potions, itemId) ??
			FindVisibleItemSlot(_consumables, itemId) ??
			FindVisibleItemSlot(_ingredients, itemId);
	}

	public void ClearPotionFiltersForTutorial()
	{
		if (string.IsNullOrWhiteSpace(_activePotionTraitFilter) && string.IsNullOrWhiteSpace(_activePotionRiskFilter))
			return;

		_activePotionTraitFilter = null;
		_activePotionRiskFilter = null;
		Refresh();
	}

	public void ClearIngredientFiltersForTutorial()
	{
		if (string.IsNullOrWhiteSpace(_activeIngredientTypeFilter) &&
			string.IsNullOrWhiteSpace(_activeIngredientTraitFilter) &&
			string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			return;
		}

		_activeIngredientTypeFilter = null;
		_activeIngredientTraitFilter = null;
		_activeIngredientRiskFilter = null;
		Refresh();
	}

	public Control? GetItemDetailPanel()
	{
		return _itemDetailPanel;
	}

	public Control? GetItemDetailFrame()
	{
		return _itemDetailPanel.GetNodeOrNull<Control>("Panel") ?? _itemDetailPanel;
	}

	public Button? GetItemDetailBrewButton()
	{
		return _itemDetailBrewButton;
	}

	private void ToggleIngredientsSort()
	{
		_ingredientsAscending = !_ingredientsAscending;
		UpdateSortButtonLabels();
		Refresh();
	}

	private void TogglePotionsSection()
	{
		_potionsCollapsed = !_potionsCollapsed;
		UpdateSectionVisibility();
	}

	private void ToggleConsumablesSection()
	{
		_consumablesCollapsed = !_consumablesCollapsed;
		UpdateSectionVisibility();
	}

	private void ToggleIngredientsSection()
	{
		_ingredientsCollapsed = !_ingredientsCollapsed;
		UpdateSectionVisibility();
	}

	private void Refresh()
	{
		if (_potions is null || _consumables is null || _ingredients is null)
			return;

		foreach (var child in _potions.GetChildren())
			child.QueueFree();
		foreach (var child in _consumables.GetChildren())
			child.QueueFree();
		foreach (var child in _ingredients.GetChildren())
			child.QueueFree();

		if (_gameState.Inventory.Count == 0)
			_ingredients.AddChild(new Label { Text = "Empty" });

		var potionStacks = _gameState.Inventory.Where(x => IsPotion(x.Key)).ToList();
		var consumableStacks = _gameState.Inventory.Where(x => IsConsumable(x.Key)).ToList();
		var ingredientStacks = _gameState.Inventory.Where(x => !IsPotion(x.Key) && !IsConsumable(x.Key)).ToList();
		var potionTraitNames = ItemFilterUtilities.BuildTopTraitNames(potionStacks.Select(x => x.Key), 3, _itemCatalog);
		var potionRiskNames = ItemFilterUtilities.BuildRiskNames(potionStacks.Select(x => x.Key), _itemCatalog);
		var ingredientTraitNames = ItemFilterUtilities.BuildTraitNames(ingredientStacks.Select(x => x.Key), _itemCatalog);
		var ingredientRiskNames = ItemFilterUtilities.BuildRiskNames(ingredientStacks.Select(x => x.Key), _itemCatalog);

		if (!string.IsNullOrWhiteSpace(_activePotionTraitFilter))
		{
			var activeTraitExists = potionTraitNames.Any(trait =>
				string.Equals(trait, _activePotionTraitFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeTraitExists)
				_activePotionTraitFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activePotionRiskFilter))
		{
			var activeRiskExists = potionRiskNames.Any(risk =>
				string.Equals(risk, _activePotionRiskFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeRiskExists)
				_activePotionRiskFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			var activeTraitExists = ingredientTraitNames.Any(trait =>
				string.Equals(trait, _activeIngredientTraitFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeTraitExists)
				_activeIngredientTraitFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientTypeFilter))
		{
			var activeTypeExists = IngredientTypeFilterOptions.Any(type =>
				string.Equals(type, _activeIngredientTypeFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeTypeExists)
				_activeIngredientTypeFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			var activeRiskExists = ingredientRiskNames.Any(risk =>
				string.Equals(risk, _activeIngredientRiskFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeRiskExists)
				_activeIngredientRiskFilter = null;
		}

		var potionStacksToRender = potionStacks;
		if (_potionsTraitFilter is null)
		{
			_activePotionTraitFilter = null;
		}
		if (_potionsRiskFilter is null)
		{
			_activePotionRiskFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activePotionTraitFilter))
		{
			potionStacksToRender = potionStacks.Where(stack => ItemFilterUtilities.ItemHasTrait(stack.Key, _activePotionTraitFilter, _itemCatalog, topCount: 3)).ToList();
		}
		if (!string.IsNullOrWhiteSpace(_activePotionRiskFilter))
		{
			potionStacksToRender = potionStacksToRender.Where(stack => ItemFilterUtilities.ItemHasRisk(stack.Key, _activePotionRiskFilter, _itemCatalog)).ToList();
		}

		foreach (var stack in potionStacksToRender.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
			_potions.AddChild(CreateSlot(stack.Key, stack.Value));

		foreach (var stack in consumableStacks.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
			_consumables.AddChild(CreateSlot(stack.Key, stack.Value));

		var ingredientStacksToRender = ingredientStacks;
		if (_ingredientsTraitFilter is null)
		{
			_activeIngredientTraitFilter = null;
		}
		if (_ingredientsTypeFilter is null)
		{
			_activeIngredientTypeFilter = null;
		}
		if (_ingredientsRiskFilter is null)
		{
			_activeIngredientRiskFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientTypeFilter))
		{
			ingredientStacksToRender = ingredientStacks
				.Where(stack => ItemHasIngredientType(stack.Key, _activeIngredientTypeFilter))
				.ToList();
		}
		if (!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			ingredientStacksToRender = ingredientStacksToRender.Where(stack => ItemFilterUtilities.ItemHasTrait(stack.Key, _activeIngredientTraitFilter, _itemCatalog)).ToList();
		}
		if (!string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			ingredientStacksToRender = ingredientStacksToRender.Where(stack => ItemFilterUtilities.ItemHasRisk(stack.Key, _activeIngredientRiskFilter, _itemCatalog)).ToList();
		}

		if (_ingredientsAscending)
		{
			foreach (var stack in ingredientStacksToRender.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
				_ingredients.AddChild(CreateSlot(stack.Key, stack.Value));
		}
		else
		{
			foreach (var stack in ingredientStacksToRender.OrderByDescending(x => ItemName(x.Key)).ThenByDescending(x => x.Key))
				_ingredients.AddChild(CreateSlot(stack.Key, stack.Value));
		}

		UpdateSectionHeaders(potionStacksToRender.Count, consumableStacks.Count, ingredientStacksToRender.Count);
		UpdateSectionVisibility();
		ItemFilterUtilities.RefreshFilterOptions(_potionsTraitFilter, potionTraitNames, "Trait", ref _activePotionTraitFilter);
		ItemFilterUtilities.RefreshFilterOptions(_potionsRiskFilter, potionRiskNames, "Risk", ref _activePotionRiskFilter);
		RefreshIngredientTypeFilterOptions();
		ItemFilterUtilities.RefreshFilterOptions(_ingredientsTraitFilter, ingredientTraitNames, "Trait", ref _activeIngredientTraitFilter);
		ItemFilterUtilities.RefreshFilterOptions(_ingredientsRiskFilter, ingredientRiskNames, "Risk", ref _activeIngredientRiskFilter);
		RefreshCurrentItemDetail();
		UpdateClearFilterButtonVisibility();
		UpdateBrewButtonState();
		UpdatePendingConsumableDialog();
	}

	private void RefreshIngredientTypeFilterOptions()
	{
		ItemFilterUtilities.RefreshFilterOptions(_ingredientsTypeFilter, IngredientTypeFilterOptions, "Type", ref _activeIngredientTypeFilter);
	}

	private void UpdateSortButtonLabels()
	{
		_ingredientsSortButton.Text = _ingredientsAscending ? "A-Z" : "Z-A";
	}

	private void UpdateSectionHeaders(int visiblePotionCount, int visibleConsumableCount, int visibleIngredientCount)
	{
		if (_potionsSectionHeader is not null)
			_potionsSectionHeader.Text = $"{(_potionsCollapsed ? ">" : "v")} Potions ({visiblePotionCount})";

		if (_consumablesSectionHeader is not null)
			_consumablesSectionHeader.Text = $"{(_consumablesCollapsed ? ">" : "v")} Consumables ({visibleConsumableCount})";

		if (_ingredientsSectionHeader is not null)
			_ingredientsSectionHeader.Text = $"{(_ingredientsCollapsed ? ">" : "v")} Ingredients ({visibleIngredientCount})";
	}

	private void UpdateSectionVisibility()
	{
		if (_potionsHeaderRow is not null)
			_potionsHeaderRow.Visible = !_potionsCollapsed;
		if (_potionsScroll is not null)
			_potionsScroll.Visible = !_potionsCollapsed;

		if (_consumablesScroll is not null)
			_consumablesScroll.Visible = !_consumablesCollapsed;

		if (_ingredientsHeaderRow is not null)
			_ingredientsHeaderRow.Visible = !_ingredientsCollapsed;
		if (_ingredientsScroll is not null)
			_ingredientsScroll.Visible = !_ingredientsCollapsed;
	}

	private void UpdateClearFilterButtonVisibility()
	{
		if (_potionsClearFilterButton is not null)
		{
			var hasActivePotionFilter =
				!string.IsNullOrWhiteSpace(_activePotionTraitFilter) ||
				!string.IsNullOrWhiteSpace(_activePotionRiskFilter);
			ApplyClearFilterButtonState(_potionsClearFilterButton, hasActivePotionFilter);
		}

		if (_ingredientsClearFilterButton is not null)
		{
			var hasActiveIngredientFilter =
				!string.IsNullOrWhiteSpace(_activeIngredientTypeFilter) ||
				!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter) ||
				!string.IsNullOrWhiteSpace(_activeIngredientRiskFilter);
			ApplyClearFilterButtonState(_ingredientsClearFilterButton, hasActiveIngredientFilter);
		}
	}

	private void ApplyMinimumPanelWidth()
	{
		CustomMinimumSize = new Vector2(InventoryPanelMinimumWidth, CustomMinimumSize.Y);

		if (Size.X < InventoryPanelMinimumWidth)
			Size = new Vector2(InventoryPanelMinimumWidth, Size.Y);
	}

	private static void ApplyClearFilterButtonState(Button button, bool isActive)
	{
		button.Visible = true;
		button.Disabled = !isActive;
		button.MouseFilter = isActive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		button.Modulate = isActive ? Colors.White : new Color(1f, 1f, 1f, 0f);
	}

	private void CreatePendingConsumableDialog()
	{
		_pendingConsumableDialog = new ConfirmationDialog
		{
			Title = "Consumables Full",
			DialogText = "Consumable inventory is full."
		};
		_pendingConsumableChoice = new OptionButton
		{
			CustomMinimumSize = new Vector2(0, 36),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};

		_pendingConsumableDialog.AddChild(_pendingConsumableChoice);
		_pendingConsumableDialog.GetOkButton().Text = "Discard and Accept";
		_pendingConsumableDialog.GetCancelButton().Text = "Decline";
		_pendingConsumableDialog.Confirmed += OnPendingConsumableConfirmed;
		_pendingConsumableDialog.GetCancelButton().Pressed += OnPendingConsumableDeclined;
		AddChild(_pendingConsumableDialog);
	}

	private void UpdatePendingConsumableDialog()
	{
		if (_pendingConsumableDialog is null || _pendingConsumableChoice is null)
			return;

		if (!_gameState.HasPendingConsumableGrant)
		{
			if (_pendingConsumableDialog.Visible)
				_pendingConsumableDialog.Hide();

			return;
		}

		_pendingConsumableDiscardIds.Clear();
		_pendingConsumableChoice.Clear();
		foreach (var stack in _gameState.Inventory.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
		{
			if (stack.Value <= 0)
				continue;
			if (!IsConsumable(stack.Key))
				continue;

			_pendingConsumableDiscardIds.Add(stack.Key);
			_pendingConsumableChoice.AddItem($"{ItemName(stack.Key)} x{stack.Value}");
		}

		if (_pendingConsumableDiscardIds.Count == 0)
		{
			_gameState.DeclinePendingConsumableGrant();
			return;
		}

		_pendingConsumableChoice.Selected = 0;
		_pendingConsumableDialog.DialogText =
			$"Accept {_gameState.PendingConsumableQuantity}x {ItemName(_gameState.PendingConsumableItemId)} by discarding one existing consumable stack?";
		if (!_pendingConsumableDialog.Visible)
			_pendingConsumableDialog.PopupCentered(new Vector2I(420, 220));
	}

	private void OnPendingConsumableConfirmed()
	{
		if (_pendingConsumableChoice is null)
			return;

		var selectedIndex = _pendingConsumableChoice.Selected;
		if (selectedIndex < 0 || selectedIndex >= _pendingConsumableDiscardIds.Count)
		{
			CursorToast.Show(this, "Choose a consumable to discard.");
			UpdatePendingConsumableDialog();
			return;
		}

		var discardItemId = _pendingConsumableDiscardIds[selectedIndex];
		if (!_gameState.TryAcceptPendingConsumableByDiscarding(discardItemId, out var error))
		{
			CursorToast.Show(this, error);
			UpdatePendingConsumableDialog();
		}
	}

	private void OnPendingConsumableDeclined()
	{
		_gameState.DeclinePendingConsumableGrant();
	}

	private void OnIngredientTraitSelected(long selectedIndex)
	{
		if (_ingredientsTraitFilter is null)
			return;

		HandleFilterSelected(_ingredientsTraitFilter, selectedIndex, "Trait", ref _activeIngredientTraitFilter);
	}

	private void OnIngredientTypeSelected(long selectedIndex)
	{
		if (_ingredientsTypeFilter is null)
			return;

		HandleFilterSelected(_ingredientsTypeFilter, selectedIndex, "Type", ref _activeIngredientTypeFilter);
	}

	private void OnIngredientRiskSelected(long selectedIndex)
	{
		if (_ingredientsRiskFilter is null)
			return;

		HandleFilterSelected(_ingredientsRiskFilter, selectedIndex, "Risk", ref _activeIngredientRiskFilter);
	}

	private void OnPotionTraitSelected(long selectedIndex)
	{
		if (_potionsTraitFilter is null)
			return;

		HandleFilterSelected(_potionsTraitFilter, selectedIndex, "Trait", ref _activePotionTraitFilter);
	}

	private void OnPotionRiskSelected(long selectedIndex)
	{
		if (_potionsRiskFilter is null)
			return;

		HandleFilterSelected(_potionsRiskFilter, selectedIndex, "Risk", ref _activePotionRiskFilter);
	}

	private void ClearIngredientFilters()
	{
		if (_ingredientsTypeFilter is null && _ingredientsTraitFilter is null && _ingredientsRiskFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activeIngredientTypeFilter) && string.IsNullOrWhiteSpace(_activeIngredientTraitFilter) && string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			if (_ingredientsTypeFilter is not null)
				_ingredientsTypeFilter.Selected = 0;
			if (_ingredientsTraitFilter is not null)
				_ingredientsTraitFilter.Selected = 0;
			if (_ingredientsRiskFilter is not null)
				_ingredientsRiskFilter.Selected = 0;
			UpdateClearFilterButtonVisibility();
			return;
		}

		_activeIngredientTypeFilter = null;
		_activeIngredientTraitFilter = null;
		_activeIngredientRiskFilter = null;
		Refresh();
	}

	private void ClearPotionFilters()
	{
		if (_potionsTraitFilter is null && _potionsRiskFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activePotionTraitFilter) && string.IsNullOrWhiteSpace(_activePotionRiskFilter))
		{
			if (_potionsTraitFilter is not null)
				_potionsTraitFilter.Selected = 0;
			if (_potionsRiskFilter is not null)
				_potionsRiskFilter.Selected = 0;
			UpdateClearFilterButtonVisibility();
			return;
		}

		_activePotionTraitFilter = null;
		_activePotionRiskFilter = null;
		Refresh();
	}

	private void HandleFilterSelected(OptionButton? traitFilter, long selectedIndex, string placeholderLabel, ref string? activeTraitFilter)
	{
		if (traitFilter is null)
			return;

		var selectedTrait = traitFilter.GetItemText((int)selectedIndex);
		if (string.Equals(selectedTrait, placeholderLabel, System.StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrWhiteSpace(activeTraitFilter))
			{
				activeTraitFilter = null;
				Refresh();
			}

			return;
		}

		if (string.Equals(activeTraitFilter, selectedTrait, System.StringComparison.OrdinalIgnoreCase))
		{
			activeTraitFilter = null;
		}
		else
		{
			activeTraitFilter = selectedTrait;
		}

		Refresh();
	}

	private Control CreateSlot(string itemId, int quantity)
	{
		var item = _itemCatalog.TryGetItem(itemId, out var def) ? def : null;
		var itemName = DisplayName(itemId, item?.Name ?? itemId);
		var shouldShowRiskNameColor = IsPotion(itemId) && HasActiveRisk(item);

		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = new Vector2(SlotWidth, SlotHeight),
			TooltipText = GetInventorySlotTooltipText(itemName),
			MouseFilter = MouseFilterEnum.Stop,
			Flat = false,
			ItemId = itemId,
			ItemName = itemName,
			IconPath = item?.IconPath,
			Quantity = quantity
		};
		slot.AddThemeStyleboxOverride("normal", CreateSlotStyleBox(new Color(0.082f, 0.092f, 0.103f, 0.92f), new Color(0.24f, 0.26f, 0.29f, 0.94f)));
		slot.AddThemeStyleboxOverride("hover", CreateSlotStyleBox(new Color(0.11f, 0.125f, 0.142f, 0.96f), new Color(0.34f, 0.37f, 0.41f, 0.98f)));
		slot.AddThemeStyleboxOverride("pressed", CreateSlotStyleBox(new Color(0.06f, 0.069f, 0.079f, 0.98f), new Color(0.19f, 0.21f, 0.23f, 0.98f)));
		slot.AddThemeStyleboxOverride("disabled", CreateSlotStyleBox(new Color(0.07f, 0.078f, 0.088f, 0.75f), new Color(0.18f, 0.2f, 0.22f, 0.78f)));
		slot.SlotActivated += ShowItemDetail;
		slot.IngredientRequested += QueueIngredientFromSlot;

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
			Position = new Vector2((SlotWidth - IconSize) * 0.5f, 28),
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			Size = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		icon.Texture = UiIconLoader.LoadIcon(item?.IconPath);

		var qty = new Label
		{
			Text = quantity.ToString(),
			Position = new Vector2(8, 6),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var hasPrice = item is not null || _gameState.TryGetPotionBasePrice(itemId, out _);
		if (hasPrice)
		{
			var price = new Label
			{
				Text = $"\u00A3{GetItemPrice(itemId, item)}",
				Position = new Vector2(48, 6),
				CustomMinimumSize = new Vector2(56, 0),
				Size = new Vector2(56, 22),
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			price.AddThemeColorOverride("font_color", new Color("FFD700"));
			content.AddChild(price);
		}

		var name = new Label
		{
			Text = itemName,
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = false
		};

		var nameBlock = new Control
		{
			Position = new Vector2(6, 92),
			CustomMinimumSize = new Vector2(SlotWidth - 12, 34),
			Size = new Vector2(SlotWidth - 12, 34),
			MouseFilter = MouseFilterEnum.Ignore
		};

		SplitInventoryName(itemName, out var firstLine, out var secondLine);
		name.Text = firstLine;
		name.Position = new Vector2(0, 0);
		name.CustomMinimumSize = new Vector2(SlotWidth - 12, 0);
		name.Size = new Vector2(SlotWidth - 12, 18);
		name.AddThemeFontSizeOverride("font_size", SlotNameFontSize);
		if (shouldShowRiskNameColor)
			name.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f, 1f));

		nameBlock.AddChild(name);

		if (!string.IsNullOrWhiteSpace(secondLine))
		{
			var secondName = new Label
			{
				Text = secondLine,
				Position = new Vector2(0, 15),
				CustomMinimumSize = new Vector2(SlotWidth - 12, 0),
				Size = new Vector2(SlotWidth - 12, 18),
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				ClipText = false
			};
			secondName.AddThemeFontSizeOverride("font_size", SlotNameFontSize);
			if (shouldShowRiskNameColor)
				secondName.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f, 1f));

			nameBlock.AddChild(secondName);
		}

		content.AddChild(icon);
		content.AddChild(qty);
		content.AddChild(nameBlock);
		var topTraitText = BuildSlotTraitText(item);
		if (!string.IsNullOrWhiteSpace(topTraitText))
			content.AddChild(CreateSlotTraitTag(topTraitText));

		slot.AddChild(content);
		slot.AddChild(hoverOutline);
		return slot;
	}

	private static PanelContainer CreateSlotTraitTag(string text)
	{
		var tag = new PanelContainer
		{
			Position = new Vector2(12, 140),
			CustomMinimumSize = new Vector2(SlotWidth - 24, SlotTraitTagHeight),
			Size = new Vector2(SlotWidth - 24, SlotTraitTagHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};
		tag.AddThemeStyleboxOverride("panel", CreateTraitTagStyleBox());

		var label = new Label
		{
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.58f, 1f));
		label.AddThemeFontSizeOverride("font_size", 13);
		tag.AddChild(label);

		return tag;
	}

	private static string BuildSlotTraitText(ItemDef? item)
	{
		return InventoryItemTextFormatter.BuildSlotTraitText(item);
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

	private static StyleBoxFlat CreateTraitTagStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.18f, 0.1f, 0.82f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = new Color(0.28f, 0.62f, 0.32f, 0.9f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static StyleBoxFlat CreateHoverOutlineStyleBox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, 0f),
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

	private static string GetInventorySlotTooltipText(string itemName)
	{
		if (!ShowInventorySlotTooltips)
			return string.Empty;

		return itemName;
	}

	private static bool HasActiveRisk(ItemDef? item)
	{
		if (item?.Risks is null || item.Risks.Count == 0)
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
				return true;
		}

		return false;
	}

	private void ShowItemDetail(string itemId)
	{
		if (_itemDetailPanel.Visible && string.Equals(_currentItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
		{
			HideItemDetail();
			return;
		}

		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;

		_currentItemId = itemId;
		RefreshCurrentItemDetail();
		_itemDetailPanel.Visible = true;
		_itemDetailPanel.MoveToFront();
		UpdateBrewButtonState();
		QueueResizeItemDetailToContent();
		EmitSignal(SignalName.ItemDetailShown, itemId);
	}

	private void QueueIngredientFromSlot(string itemId)
	{
		if (_brewPanel is null)
			return;

		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;

		if (!IsIngredient(item))
			return;

		if (!_brewPanel.Visible)
			_brewPanel.ShowPanel();

		var quantityBeforeQueue = _gameState.Inventory.GetValueOrDefault(itemId);
		_brewPanel.TryQueueIngredient(itemId);
		var quantityAfterQueue = _gameState.Inventory.GetValueOrDefault(itemId);
		var queuedSuccessfully = quantityAfterQueue < quantityBeforeQueue;
		if (!queuedSuccessfully)
			return;
		if (!_itemDetailPanel.Visible)
			return;
		if (!string.Equals(_currentItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
			return;

		HideItemDetail();
	}

	private void HideItemDetail()
	{
		_currentItemId = null;
		_itemDetailImage.Texture = null;
		_itemDetailTypeTag.Text = string.Empty;
		_itemDetailTypeTag.Visible = false;
		_itemDetailTraitsHeader.Text = "TRAITS";
		_itemDetailRisksHeader.Text = "RISKS";
		_itemDetailTraits.Text = "";
		_itemDetailRisks.Text = "";
		_itemDetailDescription.Text = "";
		_itemDetailPrice.Text = "";
		_itemDetailOwned.Text = "";
		ClearKnownRecipeRows();
		_itemDetailBrewButton.Visible = false;
		_itemDetailBrewButton.Disabled = true;
		HideAddToPotionBookButton();
		_itemDetailDiscardButton.Visible = false;
		_itemDetailDiscardButton.Disabled = true;
		_itemDetailPanel.Visible = false;
	}

	private void RefreshCurrentItemDetail()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

		if (!_itemCatalog.TryGetItem(_currentItemId, out var item))
			return;

		_itemDetailImage.Texture = UiIconLoader.LoadIcon(item.IconPath);
		_itemDetailName.Text = FormatItemDetailName(DisplayName(_currentItemId, item.Name));
		SetItemTypeTag(item);
		_itemDetailOwned.Text = $"Owned: {_gameState.Inventory.GetValueOrDefault(_currentItemId)}";
		_itemDetailPrice.Text = $"Sell Price: \u00A3{GetItemPrice(_currentItemId, item)}";

		if (IsConsumable(_currentItemId))
		{
			_itemDetailTraitsHeader.Text = "EFFECT";
			_itemDetailRisksHeader.Text = "CAN USE ON";
			_itemDetailTraits.Text = BuildConsumableEffectText(item);
			_itemDetailRisks.Text = BuildConsumableGateText(item);
			_itemDetailDescription.Text = item.Description;
		}
		else
		{
			_itemDetailTraitsHeader.Text = "TRAITS";
			_itemDetailRisksHeader.Text = "RISKS";
			_itemDetailTraits.Text = FormatTopStats(item.Traits, 3);
			_itemDetailRisks.Text = IsPotion(_currentItemId)
				? FormatTopStatNames(item.Risks, 3, "None")
				: FormatTopStats(item.Risks, 3, "None");
			_itemDetailDescription.Text = IsPotion(_currentItemId) && item.Treatment is null
				? _brewService.BuildPotionDescriptionText(_currentItemId, item.Description)
				: item.Description;
		}

		RefreshKnownRecipes(_currentItemId, item);
	}

	private void TryAddSelectedPotionToBook()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;
		if (!IsPotion(_currentItemId))
			return;
		if (!_itemCatalog.TryGetItem(_currentItemId, out var item))
			return;
		if (item.Treatment is not null)
			return;
		if (HasActiveRisk(item))
			return;
		if (_gameState.KnowsPotion(_currentItemId))
			return;
		if (!_gameState.TryGetPotionRecipe(_currentItemId, out var ingredientIds) || ingredientIds.Count == 0)
		{
			GD.PushError($"InventoryPanel: Cannot add potion '{_currentItemId}' to the potion book because no recipe is recorded.");
			return;
		}

		_gameState.LearnPotion(_currentItemId);
		RefreshCurrentItemDetail();
		UpdateBrewButtonState();
		QueueResizeItemDetailToContent();
	}

	private void TryUseSelectedItem()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

		if (IsIngredient(_currentItemId))
		{
			if (_brewPanel is null)
			{
				GD.PushError("InventoryPanel: Brew panel was not found.");
				return;
			}

			if (!_brewPanel.Visible)
				_brewPanel.ShowPanel();

			_brewPanel.TryQueueIngredient(_currentItemId);
			HideItemDetail();
			return;
		}

		if (!IsPotion(_currentItemId))
			return;

		if (!_brewService.TryBrewPotion(_currentItemId, out var error))
			CursorToast.Show(this, error);

		UpdateBrewButtonState();
	}

	private void TryDiscardSelectedPotion()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

		var itemId = _currentItemId;
		if (IsConsumable(itemId))
		{
			TrySellSelectedConsumable(itemId);
			return;
		}

		if (!IsPotion(itemId))
			return;

		if (!_gameState.ConsumeItem(itemId, 1))
		{
			GD.PushError($"InventoryPanel: Cannot discard potion '{itemId}' because it is not in inventory.");
			return;
		}

		if (!_gameState.HasItem(itemId, 1))
		{
			HideItemDetail();
			return;
		}

		RefreshCurrentItemDetail();
		UpdateBrewButtonState();
		QueueResizeItemDetailToContent();
	}

	private void TrySellSelectedConsumable(string itemId)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;

		if (!_gameState.ConsumeItem(itemId, 1))
		{
			GD.PushError($"InventoryPanel: Cannot sell consumable '{itemId}' because it is not in inventory.");
			return;
		}

		_gameState.AddGold(GetItemPrice(itemId, item));
		if (!_gameState.HasItem(itemId, 1))
		{
			HideItemDetail();
			return;
		}

		RefreshCurrentItemDetail();
		UpdateBrewButtonState();
		QueueResizeItemDetailToContent();
	}

	private void QueueResizeItemDetailToContent()
	{
		if (_itemDetailResizeQueued)
			return;

		_itemDetailResizeQueued = true;
		Callable.From(ResizeItemDetailToContent).CallDeferred();
	}

	private void ResizeItemDetailToContent()
	{
		_itemDetailResizeQueued = false;

		if (!_itemDetailPanel.Visible)
			return;

		var frame = GetItemDetailFrame();
		if (frame is null)
			return;

		var minimumSize = frame.GetCombinedMinimumSize();
		if (minimumSize.Y <= 0.0f)
			return;

		var width = frame.Size.X > 0.0f ? frame.Size.X : minimumSize.X;
		var height = Mathf.Ceil(minimumSize.Y);
		frame.Size = new Vector2(width, height);
		_itemDetailPanel.Size = new Vector2(_itemDetailPanel.Size.X, height);
	}

	private void UpdateBrewButtonState()
	{
		if (!string.IsNullOrWhiteSpace(_currentItemId) && IsConsumable(_currentItemId))
		{
			_itemDetailBrewButton.Visible = false;
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = "";
			HideAddToPotionBookButton();
			_itemDetailDiscardButton.Text = "Sell";
			_itemDetailDiscardButton.Visible = true;
			_itemDetailDiscardButton.Disabled = !_gameState.HasItem(_currentItemId, 1);
			_itemDetailDiscardButton.TooltipText = _itemDetailDiscardButton.Disabled
				? "No stock available to sell."
				: "Sell one consumable.";
			return;
		}

		if (string.IsNullOrWhiteSpace(_currentItemId) || !IsPotion(_currentItemId))
		{
			if (!string.IsNullOrWhiteSpace(_currentItemId) && IsIngredient(_currentItemId))
			{
				_itemDetailBrewButton.Text = "Add to Brew";
				_itemDetailBrewButton.Visible = true;
				_itemDetailBrewButton.Disabled = !_gameState.HasItem(_currentItemId, 1) || _brewPanel is null;
				_itemDetailBrewButton.TooltipText = _itemDetailBrewButton.Disabled
					? "No stock available to add."
					: "Add this ingredient to the brew panel.";
				_itemDetailDiscardButton.Visible = false;
				_itemDetailDiscardButton.Disabled = true;
				HideAddToPotionBookButton();
				return;
			}

			_itemDetailBrewButton.Visible = false;
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = "";
			HideAddToPotionBookButton();
			_itemDetailDiscardButton.Visible = false;
			_itemDetailDiscardButton.Disabled = true;
			return;
		}

		_itemDetailBrewButton.Text = "Brew";
		_itemDetailBrewButton.Visible = true;
		_itemDetailDiscardButton.Text = "Discard";
		_itemDetailDiscardButton.Visible = true;
		_itemDetailDiscardButton.Disabled = !_gameState.HasItem(_currentItemId, 1);
		_itemDetailDiscardButton.TooltipText = _itemDetailDiscardButton.Disabled
			? "No stock available to discard."
			: "Discard one potion from inventory.";
		UpdateAddToPotionBookButtonState(_currentItemId);

		if (!_brewService.TryGetRequiredIngredients(_currentItemId, out var requiredIngredients, out var error))
		{
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = error;
			return;
		}

		var isInPotionBook = _gameState.KnowsPotion(_currentItemId);
		var hasIngredients = _brewService.HasRequiredIngredients(requiredIngredients);
		_itemDetailBrewButton.Disabled = !isInPotionBook || !hasIngredients;
		_itemDetailBrewButton.TooltipText = !isInPotionBook
			? "Add this potion to the potion book to unlock repeat brewing."
			: hasIngredients
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
	}

	private void UpdateAddToPotionBookButtonState(string itemId)
	{
		if (_itemDetailAddToPotionBookButton is null)
			return;

		_itemDetailAddToPotionBookButton.Text = "Add to Book";
		_itemDetailAddToPotionBookButton.Visible = false;
		_itemDetailAddToPotionBookButton.Disabled = true;
		_itemDetailAddToPotionBookButton.TooltipText = "";

		if (string.IsNullOrWhiteSpace(itemId))
			return;
		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;
		if (!IsPotion(itemId))
			return;
		if (item.Treatment is not null)
			return;
		if (HasActiveRisk(item))
			return;
		if (_gameState.KnowsPotion(itemId))
			return;

		_itemDetailAddToPotionBookButton.Visible = true;
		if (!_gameState.TryGetPotionRecipe(itemId, out var ingredientIds) || ingredientIds.Count == 0)
		{
			_itemDetailAddToPotionBookButton.Disabled = true;
			_itemDetailAddToPotionBookButton.TooltipText = "No recipe is recorded for this potion.";
			return;
		}

		_itemDetailAddToPotionBookButton.Disabled = false;
		_itemDetailAddToPotionBookButton.TooltipText = "Add this clean potion to the potion book.";
	}

	private void HideAddToPotionBookButton()
	{
		if (_itemDetailAddToPotionBookButton is null)
			return;

		_itemDetailAddToPotionBookButton.Visible = false;
		_itemDetailAddToPotionBookButton.Disabled = true;
		_itemDetailAddToPotionBookButton.TooltipText = "";
	}

	private string ItemName(string itemId)
	{
		return _itemCatalog.GetItemName(itemId);
	}

	private int GetItemPrice(string itemId, ItemDef? item)
	{
		if (_gameState.TryGetPotionBasePrice(itemId, out var potionBasePrice))
			return potionBasePrice;

		return item?.BasePrice ?? 0;
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		if (IsPotion(itemId))
		{
			var customName = _gameState.GetPotionDisplayName(itemId);
			if (!string.IsNullOrWhiteSpace(customName))
				return customName;
		}

		return fallbackName;
	}

	private static void SplitInventoryName(string itemName, out string firstLine, out string secondLine)
	{
		InventoryItemTextFormatter.SplitInventoryName(itemName, out firstLine, out secondLine);
	}

	private static string FormatItemDetailName(string itemName)
	{
		return InventoryItemTextFormatter.FormatItemDetailName(itemName);
	}

	private bool IsPotion(string itemId)
	{
		return _itemCatalog.IsPotion(itemId);
	}

	private bool IsConsumable(string itemId)
	{
		return _itemCatalog.IsConsumable(itemId);
	}

	private bool IsIngredient(string itemId)
	{
		return _itemCatalog.IsIngredient(itemId);
	}

	private static string BuildConsumableEffectText(ItemDef item)
	{
		return InventoryItemTextFormatter.BuildConsumableEffectText(item);
	}

	private static string BuildConsumableGateText(ItemDef item)
	{
		return InventoryItemTextFormatter.BuildConsumableGateText(item);
	}

	private bool ItemHasIngredientType(string itemId, string ingredientType)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return false;

		return ItemCatalogService.HasTag(item, ingredientType);
	}

	private static bool IsIngredient(ItemDef item)
	{
		return ItemCatalogService.HasTag(item, ItemTags.Ingredient);
	}

	private void SetItemTypeTag(ItemDef item)
	{
		var displayTypeTag = TryGetVisibleTypeTag(item);
		_itemDetailTypeTag.Visible = !string.IsNullOrWhiteSpace(displayTypeTag);
		_itemDetailTypeTag.Text = displayTypeTag;
	}

	private static string TryGetVisibleTypeTag(ItemDef item)
	{
		return InventoryItemTextFormatter.TryGetVisibleTypeTag(item);
	}

	private void RefreshKnownRecipes(string itemId, ItemDef item)
	{
		ClearKnownRecipeRows();

		if (!IsIngredient(item))
		{
			AddKnownRecipeEmptyRow("Only ingredients list known recipes.");
			return;
		}

		var knownPotionIds = _gameState.KnownPotions
			.OrderBy(potionId => DisplayName(potionId, ItemName(potionId)))
			.ThenBy(potionId => potionId)
			.ToList();

		var foundAnyRecipe = false;
		foreach (var potionId in knownPotionIds)
		{
			if (!_gameState.TryGetPotionRecipe(potionId, out var ingredientIds))
				continue;
			if (!ingredientIds.Any(ingredientId => string.Equals(ingredientId, itemId, System.StringComparison.OrdinalIgnoreCase)))
				continue;
			if (!_itemCatalog.TryGetItem(potionId, out var potion))
				continue;

			foundAnyRecipe = true;
			AddKnownRecipeRow(potionId, potion);
		}

		if (!foundAnyRecipe)
			AddKnownRecipeEmptyRow("No known recipes");
	}

	private void AddKnownRecipeRow(string potionId, ItemDef potion)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0, 34),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 8);

		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(30, 30),
			Texture = UiIconLoader.LoadIcon(potion.IconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};

		var name = new Label
		{
			Text = DisplayName(potionId, potion.Name),
			CustomMinimumSize = new Vector2(114, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore
		};

		var status = new Label
		{
			Text = BuildRecipeStatusText(potionId),
			CustomMinimumSize = new Vector2(76, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore
		};
		status.AddThemeColorOverride("font_color", status.Text == "Brewable"
			? new Color(0.43f, 0.83f, 0.48f, 1f)
			: new Color(0.73f, 0.74f, 0.78f, 1f));

		row.AddChild(icon);
		row.AddChild(name);
		row.AddChild(status);
		_itemDetailKnownRecipes.AddChild(row);
	}

	private string BuildRecipeStatusText(string potionId)
	{
		if (!_brewService.TryGetRequiredIngredients(potionId, out var requiredIngredients, out _))
			return "Unknown";

		var missingCount = 0;
		foreach (var pair in requiredIngredients)
		{
			var have = _gameState.Inventory.GetValueOrDefault(pair.Key);
			missingCount += Math.Max(0, pair.Value - have);
		}

		return missingCount == 0 ? "Brewable" : $"Missing {missingCount}";
	}

	private void AddKnownRecipeEmptyRow(string text)
	{
		var label = new Label
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 34),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", new Color(0.68f, 0.7f, 0.75f, 1f));
		_itemDetailKnownRecipes.AddChild(label);
	}

	private void ClearKnownRecipeRows()
	{
		foreach (var child in _itemDetailKnownRecipes.GetChildren())
		{
			_itemDetailKnownRecipes.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Control? FindVisibleItemSlot(Node root, string itemId)
	{
		foreach (var child in root.GetChildren())
		{
			if (child is InventoryItemSlot slot && string.Equals(slot.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
				return slot;
		}

		return null;
	}

	private static string FormatTopStats(Dictionary<string, int> values, int maxCount, string emptyLabel = "None")
	{
		return InventoryItemTextFormatter.FormatTopStats(values, maxCount, emptyLabel);
	}

	private static string FormatTopStatNames(Dictionary<string, int> values, int maxCount, string emptyLabel = "None")
	{
		return InventoryItemTextFormatter.FormatTopStatNames(values, maxCount, emptyLabel);
	}

	private static string DisplayStatName(string key)
	{
		return InventoryItemTextFormatter.DisplayStatName(key);
	}
}
