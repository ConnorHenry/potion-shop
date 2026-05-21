using System.Linq;
using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class InventoryPanel : Control
{
	private const float SlotSize = 112.0f;
	private const float IconSize = 70.0f;

	[Export] public NodePath PotionsContainerPath = default!;
	[Export] public NodePath IngredientsContainerPath = default!;
	[Export] public NodePath PotionsSortButtonPath = default!;
	[Export] public NodePath IngredientsSortButtonPath = default!;
	[Export] public NodePath ItemDetailPanelPath = default!;
	[Export] public NodePath ItemDetailImagePath = default!;
	[Export] public NodePath ItemDetailNamePath = default!;
	[Export] public NodePath ItemDetailTraitsHeaderPath = default!;
	[Export] public NodePath ItemDetailTraitsPath = default!;
	[Export] public NodePath ItemDetailRisksHeaderPath = default!;
	[Export] public NodePath ItemDetailRisksPath = default!;
	[Export] public NodePath ItemDetailDescriptionPath = default!;
	[Export] public NodePath ItemDetailCloseButtonPath = default!;

	private GridContainer _potions = default!;
	private GridContainer _ingredients = default!;
	private Button _potionsSortButton = default!;
	private Button _ingredientsSortButton = default!;
	private Control _itemDetailPanel = default!;
	private TextureRect _itemDetailImage = default!;
	private Label _itemDetailName = default!;
	private Label _itemDetailTraitsHeader = default!;
	private RichTextLabel _itemDetailTraits = default!;
	private Label _itemDetailRisksHeader = default!;
	private RichTextLabel _itemDetailRisks = default!;
	private RichTextLabel _itemDetailDescription = default!;
	private Button _itemDetailCloseButton = default!;
	private string? _currentItemId;
	private bool _potionsAscending = true;
	private bool _ingredientsAscending = true;

	public override void _Ready()
	{
		_potions = GetNode<GridContainer>(PotionsContainerPath);
		_ingredients = GetNode<GridContainer>(IngredientsContainerPath);
		_potionsSortButton = GetNode<Button>(PotionsSortButtonPath);
		_ingredientsSortButton = GetNode<Button>(IngredientsSortButtonPath);
		_itemDetailPanel = GetNode<Control>(ItemDetailPanelPath);
		_itemDetailImage = GetNode<TextureRect>(ItemDetailImagePath);
		_itemDetailName = GetNode<Label>(ItemDetailNamePath);
		_itemDetailTraitsHeader = GetNode<Label>(ItemDetailTraitsHeaderPath);
		_itemDetailTraits = GetNode<RichTextLabel>(ItemDetailTraitsPath);
		_itemDetailRisksHeader = GetNode<Label>(ItemDetailRisksHeaderPath);
		_itemDetailRisks = GetNode<RichTextLabel>(ItemDetailRisksPath);
		_itemDetailDescription = GetNode<RichTextLabel>(ItemDetailDescriptionPath);
		_itemDetailCloseButton = GetNode<Button>(ItemDetailCloseButtonPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_itemDetailPanel.MouseFilter = MouseFilterEnum.Ignore;
		_potionsSortButton.Pressed += TogglePotionsSort;
		_ingredientsSortButton.Pressed += ToggleIngredientsSort;
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
		{
			_ingredients.AddChild(new Label { Text = "Empty" });
			return;
		}

		var potionStacks = GameState.Inventory.Where(x => IsPotion(x.Key));
		var ingredientStacks = GameState.Inventory.Where(x => !IsPotion(x.Key));

		if (_potionsAscending)
		{
			foreach (var stack in potionStacks.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
				_potions.AddChild(CreateSlot(stack.Key, stack.Value));
		}
		else
		{
			foreach (var stack in potionStacks.OrderByDescending(x => ItemName(x.Key)).ThenByDescending(x => x.Key))
				_potions.AddChild(CreateSlot(stack.Key, stack.Value));
		}

		if (_ingredientsAscending)
		{
			foreach (var stack in ingredientStacks.OrderBy(x => ItemName(x.Key)).ThenBy(x => x.Key))
				_ingredients.AddChild(CreateSlot(stack.Key, stack.Value));
		}
		else
		{
			foreach (var stack in ingredientStacks.OrderByDescending(x => ItemName(x.Key)).ThenByDescending(x => x.Key))
				_ingredients.AddChild(CreateSlot(stack.Key, stack.Value));
		}
	}

	private void UpdateSortButtonLabels()
	{
		_potionsSortButton.Text = _potionsAscending ? "A-Z" : "Z-A";
		_ingredientsSortButton.Text = _ingredientsAscending ? "A-Z" : "Z-A";
	}

	private Control CreateSlot(string itemId, int quantity)
	{
		var item = DataDb.Items.TryGetValue(itemId, out var def) ? def : null;
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
			Position = new Vector2(4, 80),
			CustomMinimumSize = new Vector2(SlotSize - 8, 26),
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Arbitrary,
			ClipText = true
		};

		content.AddChild(icon);
		content.AddChild(qty);
		content.AddChild(name);
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

		if (!DataDb.Items.TryGetValue(itemId, out var item))
			return;

		_currentItemId = itemId;
		_itemDetailImage.Texture = LoadIcon(item.IconPath);
		_itemDetailName.Text = DisplayName(itemId, item.Name);
		_itemDetailDescription.Text = item.Description;
		_itemDetailTraits.Text = FormatTopTraits(item.Traits, 3);
		_itemDetailRisks.Text = FormatDictionary(item.Risks);
		_itemDetailPanel.Visible = true;
	}

	private void HideItemDetail()
	{
		_currentItemId = null;
		_itemDetailImage.Texture = null;
		_itemDetailTraits.Text = "";
		_itemDetailRisks.Text = "";
		_itemDetailDescription.Text = "";
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
		return DataDb.Items.TryGetValue(itemId, out var item)
			? DisplayName(itemId, item.Name)
			: itemId;
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

	private static bool IsPotion(string itemId)
	{
		if (!DataDb.Items.TryGetValue(itemId, out var item))
			return false;

		return item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
	}

	private static string FormatDictionary(Dictionary<string, int> values)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join("\n",
			values
				.OrderBy(x => x.Key)
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

	private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
	private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
