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
	[Export] public NodePath PotionsClearFilterButtonPath = default!;
	[Export] public NodePath IngredientsSortButtonPath = default!;
	[Export] public NodePath IngredientsTraitFilterPath = default!;
	[Export] public NodePath IngredientsClearFilterButtonPath = default!;
	[Export] public NodePath ItemDetailPanelPath = default!;
	[Export] public NodePath ItemDetailImagePath = default!;
	[Export] public NodePath ItemDetailNamePath = default!;
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
	private Button? _potionsClearFilterButton;
	private Button _ingredientsSortButton = default!;
	private OptionButton? _ingredientsTraitFilter;
	private Button? _ingredientsClearFilterButton;
	private Control _itemDetailPanel = default!;
	private TextureRect _itemDetailImage = default!;
	private Label _itemDetailName = default!;
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
	private string? _activeIngredientTraitFilter;
	private readonly PotionInventoryBrewService _brewService = new();

	public override void _Ready()
	{
		_potions = GetNode<GridContainer>(PotionsContainerPath);
		_ingredients = GetNode<GridContainer>(IngredientsContainerPath);
		_potionsSortButton = GetNode<Button>(PotionsSortButtonPath);
		_potionsTraitFilter = GetNodeOrNull<OptionButton>(PotionsTraitFilterPath);
		_potionsClearFilterButton = GetNodeOrNull<Button>(PotionsClearFilterButtonPath);
		_ingredientsSortButton = GetNode<Button>(IngredientsSortButtonPath);
		_ingredientsTraitFilter = GetNodeOrNull<OptionButton>(IngredientsTraitFilterPath);
		_ingredientsClearFilterButton = GetNodeOrNull<Button>(IngredientsClearFilterButtonPath);
		_itemDetailPanel = GetNode<Control>(ItemDetailPanelPath);
		_itemDetailImage = GetNode<TextureRect>(ItemDetailImagePath);
		_itemDetailName = GetNode<Label>(ItemDetailNamePath);
		_itemDetailTraitsHeader = GetNode<Label>(ItemDetailTraitsHeaderPath);
		_itemDetailTraits = GetNode<RichTextLabel>(ItemDetailTraitsPath);
		_itemDetailRisksHeader = GetNode<Label>(ItemDetailRisksHeaderPath);
		_itemDetailRisks = GetNode<RichTextLabel>(ItemDetailRisksPath);
		_itemDetailDescription = GetNode<RichTextLabel>(ItemDetailDescriptionPath);
		_itemDetailDescription.BbcodeEnabled = true;
		_itemDetailBrewButton = GetNode<Button>(ItemDetailBrewButtonPath);
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);
		_brewPanel = GetNodeOrNull<BrewPanel>(new NodePath("../BrewPanel"));

		MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.MouseFilter = MouseFilterEnum.Ignore;
		_potionsSortButton.Pressed += TogglePotionsSort;
		_ingredientsSortButton.Pressed += ToggleIngredientsSort;
		if (_potionsTraitFilter is not null)
			_potionsTraitFilter.ItemSelected += OnPotionTraitSelected;
		if (_potionsClearFilterButton is not null)
			_potionsClearFilterButton.Pressed += ClearPotionTraitFilter;
		if (_ingredientsTraitFilter is not null)
			_ingredientsTraitFilter.ItemSelected += OnIngredientTraitSelected;
		if (_ingredientsClearFilterButton is not null)
			_ingredientsClearFilterButton.Pressed += ClearIngredientTraitFilter;
		_itemDetailBrewButton.Pressed += TryBrewSelectedPotion;
		_itemDetailCloseButton.Pressed += HideItemDetail;
		GameState.Changed += Refresh;

		Visible = true;
		_itemDetailPanel.Visible = false;
		UpdateSortButtonLabels();
		Refresh();
	}

	public override void _ExitTree()
	{
		GameState.Changed -= Refresh;
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

		if (GameState.Inventory.Count == 0)
			_ingredients.AddChild(new Label { Text = "Empty" });

		var potionStacks = GameState.Inventory.Where(x => IsPotion(x.Key)).ToList();
		var ingredientStacks = GameState.Inventory.Where(x => !IsPotion(x.Key)).ToList();
		var potionTraitNames = BuildTraitNames(potionStacks);
		var ingredientTraitNames = BuildTraitNames(ingredientStacks);

		if (!string.IsNullOrWhiteSpace(_activePotionTraitFilter))
		{
			var activeTraitExists = potionTraitNames.Any(trait =>
				string.Equals(trait, _activePotionTraitFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeTraitExists)
				_activePotionTraitFilter = null;
		}

		if (!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			var activeTraitExists = ingredientTraitNames.Any(trait =>
				string.Equals(trait, _activeIngredientTraitFilter, System.StringComparison.OrdinalIgnoreCase));
			if (!activeTraitExists)
				_activeIngredientTraitFilter = null;
		}

		var potionStacksToRender = potionStacks;
		if (_potionsTraitFilter is null)
		{
			_activePotionTraitFilter = null;
		}
		else if (!string.IsNullOrWhiteSpace(_activePotionTraitFilter))
		{
			potionStacksToRender = potionStacks.Where(stack => ItemHasTrait(stack.Key, _activePotionTraitFilter)).ToList();
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
		else if (!string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			ingredientStacksToRender = ingredientStacks.Where(stack => ItemHasTrait(stack.Key, _activeIngredientTraitFilter)).ToList();
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

		RefreshTraitFilterOptions(_potionsTraitFilter, potionTraitNames, ref _activePotionTraitFilter);
		RefreshTraitFilterOptions(_ingredientsTraitFilter, ingredientTraitNames, ref _activeIngredientTraitFilter);
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

		HandleTraitSelected(_ingredientsTraitFilter, selectedIndex, ref _activeIngredientTraitFilter);
	}

	private void ClearIngredientTraitFilter()
	{
		if (_ingredientsTraitFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activeIngredientTraitFilter))
		{
			_ingredientsTraitFilter.Selected = 0;
			return;
		}

		_activeIngredientTraitFilter = null;
		Refresh();
	}

	private void OnPotionTraitSelected(long selectedIndex)
	{
		if (_potionsTraitFilter is null)
			return;

		HandleTraitSelected(_potionsTraitFilter, selectedIndex, ref _activePotionTraitFilter);
	}

	private void ClearPotionTraitFilter()
	{
		if (_potionsTraitFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activePotionTraitFilter))
		{
			_potionsTraitFilter.Selected = 0;
			return;
		}

		_activePotionTraitFilter = null;
		Refresh();
	}

	private void HandleTraitSelected(OptionButton? traitFilter, long selectedIndex, ref string? activeTraitFilter)
	{
		if (traitFilter is null)
			return;

		var selectedTrait = traitFilter.GetItemText((int)selectedIndex);
		if (string.Equals(selectedTrait, "Trait", System.StringComparison.OrdinalIgnoreCase))
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

	private static void RefreshTraitFilterOptions(OptionButton? traitFilter, List<string> traitNames, ref string? activeTraitFilter)
	{
		if (traitFilter is null)
			return;

		traitFilter.Clear();
		traitFilter.AddItem("Trait");

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
		if (_itemDetailBrewButton is null)
			return;

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

	private static string DisplayName(string itemId, string fallbackName)
	{
		if (IsPotion(itemId))
		{
			var customName = GameState.GetPotionDisplayName(itemId);
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

	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
