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
	[Export] public NodePath ItemDetailBrewButtonPath = default!;
	[Export] public NodePath ItemDetailCloseButtonPath = default!;

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
	private Button _itemDetailBrewButton = default!;
	private Button _itemDetailCloseButton = default!;
	private BrewPanel? _brewPanel;
	private string? _currentItemId;
	private bool _potionsAscending = true;
	private bool _ingredientsAscending = true;
	private string? _activePotionTraitFilter;
	private string? _activePotionRiskFilter;
	private string? _activeIngredientTraitFilter;
	private string? _activeIngredientRiskFilter;
	private readonly PotionInventoryBrewService _brewService = new();
	private GameState _gameState = default!;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("InventoryPanel: /root/GameState was not found.");
			return;
		}
		_gameState = gameState;

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
		_itemDetailDescription.BbcodeEnabled = true;
		_itemDetailPrice.AddThemeColorOverride("font_color", new Color("FFD700"));
		_itemDetailBrewButton = GetNode<Button>(ItemDetailBrewButtonPath);
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);
		_brewPanel = GetNodeOrNull<BrewPanel>(new NodePath("../BrewPanel"));

		MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.MouseFilter = MouseFilterEnum.Ignore;
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
		_itemDetailBrewButton.Pressed += TryBrewSelectedPotion;
		_itemDetailCloseButton.Pressed += HideItemDetail;
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
			_itemDetailBrewButton.Pressed -= TryBrewSelectedPotion;
		if (_itemDetailCloseButton is not null)
			_itemDetailCloseButton.Pressed -= HideItemDetail;
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
		var potionTraitNames = BuildTopTraitNames(potionStacks, 3);
		var potionRiskNames = BuildRiskNames(potionStacks);
		var ingredientTraitNames = BuildTraitNames(ingredientStacks);
		var ingredientRiskNames = BuildRiskNames(ingredientStacks);

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
			potionStacksToRender = potionStacks.Where(stack => ItemHasTopTrait(stack.Key, _activePotionTraitFilter, 3)).ToList();
		}
		if (!string.IsNullOrWhiteSpace(_activePotionRiskFilter))
		{
			potionStacksToRender = potionStacksToRender.Where(stack => ItemHasRisk(stack.Key, _activePotionRiskFilter)).ToList();
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
			ingredientStacksToRender = ingredientStacks.Where(stack => ItemHasTrait(stack.Key, _activeIngredientTraitFilter)).ToList();
		}
		if (!string.IsNullOrWhiteSpace(_activeIngredientRiskFilter))
		{
			ingredientStacksToRender = ingredientStacksToRender.Where(stack => ItemHasRisk(stack.Key, _activeIngredientRiskFilter)).ToList();
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

		RefreshFilterOptions(_potionsTraitFilter, potionTraitNames, "Trait", ref _activePotionTraitFilter);
		RefreshFilterOptions(_potionsRiskFilter, potionRiskNames, "Risk", ref _activePotionRiskFilter);
		RefreshFilterOptions(_ingredientsTraitFilter, ingredientTraitNames, "Trait", ref _activeIngredientTraitFilter);
		RefreshFilterOptions(_ingredientsRiskFilter, ingredientRiskNames, "Risk", ref _activeIngredientRiskFilter);
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

	private static void RefreshFilterOptions(OptionButton? traitFilter, List<string> traitNames, string placeholderLabel, ref string? activeTraitFilter)
	{
		if (traitFilter is null)
			return;

		traitFilter.Clear();
		traitFilter.AddItem(placeholderLabel);

		foreach (var traitName in traitNames)
			traitFilter.AddItem(traitName);

		if (string.IsNullOrWhiteSpace(activeTraitFilter))
		{
			traitFilter.Selected = 0;
			return;
		}

		for (var index = 1; index < traitFilter.ItemCount; index++)
		{
			var itemText = traitFilter.GetItemText(index);
			if (!string.Equals(itemText, activeTraitFilter, System.StringComparison.OrdinalIgnoreCase))
				continue;

			traitFilter.Selected = index;
			return;
		}

		activeTraitFilter = null;
		traitFilter.Selected = 0;
	}

	private static List<string> BuildTopTraitNames(IEnumerable<KeyValuePair<string, int>> itemStacks, int maxCount)
	{
		var uniqueNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var stack in itemStacks)
		{
			if (!ItemCatalog.TryGetItem(stack.Key, out var item))
				continue;

			foreach (var trait in item.Traits
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount))
			{
				if (string.IsNullOrWhiteSpace(trait.Key))
					continue;
				if (trait.Value <= 0)
					continue;

				uniqueNames.Add(trait.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	private static List<string> BuildTraitNames(IEnumerable<KeyValuePair<string, int>> itemStacks)
	{
		var uniqueNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var stack in itemStacks)
		{
			if (!ItemCatalog.TryGetItem(stack.Key, out var item))
				continue;

			foreach (var trait in item.Traits)
			{
				if (string.IsNullOrWhiteSpace(trait.Key))
					continue;
				if (trait.Value <= 0)
					continue;

				uniqueNames.Add(trait.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	private static List<string> BuildRiskNames(IEnumerable<KeyValuePair<string, int>> itemStacks)
	{
		var uniqueNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var stack in itemStacks)
		{
			if (!ItemCatalog.TryGetItem(stack.Key, out var item))
				continue;

			foreach (var risk in item.Risks)
			{
				if (string.IsNullOrWhiteSpace(risk.Key))
					continue;
				if (risk.Value <= 0)
					continue;

				uniqueNames.Add(risk.Key);
			}
		}

		return uniqueNames.OrderBy(name => name).ToList();
	}

	private static bool ItemHasTrait(string itemId, string traitName)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		foreach (var trait in item.Traits)
		{
			if (!string.Equals(trait.Key, traitName, System.StringComparison.OrdinalIgnoreCase))
				continue;

			return trait.Value > 0;
		}

		return false;
	}

	private static bool ItemHasTopTrait(string itemId, string traitName, int maxCount)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		foreach (var trait in item.Traits
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.Take(maxCount))
		{
			if (!string.Equals(trait.Key, traitName, System.StringComparison.OrdinalIgnoreCase))
				continue;
			if (trait.Value <= 0)
				continue;

			return true;
		}

		return false;
	}

	private static bool ItemHasRisk(string itemId, string riskName)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.Equals(risk.Key, riskName, System.StringComparison.OrdinalIgnoreCase))
				continue;

			return risk.Value > 0;
		}

		return false;
	}

	private Control CreateSlot(string itemId, int quantity)
	{
		var item = ItemCatalog.TryGetItem(itemId, out var def) ? def : null;
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
		icon.Texture = LoadIcon(item?.IconPath);

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

		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return;

		_currentItemId = itemId;
		RefreshCurrentItemDetail();
		_itemDetailPanel.Visible = true;
		UpdateBrewButtonState();
	}

	private void QueueIngredientFromSlot(string itemId)
	{
		if (_brewPanel is null || !_brewPanel.Visible)
			return;

		if (!ItemCatalog.TryGetItem(itemId, out var item))
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
		_itemDetailBrewButton.Visible = false;
		_itemDetailBrewButton.Disabled = true;
		_itemDetailPanel.Visible = false;
	}

	private void RefreshCurrentItemDetail()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

		if (!ItemCatalog.TryGetItem(_currentItemId, out var item))
			return;

		_itemDetailImage.Texture = LoadIcon(item.IconPath);
		_itemDetailName.Text = DisplayName(_currentItemId, item.Name);
		_itemDetailPrice.Text = $"Sell Price - £{GetItemPrice(_currentItemId, item)}";
		_itemDetailTraits.Text = FormatTopTraits(item.Traits, 3);
		_itemDetailRisks.Text = FormatDictionary(item.Risks);
		_itemDetailDescription.Text = IsPotion(_currentItemId)
			? _brewService.BuildPotionDescriptionText(_currentItemId, item.Description)
			: item.Description;
	}

	private void TryBrewSelectedPotion()
	{
		if (string.IsNullOrWhiteSpace(_currentItemId))
			return;

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
			_itemDetailBrewButton.Visible = false;
			_itemDetailBrewButton.Disabled = true;
			_itemDetailBrewButton.TooltipText = "";
			return;
		}

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

	private static Texture2D? LoadIcon(string? iconPath)
	{
		if (string.IsNullOrWhiteSpace(iconPath))
			return null;

		return ResourceLoader.Load<Texture2D>(iconPath);
	}

	private static string ItemName(string itemId)
	{
		return ItemCatalog.GetItemName(itemId);
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

	private static bool IsPotion(string itemId)
	{
		if (!ItemCatalog.TryGetItem(itemId, out var item))
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsIngredient(ItemDef item)
	{
		return item.Tags.Any(tag => string.Equals(tag, "ingredient", System.StringComparison.OrdinalIgnoreCase));
	}

	private static string FormatDictionary(Dictionary<string, int> values)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join("\n",
			values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Select(x => $"{x.Key}: {x.Value}"));
	}

	private static string FormatTopTraits(Dictionary<string, int> values, int maxCount)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join("\n",
			values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount)
				.Select(x => $"{x.Key}: {x.Value}"));
	}
}
