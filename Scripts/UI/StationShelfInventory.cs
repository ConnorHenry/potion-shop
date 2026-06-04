using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class StationShelfInventory : Control
{
	private const float SlotWidth = 104.0f;
	private const float SlotHeight = 106.0f;
	private const float IconSize = 54.0f;
	private const float IngredientSlotWidth = 116.0f;
	private const float IngredientSlotHeight = 160.0f;
	private const float IngredientJarWidth = 116.0f;
	private const float IngredientJarHeight = 120.0f;
	private const float IngredientJarIconSize = 76.0f;
	private const string IngredientJarOverlayPath = "res://Assets/UI/ingredient_jar_overlay.png";
	private const int IngredientDefaultVisibleSlots = 12;
	private const int ConsumableDefaultVisibleSlots = 4;
	private const int ShelfNameSingleLineCharacterLimit = 12;

	[Export] public NodePath IngredientSlotsPath = default!;
	[Export] public NodePath ConsumableSlotsPath = default!;
	[Export] public NodePath IngredientPreviousButtonPath = default!;
	[Export] public NodePath IngredientNextButtonPath = default!;
	[Export] public NodePath ConsumablePreviousButtonPath = default!;
	[Export] public NodePath ConsumableNextButtonPath = default!;
	[Export] public NodePath BrewPanelPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public int IngredientVisibleSlots = IngredientDefaultVisibleSlots;
	[Export] public int ConsumableVisibleSlots = ConsumableDefaultVisibleSlots;

	private GridContainer _ingredientSlots = default!;
	private GridContainer _consumableSlots = default!;
	private Button _ingredientPreviousButton = default!;
	private Button _ingredientNextButton = default!;
	private Button _consumablePreviousButton = default!;
	private Button _consumableNextButton = default!;
	private BrewPanel _brewPanel = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private int _ingredientPage;
	private int _consumablePage;

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

		_ingredientSlots = GetRequiredNode<GridContainer>(IngredientSlotsPath, nameof(IngredientSlotsPath));
		_consumableSlots = GetRequiredNode<GridContainer>(ConsumableSlotsPath, nameof(ConsumableSlotsPath));
		_ingredientPreviousButton = GetRequiredNode<Button>(IngredientPreviousButtonPath, nameof(IngredientPreviousButtonPath));
		_ingredientNextButton = GetRequiredNode<Button>(IngredientNextButtonPath, nameof(IngredientNextButtonPath));
		_consumablePreviousButton = GetRequiredNode<Button>(ConsumablePreviousButtonPath, nameof(ConsumablePreviousButtonPath));
		_consumableNextButton = GetRequiredNode<Button>(ConsumableNextButtonPath, nameof(ConsumableNextButtonPath));
		if (_ingredientSlots is null ||
			_consumableSlots is null ||
			_ingredientPreviousButton is null ||
			_ingredientNextButton is null ||
			_consumablePreviousButton is null ||
			_consumableNextButton is null)
		{
			return;
		}

		MouseFilter = MouseFilterEnum.Ignore;
		_ingredientPreviousButton.Pressed += ShowPreviousIngredientPage;
		_ingredientNextButton.Pressed += ShowNextIngredientPage;
		_consumablePreviousButton.Pressed += ShowPreviousConsumablePage;
		_consumableNextButton.Pressed += ShowNextConsumablePage;
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
	}

	public void Refresh()
	{
		if (_gameState is null || _itemCatalog is null || _ingredientSlots is null || _consumableSlots is null)
			return;

		var ingredientStacks = BuildShelfStacks(includeIngredients: true);
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

			stacks.Add(new ShelfStack(stack.Key, item.Name, item.IconPath, stack.Value));
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

	private void RenderPage(
		GridContainer container,
		IReadOnlyList<ShelfStack> stacks,
		int page,
		int visibleSlotCount,
		bool connectIngredientRequest)
	{
		ClearContainer(container);

		var startIndex = page * visibleSlotCount;
		var endIndex = Math.Min(stacks.Count, startIndex + visibleSlotCount);
		for (var i = startIndex; i < endIndex; i++)
			container.AddChild(CreateShelfSlot(stacks[i], connectIngredientRequest));
	}

	private InventoryItemSlot CreateShelfSlot(ShelfStack stack, bool connectIngredientRequest)
	{
		var slotSize = connectIngredientRequest
			? new Vector2(IngredientSlotWidth, IngredientSlotHeight)
			: new Vector2(SlotWidth, SlotHeight);
		var slot = new InventoryItemSlot
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = MouseFilterEnum.Stop,
			Flat = false,
			TooltipText = stack.Name,
			ItemId = stack.ItemId,
			ItemName = stack.Name,
			IconPath = stack.IconPath,
			Quantity = stack.Quantity
		};
		slot.AddThemeStyleboxOverride("normal", CreateSlotStyleBox(new Color(0.08f, 0.055f, 0.035f, 0.08f), new Color(0.36f, 0.24f, 0.13f, 0.16f)));
		slot.AddThemeStyleboxOverride("hover", CreateSlotStyleBox(new Color(0.16f, 0.1f, 0.055f, 0.32f), new Color(0.74f, 0.48f, 0.2f, 0.6f)));
		slot.AddThemeStyleboxOverride("pressed", CreateSlotStyleBox(new Color(0.06f, 0.038f, 0.024f, 0.38f), new Color(0.48f, 0.29f, 0.13f, 0.62f)));
		slot.AddThemeStyleboxOverride("disabled", CreateSlotStyleBox(new Color(0.05f, 0.04f, 0.034f, 0.12f), new Color(0.22f, 0.17f, 0.12f, 0.22f)));
		if (connectIngredientRequest)
			slot.IngredientRequested += QueueIngredientFromShelf;

		var content = new Control
		{
			CustomMinimumSize = slotSize,
			Size = slotSize,
			MouseFilter = MouseFilterEnum.Ignore
		};

		var quantity = new Label
		{
			Text = stack.Quantity.ToString(),
			Position = connectIngredientRequest ? new Vector2(8.0f, 39.0f) : new Vector2(8.0f, 6.0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		quantity.AddThemeColorOverride("font_color", new Color(0.98f, 0.9f, 0.62f, 1.0f));
		quantity.AddThemeFontSizeOverride("font_size", 15);

		var name = new Label
		{
			Text = FormatShelfSlotName(stack.Name),
			Position = connectIngredientRequest ? new Vector2(5.0f, 2.0f) : new Vector2(5.0f, 70.0f),
			CustomMinimumSize = new Vector2(slotSize.X - 10.0f, 34.0f),
			Size = new Vector2(slotSize.X - 10.0f, 34.0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		name.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f, 1.0f));
		name.AddThemeFontSizeOverride("font_size", 12);

		content.AddChild(connectIngredientRequest ? CreateIngredientJar(stack) : CreateShelfIcon(stack.IconPath));
		content.AddChild(quantity);
		content.AddChild(name);
		slot.AddChild(content);
		return slot;
	}

	private static Control CreateShelfIcon(string? iconPath)
	{
		return new TextureRect
		{
			Position = new Vector2((SlotWidth - IconSize) * 0.5f, 12.0f),
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			Size = new Vector2(IconSize, IconSize),
			Texture = UiIconLoader.LoadIcon(iconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
	}

	private static Control CreateIngredientJar(ShelfStack stack)
	{
		var jar = new Control
		{
			Position = new Vector2((IngredientSlotWidth - IngredientJarWidth) * 0.5f, 36.0f),
			CustomMinimumSize = new Vector2(IngredientJarWidth, IngredientJarHeight),
			Size = new Vector2(IngredientJarWidth, IngredientJarHeight),
			MouseFilter = MouseFilterEnum.Ignore
		};

		var icon = new TextureRect
		{
			Position = new Vector2((IngredientJarWidth - IngredientJarIconSize) * 0.5f, 40.0f),
			CustomMinimumSize = new Vector2(IngredientJarIconSize, IngredientJarIconSize),
			Size = new Vector2(IngredientJarIconSize, IngredientJarIconSize),
			Texture = UiIconLoader.LoadIcon(stack.IconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};

		var overlay = new TextureRect
		{
			Position = Vector2.Zero,
			CustomMinimumSize = new Vector2(IngredientJarWidth, IngredientJarHeight),
			Size = new Vector2(IngredientJarWidth, IngredientJarHeight),
			Texture = UiIconLoader.LoadIcon(IngredientJarOverlayPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};

		jar.AddChild(icon);
		jar.AddChild(overlay);
		return jar;
	}

	private static string FormatShelfSlotName(string itemName)
	{
		if (string.IsNullOrWhiteSpace(itemName))
			return itemName;

		var trimmedName = itemName.Trim();
		if (trimmedName.Length <= ShelfNameSingleLineCharacterLimit)
			return trimmedName;

		InventoryItemTextFormatter.SplitInventoryName(trimmedName, out var firstLine, out var secondLine);
		return string.IsNullOrWhiteSpace(secondLine)
			? firstLine
			: $"{firstLine}\n{secondLine}";
	}

	private void QueueIngredientFromShelf(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;

		if (!_itemCatalog.IsIngredient(itemId))
			return;

		if (!_brewPanel.Visible)
			_brewPanel.ShowPanel();

		_brewPanel.TryQueueIngredient(itemId);
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
		var maxPage = GetMaxPage(BuildShelfStacks(includeIngredients: true).Count, visibleSlots);
		if (_ingredientPage >= maxPage)
			return;

		_ingredientPage += 1;
		Refresh();
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

	private TNode GetRequiredNode<TNode>(NodePath path, string exportName) where TNode : Node
	{
		var node = GetNodeOrNull<TNode>(path);
		if (node is null)
		{
			GD.PushError($"StationShelfInventory: {exportName} was not found at '{path}'.");
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
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomRight = 5,
			CornerRadiusBottomLeft = 5
		};
	}

	private readonly record struct ShelfStack(string ItemId, string Name, string? IconPath, int Quantity);
}
