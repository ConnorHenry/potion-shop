using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class InventoryPanel : Control
{
	private const float SlotSize = 112.0f;
	private const float IconSize = 70.0f;

	[Export] public NodePath PotionsContainerPath = default!;
	[Export] public NodePath IngredientsContainerPath = default!;
	[Export] public NodePath PotionsSortButtonPath = default!;
	[Export] public NodePath PotionsTraitFilterPath = default!;
	[Export] public NodePath PotionsRiskFilterPath = default!;
	[Export] public NodePath PotionsClearFilterButtonPath = default!;
	[Export] public NodePath IngredientsSortButtonPath = default!;
	[Export] public NodePath IngredientsTraitFilterPath = default!;
	[Export] public NodePath IngredientsRiskFilterPath = default!;
	[Export] public NodePath IngredientsClearFilterButtonPath = default!;
	[Export] public NodePath ItemDetailPanelPath = default!;
	[Export] public NodePath ItemDetailImagePath = default!;
	[Export] public NodePath ItemDetailNamePath = default!;
	[Export] public NodePath ItemDetailPricePath = default!;
	[Export] public NodePath ItemDetailTraitsHeaderPath = default!;
	[Export] public NodePath ItemDetailTraitsPath = default!;
	[Export] public NodePath ItemDetailRisksHeaderPath = default!;
	[Export] public NodePath ItemDetailRisksPath = default!;
	[Export] public NodePath ItemDetailDescriptionPath = default!;
	[Export] public NodePath ItemDetailOwnedPath = default!;
	[Export] public NodePath ItemDetailKnownRecipesPath = default!;
	[Export] public NodePath ItemDetailBrewButtonPath = default!;
	[Export] public NodePath ItemDetailCloseButtonPath = default!;
	[Export] public NodePath ItemDetailTopCloseButtonPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private GridContainer _potions = default!;
	private GridContainer _ingredients = default!;
	private Button _potionsSortButton = default!;
	private OptionButton? _potionsTraitFilter;
	private OptionButton? _potionsRiskFilter;
	private Button? _potionsClearFilterButton;
	private Button _ingredientsSortButton = default!;
	private OptionButton? _ingredientsTraitFilter;
	private OptionButton? _ingredientsRiskFilter;
	private Button? _ingredientsClearFilterButton;
	private Control _itemDetailPanel = default!;
	private TextureRect _itemDetailImage = default!;
	private Label _itemDetailName = default!;
	private Label _itemDetailPrice = default!;
	private Label _itemDetailTraitsHeader = default!;
	private RichTextLabel _itemDetailTraits = default!;
	private Label _itemDetailRisksHeader = default!;
	private RichTextLabel _itemDetailRisks = default!;
	private RichTextLabel _itemDetailDescription = default!;
	private Label _itemDetailOwned = default!;
	private VBoxContainer _itemDetailKnownRecipes = default!;
	private Button _itemDetailBrewButton = default!;
	private Button _itemDetailCloseButton = default!;
	private Button? _itemDetailTopCloseButton;
	private BrewPanel? _brewPanel;
	private string? _currentItemId;
	private bool _potionsAscending = true;
	private bool _ingredientsAscending = true;
	private string? _activePotionTraitFilter;
	private string? _activePotionRiskFilter;
	private string? _activeIngredientTraitFilter;
	private string? _activeIngredientRiskFilter;
	private PotionInventoryBrewService _brewService = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;

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

		_potions = GetNode<GridContainer>(PotionsContainerPath);
		_ingredients = GetNode<GridContainer>(IngredientsContainerPath);
		_potionsSortButton = GetNode<Button>(PotionsSortButtonPath);
		_potionsTraitFilter = GetNodeOrNull<OptionButton>(PotionsTraitFilterPath);
		_potionsRiskFilter = GetNodeOrNull<OptionButton>(PotionsRiskFilterPath);
		_potionsClearFilterButton = GetNodeOrNull<Button>(PotionsClearFilterButtonPath);
		_ingredientsSortButton = GetNode<Button>(IngredientsSortButtonPath);
		_ingredientsTraitFilter = GetNodeOrNull<OptionButton>(IngredientsTraitFilterPath);
		_ingredientsRiskFilter = GetNodeOrNull<OptionButton>(IngredientsRiskFilterPath);
		_ingredientsClearFilterButton = GetNodeOrNull<Button>(IngredientsClearFilterButtonPath);
		_itemDetailPanel = GetNode<Control>(ItemDetailPanelPath);
		_itemDetailImage = GetNode<TextureRect>(ItemDetailImagePath);
		_itemDetailName = GetNode<Label>(ItemDetailNamePath);
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
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);
		_itemDetailTopCloseButton = GetNodeOrNull<Button>(ItemDetailTopCloseButtonPath);
		_brewPanel = GetNodeOrNull<BrewPanel>(new NodePath("../BrewPanel"));

		MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.ZIndex = 2000;
		_potionsSortButton.Pressed += TogglePotionsSort;
		_ingredientsSortButton.Pressed += ToggleIngredientsSort;
		if (_potionsTraitFilter is not null)
			_potionsTraitFilter.ItemSelected += OnPotionTraitSelected;
		if (_potionsRiskFilter is not null)
			_potionsRiskFilter.ItemSelected += OnPotionRiskSelected;
		if (_potionsClearFilterButton is not null)
			_potionsClearFilterButton.Pressed += ClearPotionFilters;
		if (_ingredientsTraitFilter is not null)
			_ingredientsTraitFilter.ItemSelected += OnIngredientTraitSelected;
		if (_ingredientsRiskFilter is not null)
			_ingredientsRiskFilter.ItemSelected += OnIngredientRiskSelected;
		if (_ingredientsClearFilterButton is not null)
			_ingredientsClearFilterButton.Pressed += ClearIngredientFilters;
		_itemDetailBrewButton.Pressed += TryUseSelectedItem;
		_itemDetailCloseButton.Pressed += HideItemDetail;
		if (_itemDetailTopCloseButton is not null)
			_itemDetailTopCloseButton.Pressed += HideItemDetail;
		_gameState.Changed += Refresh;

		Visible = true;
		_itemDetailPanel.Visible = false;
		UpdateSortButtonLabels();
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_potionsSortButton is not null)
			_potionsSortButton.Pressed -= TogglePotionsSort;
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
		if (_ingredientsRiskFilter is not null)
			_ingredientsRiskFilter.ItemSelected -= OnIngredientRiskSelected;
		if (_ingredientsClearFilterButton is not null)
			_ingredientsClearFilterButton.Pressed -= ClearIngredientFilters;
		if (_itemDetailBrewButton is not null)
			_itemDetailBrewButton.Pressed -= TryUseSelectedItem;
		if (_itemDetailCloseButton is not null)
			_itemDetailCloseButton.Pressed -= HideItemDetail;
		if (_itemDetailTopCloseButton is not null)
			_itemDetailTopCloseButton.Pressed -= HideItemDetail;
	}

	private void TogglePotionsSort()
	{
		_potionsAscending = !_potionsAscending;
		UpdateSortButtonLabels();
		Refresh();
	}

	private void ToggleIngredientsSort()
	{
		_ingredientsAscending = !_ingredientsAscending;
		UpdateSortButtonLabels();
		Refresh();
	}

	private void Refresh()
	{
		if (_potions is null || _ingredients is null)
			return;

		foreach (var child in _potions.GetChildren())
			child.QueueFree();
		foreach (var child in _ingredients.GetChildren())
			child.QueueFree();

		if (_gameState.Inventory.Count == 0)
			_ingredients.AddChild(new Label { Text = "Empty" });

		var potionStacks = _gameState.Inventory.Where(x => IsPotion(x.Key)).ToList();
		var ingredientStacks = _gameState.Inventory.Where(x => !IsPotion(x.Key)).ToList();
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

		if (_potionsAscending)
		{
			foreach (var stack in potionStacksToRender.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
				_potions.AddChild(CreateSlot(stack.Key, stack.Value));
		}
		else
		{
			foreach (var stack in potionStacksToRender.OrderByDescending(x => ItemName(x.Key)).ThenByDescending(x => x.Key))
				_potions.AddChild(CreateSlot(stack.Key, stack.Value));
		}

		var ingredientStacksToRender = ingredientStacks;
		if (_ingredientsTraitFilter is null)
		{
			_activeIngredientTraitFilter = null;
		}
		if (_ingredientsRiskFilter is null)
		{
			_activeIngredientRiskFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			ingredientStacksToRender = ingredientStacks.Where(stack => ItemFilterUtilities.ItemHasTrait(stack.Key, _activeIngredientTraitFilter, _itemCatalog)).ToList();
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

		ItemFilterUtilities.RefreshFilterOptions(_potionsTraitFilter, potionTraitNames, "Trait", ref _activePotionTraitFilter);
		ItemFilterUtilities.RefreshFilterOptions(_potionsRiskFilter, potionRiskNames, "Risk", ref _activePotionRiskFilter);
		ItemFilterUtilities.RefreshFilterOptions(_ingredientsTraitFilter, ingredientTraitNames, "Trait", ref _activeIngredientTraitFilter);
		ItemFilterUtilities.RefreshFilterOptions(_ingredientsRiskFilter, ingredientRiskNames, "Risk", ref _activeIngredientRiskFilter);
		RefreshCurrentItemDetail();
		UpdateBrewButtonState();
	}

	private void UpdateSortButtonLabels()
	{
		_potionsSortButton.Text = _potionsAscending ? "A-Z" : "Z-A";
		_ingredientsSortButton.Text = _ingredientsAscending ? "A-Z" : "Z-A";
	}

	private void OnIngredientTraitSelected(long selectedIndex)
	{
		if (_ingredientsTraitFilter is null)
			return;

		HandleFilterSelected(_ingredientsTraitFilter, selectedIndex, "Trait", ref _activeIngredientTraitFilter);
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
		if (_ingredientsTraitFilter is null && _ingredientsRiskFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activeIngredientTraitFilter) && string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			if (_ingredientsTraitFilter is not null)
				_ingredientsTraitFilter.Selected = 0;
			if (_ingredientsRiskFilter is not null)
				_ingredientsRiskFilter.Selected = 0;
			return;
		}

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

		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			TooltipText = itemName,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = true,
			ItemId = itemId,
			ItemName = itemName,
			IconPath = item?.IconPath,
			Quantity = quantity
		};
		slot.SlotActivated += ShowItemDetail;
		slot.IngredientRequested += QueueIngredientFromSlot;

		var content = new Control
		{
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var icon = new TextureRect
		{
			Position = new Vector2(21, 6),
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		icon.Texture = UiIconLoader.LoadIcon(item?.IconPath);

		var qty = new Label
		{
			Text = quantity.ToString(),
			Position = new Vector2(4, 2),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var hasPrice = item is not null || _gameState.TryGetPotionBasePrice(itemId, out _);
		if (hasPrice)
		{
			var price = new Label
			{
				Text = $"£{GetItemPrice(itemId, item)}",
				Position = new Vector2(50, 2),
				CustomMinimumSize = new Vector2(58, 0),
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
			Position = new Vector2(4, 70),
			CustomMinimumSize = new Vector2(SlotSize - 8, 34),
			MouseFilter = MouseFilterEnum.Ignore
		};

		SplitInventoryName(itemName, out var firstLine, out var secondLine);
		name.Text = firstLine;
		name.Position = new Vector2(0, 0);
		name.CustomMinimumSize = new Vector2(SlotSize - 8, 0);

		nameBlock.AddChild(name);

		if (!string.IsNullOrWhiteSpace(secondLine))
		{
			var secondName = new Label
			{
				Text = secondLine,
				Position = new Vector2(0, 15),
				CustomMinimumSize = new Vector2(SlotSize - 8, 0),
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				ClipText = false
			};

			nameBlock.AddChild(secondName);
		}

		content.AddChild(icon);
		content.AddChild(qty);
		content.AddChild(nameBlock);
		slot.AddChild(content);
		return slot;
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
	}

	private void QueueIngredientFromSlot(string itemId)
	{
		if (_brewPanel is null || !_brewPanel.Visible)
			return;

		if (!_itemCatalog.TryGetItem(itemId, out var item))
			return;

		if (!IsIngredient(item))
			return;

		_brewPanel.TryQueueIngredient(itemId);
	}

	private void HideItemDetail()
	{
		_currentItemId = null;
		_itemDetailImage.Texture = null;
		_itemDetailTraits.Text = "";
		_itemDetailRisks.Text = "";
		_itemDetailDescription.Text = "";
		_itemDetailPrice.Text = "";
		_itemDetailOwned.Text = "";
		ClearKnownRecipeRows();
		_itemDetailBrewButton.Visible = false;
		_itemDetailBrewButton.Disabled = true;
		_itemDetailPanel.Visible = false;
	}

	private void RefreshCurrentItemDetail()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

		if (!_itemCatalog.TryGetItem(_currentItemId, out var item))
			return;

		_itemDetailImage.Texture = UiIconLoader.LoadIcon(item.IconPath);
		_itemDetailName.Text = DisplayName(_currentItemId, item.Name);
		_itemDetailOwned.Text = $"Owned: {_gameState.Inventory.GetValueOrDefault(_currentItemId)}";
		_itemDetailPrice.Text = $"Sell Price: \u00A3{GetItemPrice(_currentItemId, item)}";
		_itemDetailTraits.Text = FormatTopStats(item.Traits, 3);
		_itemDetailRisks.Text = FormatTopStats(item.Risks, 3, "None");
		_itemDetailDescription.Text = IsPotion(_currentItemId)
			? _brewService.BuildPotionDescriptionText(_currentItemId, item.Description)
			: item.Description;
		RefreshKnownRecipes(_currentItemId, item);
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

			_brewPanel.TryQueueIngredient(_currentItemId);
			HideItemDetail();
			return;
		}

		if (!IsPotion(_currentItemId))
			return;

		if (!_brewService.TryBrewPotion(_currentItemId, out var error))
			GD.PushError(error);

		UpdateBrewButtonState();
	}

	private void UpdateBrewButtonState()
	{
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
				return;
			}

			_itemDetailBrewButton.Visible = false;
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = "";
			return;
		}

		_itemDetailBrewButton.Text = "Brew This Potion";
		_itemDetailBrewButton.Visible = true;

		if (!_brewService.TryGetRequiredIngredients(_currentItemId, out var requiredIngredients, out var error))
		{
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = error;
			return;
		}

		var hasIngredients = _brewService.HasRequiredIngredients(requiredIngredients);
		_itemDetailBrewButton.Disabled = !hasIngredients;
		_itemDetailBrewButton.TooltipText = hasIngredients
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
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
		if (string.IsNullOrWhiteSpace(itemName))
		{
			firstLine = itemName;
			secondLine = string.Empty;
			return;
		}

		var firstSpaceIndex = itemName.IndexOf(' ');
		if (firstSpaceIndex <= 0 || firstSpaceIndex >= itemName.Length - 1)
		{
			firstLine = itemName;
			secondLine = string.Empty;
			return;
		}

		firstLine = itemName[..firstSpaceIndex];
		secondLine = itemName[(firstSpaceIndex + 1)..];
	}

	private bool IsPotion(string itemId)
	{
		return _itemCatalog.IsPotion(itemId);
	}

	private bool IsIngredient(string itemId)
	{
		return _itemCatalog.IsIngredient(itemId);
	}

	private static bool IsIngredient(ItemDef item)
	{
		return ItemCatalogService.HasTag(item, "ingredient");
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

	private static string FormatTopStats(Dictionary<string, int> values, int maxCount, string emptyLabel = "None")
	{
		var lines = new List<string>(maxCount);
		if (values is not null)
		{
			lines.AddRange(values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount)
				.Select(x => $"{DisplayStatName(x.Key)} +{x.Value}"));
		}

		if (lines.Count == 0)
			lines.Add(emptyLabel);

		while (lines.Count < maxCount)
			lines.Add(string.Empty);

		return string.Join("\n", lines);
	}

	private static string DisplayStatName(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return string.Empty;

		var normalized = key.Replace('_', ' ').Trim();
		if (normalized.Length == 0)
			return string.Empty;

		return char.ToUpperInvariant(normalized[0]) + normalized[1..];
	}
}
