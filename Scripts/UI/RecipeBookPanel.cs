using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class RecipeBookPanel : Control
{
	private const string AllFilterLabel = "All";
	private const string SortAzLabel = "A-Z";
	private const int CardTitleFontSize = 22;
	private const int DetailHeadingFontSize = 14;
	private const int DetailValueFontSize = 13;
	private const int ButtonFontSize = 14;
	private const int TagFontSize = 13;

	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath ResetButtonPath = default!;
	[Export] public NodePath SortFilterPath = default!;
	[Export] public NodePath TraitFilterPath = default!;
	[Export] public NodePath RiskFilterPath = default!;
	[Export] public NodePath SearchInputPath = default!;
	[Export] public NodePath RecipesContainerPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private Button _closeButton = default!;
	private Button? _resetButton;
	private OptionButton? _sortFilter;
	private OptionButton? _traitFilter;
	private OptionButton? _riskFilter;
	private LineEdit? _searchInput;
	private VBoxContainer _recipes = default!;
	private PotionInventoryBrewService _brewService = default!;
	private string? _activeTraitFilter;
	private string? _activeRiskFilter;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"RecipeBookPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"RecipeBookPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewService = new PotionInventoryBrewService(_gameState, _itemCatalog);

		_closeButton = GetNode<Button>(CloseButtonPath);
		_resetButton = GetNodeOrNull<Button>(ResetButtonPath);
		_sortFilter = GetNodeOrNull<OptionButton>(SortFilterPath);
		_traitFilter = GetNodeOrNull<OptionButton>(TraitFilterPath);
		_riskFilter = GetNodeOrNull<OptionButton>(RiskFilterPath);
		_searchInput = GetNodeOrNull<LineEdit>(SearchInputPath);
		_recipes = GetNode<VBoxContainer>(RecipesContainerPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		if (_resetButton is not null)
			_resetButton.Pressed += ClearFilters;
		if (_sortFilter is not null)
		{
			_sortFilter.ItemSelected += OnSortSelected;
			InitializeSortFilter(_sortFilter);
		}
		if (_traitFilter is not null)
			_traitFilter.ItemSelected += OnTraitSelected;
		if (_riskFilter is not null)
			_riskFilter.ItemSelected += OnRiskSelected;
		_gameState.Changed += Refresh;

		Visible = false;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_resetButton is not null)
			_resetButton.Pressed -= ClearFilters;
		if (_sortFilter is not null)
			_sortFilter.ItemSelected -= OnSortSelected;
		if (_traitFilter is not null)
			_traitFilter.ItemSelected -= OnTraitSelected;
		if (_riskFilter is not null)
			_riskFilter.ItemSelected -= OnRiskSelected;
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (Visible)
			Refresh();
	}

	public void HidePanel()
	{
		Visible = false;
	}

	private static void InitializeSortFilter(OptionButton sortFilter)
	{
		sortFilter.Clear();
		sortFilter.AddItem($"Sort: {SortAzLabel}");
		sortFilter.Selected = 0;
	}

	private void OnSortSelected(long _selectedIndex)
	{
		Refresh();
	}

	private void ClearFilters()
	{
		_activeTraitFilter = null;
		_activeRiskFilter = null;
		if (_searchInput is not null)
			_searchInput.Text = string.Empty;
		if (_traitFilter is not null)
			_traitFilter.Selected = 0;
		if (_riskFilter is not null)
			_riskFilter.Selected = 0;
		if (_sortFilter is not null)
			_sortFilter.Selected = 0;
		Refresh();
	}

	private void Refresh()
	{
		foreach (var child in _recipes.GetChildren())
			child.QueueFree();

		var learnedPotionIds = GetLearnedPotionEntries();
		var potionIds = learnedPotionIds.Select(x => x.PotionId).ToList();
		var traitNames = ItemFilterUtilities.BuildTopTraitNames(potionIds, 3, _itemCatalog);
		var riskNames = ItemFilterUtilities.BuildRiskNames(potionIds, _itemCatalog);
		ItemFilterUtilities.RefreshFilterOptions(_traitFilter, traitNames, AllFilterLabel, ref _activeTraitFilter);
		ItemFilterUtilities.RefreshFilterOptions(_riskFilter, riskNames, AllFilterLabel, ref _activeRiskFilter);

		if (!string.IsNullOrWhiteSpace(_activeTraitFilter))
			learnedPotionIds = learnedPotionIds.Where(entry => ItemFilterUtilities.ItemHasTrait(entry.PotionId, _activeTraitFilter, _itemCatalog, topCount: 3)).ToList();
		if (!string.IsNullOrWhiteSpace(_activeRiskFilter))
			learnedPotionIds = learnedPotionIds.Where(entry => ItemFilterUtilities.ItemHasRisk(entry.PotionId, _activeRiskFilter, _itemCatalog)).ToList();

		learnedPotionIds = learnedPotionIds
			.OrderBy(x => x.Name)
			.ThenBy(x => x.PotionId)
			.ToList();

		if (learnedPotionIds.Count == 0)
		{
			_recipes.AddChild(new Label
			{
				Text = _gameState.KnownPotions.Any(IsKnownBrewedPotion)
					? "No brewed recipes match the selected filters."
					: "No brewed recipes yet."
			});
			return;
		}

		foreach (var entry in learnedPotionIds)
			_recipes.AddChild(CreateRecipeCard(entry.PotionId));
	}

	private void OnTraitSelected(long selectedIndex)
	{
		if (_traitFilter is null)
			return;

		HandleFilterSelected(_traitFilter, selectedIndex, AllFilterLabel, ref _activeTraitFilter);
	}

	private void OnRiskSelected(long selectedIndex)
	{
		if (_riskFilter is null)
			return;

		HandleFilterSelected(_riskFilter, selectedIndex, AllFilterLabel, ref _activeRiskFilter);
	}

	private void HandleFilterSelected(OptionButton? filter, long selectedIndex, string placeholderLabel, ref string? activeFilter)
	{
		if (filter is null)
			return;

		var selectedValue = filter.GetItemText((int)selectedIndex);
		if (string.Equals(selectedValue, placeholderLabel, System.StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrWhiteSpace(activeFilter))
			{
				activeFilter = null;
				Refresh();
			}

			return;
		}

		if (string.Equals(activeFilter, selectedValue, System.StringComparison.OrdinalIgnoreCase))
			activeFilter = null;
		else
			activeFilter = selectedValue;

		Refresh();
	}

	private List<LearnedPotionEntry> GetLearnedPotionEntries()
	{
		return _gameState.KnownPotions
			.Where(IsKnownBrewedPotion)
			.Select(id => new LearnedPotionEntry(id, DisplayName(id, ItemName(id))))
			.ToList();
	}

	private Control CreateRecipeCard(string potionId)
	{
		if (!_itemCatalog.TryGetItem(potionId, out var item))
			return new Label { Text = potionId };

		if (!_brewService.TryGetRequiredIngredients(potionId, out var requiredIngredients, out var error))
		{
			return new Label
			{
				Text = $"Unable to render recipe '{potionId}': {error}"
			};
		}

		var availabilityEntries = BuildIngredientAvailabilityEntries(requiredIngredients);
		var missingCount = availabilityEntries.Count(entry => !entry.IsAvailable);
		var isBrewable = missingCount == 0;

		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 154)
		};
		ApplyCardStyle(card);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);

		var cardBody = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		cardBody.AddThemeConstantOverride("separation", 10);

		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(58, 58),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		icon.Texture = UiIconLoader.LoadIcon(item.IconPath);

		var iconColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(64, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		iconColumn.AddThemeConstantOverride("separation", 6);
		iconColumn.AddChild(icon);
		iconColumn.AddChild(new Control
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		});

		var contentColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		contentColumn.AddThemeConstantOverride("separation", 8);

		var topRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin
		};
		topRow.AddThemeConstantOverride("separation", 8);

		var headingColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		headingColumn.AddThemeConstantOverride("separation", 5);

		var title = new Label
		{
			Text = DisplayName(potionId, item.Name),
			ClipText = true
		};
		title.AddThemeFontSizeOverride("font_size", CardTitleFontSize);

		var statusTag = CreateStatusTag(isBrewable, missingCount);

		var actionColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(116, 0),
			SizeFlagsVertical = SizeFlags.ShrinkBegin
		};

		var brewButton = new Button
		{
			Text = "Brew",
			CustomMinimumSize = new Vector2(104, 36),
			SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
			Disabled = !isBrewable
		};
		ApplyPrimaryButtonStyle(brewButton);
		brewButton.TooltipText = isBrewable
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
		brewButton.Pressed += () => TryBrewPotion(potionId);

		actionColumn.AddChild(brewButton);

		headingColumn.AddChild(title);
		headingColumn.AddChild(statusTag);
		topRow.AddChild(headingColumn);
		topRow.AddChild(actionColumn);

		var divider = new HSeparator();
		divider.AddThemeColorOverride("separator", new Color("46566b"));

		var detailsRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		detailsRow.AddThemeConstantOverride("separation", 8);

		var ingredientsColumn = CreateDetailsColumn(
			"Ingredients",
			CreateIngredientLines(availabilityEntries),
			new Color("60d97a"),
			3.0f);

		var traitsColumn = CreateDetailsColumn(
			"Traits",
			BuildStatLines(item.Traits, new Color("7be291"), 3),
			new Color("60d97a"),
			1.5f);

		var risksColumn = CreateDetailsColumn(
			"Risks",
			BuildStatLines(item.Risks, new Color("ff5959"), 2),
			new Color("ff5757"),
			1.5f);

		detailsRow.AddChild(ingredientsColumn);
		detailsRow.AddChild(CreateVerticalSeparator());
		detailsRow.AddChild(traitsColumn);
		detailsRow.AddChild(CreateVerticalSeparator());
		detailsRow.AddChild(risksColumn);

		contentColumn.AddChild(topRow);
		contentColumn.AddChild(divider);
		contentColumn.AddChild(detailsRow);
		cardBody.AddChild(iconColumn);
		cardBody.AddChild(contentColumn);
		margin.AddChild(cardBody);
		card.AddChild(margin);
		return card;
	}

	private static Control CreateVerticalSeparator()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(1, 0),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Color = new Color("3f5166")
		};
	}

	private static VBoxContainer CreateDetailsColumn(
		string heading,
		IReadOnlyList<(string Text, Color Color)> lines,
		Color headingColor,
		float stretchRatio)
	{
		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretchRatio
		};
		column.AddThemeConstantOverride("separation", 3);

		var headingLabel = new Label
		{
			Text = heading
		};
		headingLabel.AddThemeColorOverride("font_color", headingColor);
		headingLabel.AddThemeFontSizeOverride("font_size", DetailHeadingFontSize);

		column.AddChild(headingLabel);

		foreach (var line in lines)
		{
			var valueLabel = new Label
			{
				Text = line.Text,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			valueLabel.AddThemeColorOverride("font_color", line.Color);
			valueLabel.AddThemeFontSizeOverride("font_size", DetailValueFontSize);
			column.AddChild(valueLabel);
		}

		return column;
	}

	private static IReadOnlyList<(string Text, Color Color)> BuildStatLines(Dictionary<string, int> values, Color valueColor, int maxCount)
	{
		var lines = new List<(string Text, Color Color)>();
		var sortedValues = values is null
			? new List<KeyValuePair<string, int>>()
			: values.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key).ToList();
		var consumed = 0;

		foreach (var entry in sortedValues)
		{
			if (string.IsNullOrWhiteSpace(entry.Key))
				continue;
			if (entry.Value <= 0)
				continue;
			if (consumed >= maxCount)
				break;

			lines.Add(($"{ToDisplayStatName(entry.Key)} +{entry.Value}", valueColor));
			consumed += 1;
		}

		if (lines.Count == 0)
			lines.Add(("None", new Color("c1c9d4")));

		return lines;
	}

	private static string ToDisplayStatName(string rawName)
	{
		if (string.IsNullOrWhiteSpace(rawName))
			return "Unknown";

		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant());
	}

	private static IReadOnlyList<(string Text, Color Color)> CreateIngredientLines(
		IReadOnlyList<IngredientAvailabilityEntry> availabilityEntries)
	{
		var lines = new List<(string Text, Color Color)>(availabilityEntries.Count);

		foreach (var entry in availabilityEntries)
		{
			var prefix = entry.IsAvailable ? "v" : "X";
			var color = entry.IsAvailable ? new Color("7be291") : new Color("ff5959");
			lines.Add(($"{prefix}  {entry.DisplayName}", color));
		}

		if (lines.Count == 0)
			lines.Add(("None", new Color("c1c9d4")));

		return lines;
	}

	private Control CreateStatusTag(bool isBrewable, int missingCount)
	{
		var tagContainer = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin
		};
		tagContainer.AddThemeStyleboxOverride("panel", isBrewable
			? CreateTagStyleBox(new Color("1f5938"), new Color("2db766"))
			: CreateTagStyleBox(new Color("5f4e1a"), new Color("d3a73b")));

		var tagMargin = new MarginContainer();
		tagMargin.AddThemeConstantOverride("margin_left", 7);
		tagMargin.AddThemeConstantOverride("margin_top", 3);
		tagMargin.AddThemeConstantOverride("margin_right", 7);
		tagMargin.AddThemeConstantOverride("margin_bottom", 3);

		var tagLabel = new Label
		{
			Text = isBrewable ? "v  Brewable" : $"!  Missing {missingCount}"
		};
		tagLabel.AddThemeColorOverride("font_color", isBrewable ? new Color("74f3a1") : new Color("f5d064"));
		tagLabel.AddThemeFontSizeOverride("font_size", TagFontSize);

		tagMargin.AddChild(tagLabel);
		tagContainer.AddChild(tagMargin);
		return tagContainer;
	}

	private static StyleBoxFlat CreateTagStyleBox(Color fillColor, Color borderColor)
	{
		return new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderColor = borderColor,
			BorderWidthBottom = 1,
			BorderWidthTop = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			CornerRadiusTopLeft = 9,
			CornerRadiusTopRight = 9,
			CornerRadiusBottomRight = 9,
			CornerRadiusBottomLeft = 9
		};
	}

	private static void ApplyCardStyle(PanelContainer card)
	{
		card.AddThemeStyleboxOverride("panel", CreatePanelStyleBox(
			new Color("0f1924e6"),
			new Color("324455f2"),
			7,
			1));
	}

	private static void ApplyPrimaryButtonStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", CreateButtonStyleBox(new Color("1f5938"), new Color("3fb26b"), 6));
		button.AddThemeStyleboxOverride("hover", CreateButtonStyleBox(new Color("286f46"), new Color("50d47e"), 6));
		button.AddThemeStyleboxOverride("pressed", CreateButtonStyleBox(new Color("18452b"), new Color("2d9d5c"), 6));
		button.AddThemeStyleboxOverride("disabled", CreateButtonStyleBox(new Color("283232"), new Color("4d5a5a"), 6));
		button.AddThemeColorOverride("font_color", new Color("f2fff7"));
		button.AddThemeColorOverride("font_disabled_color", new Color("9ba9a9"));
		button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
	}

	private static StyleBoxFlat CreateButtonStyleBox(Color fillColor, Color borderColor, int radius)
	{
		var style = CreatePanelStyleBox(fillColor, borderColor, radius, 1);
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		return style;
	}

	private static StyleBoxFlat CreatePanelStyleBox(Color fillColor, Color borderColor, int radius, int borderWidth)
	{
		return new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderColor = borderColor,
			BorderWidthBottom = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius
		};
	}

	private List<IngredientAvailabilityEntry> BuildIngredientAvailabilityEntries(Dictionary<string, int> requiredIngredients)
	{
		var entries = new List<IngredientAvailabilityEntry>();

		foreach (var pair in requiredIngredients.OrderBy(entry => ItemName(entry.Key)).ThenBy(entry => entry.Key))
		{
			var have = _gameState.Inventory.GetValueOrDefault(pair.Key);
			entries.Add(new IngredientAvailabilityEntry(
				ItemName(pair.Key),
				have >= pair.Value));
		}

		return entries;
	}

	private void TryBrewPotion(string potionId)
	{
		if (_brewService.TryBrewPotion(potionId, out var error))
			return;

		GD.PushError(error);
	}

	private bool IsKnownBrewedPotion(string potionId)
	{
		return _itemCatalog.IsPotion(potionId);
	}

	private string ItemName(string itemId)
	{
		return _itemCatalog.GetItemName(itemId);
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		var customName = _gameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private readonly record struct LearnedPotionEntry(string PotionId, string Name);
	private readonly record struct IngredientAvailabilityEntry(string DisplayName, bool IsAvailable);
}


