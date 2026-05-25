using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class RecipeBookPanel : Control
{
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath SortButtonPath = default!;
	[Export] public NodePath TraitFilterPath = default!;
	[Export] public NodePath RiskFilterPath = default!;
	[Export] public NodePath RecipesContainerPath = default!;

	private Button _closeButton = default!;
	private Button? _clearButton;
	private Button _sortButton = default!;
	private OptionButton? _traitFilter;
	private OptionButton? _riskFilter;
	private VBoxContainer _recipes = default!;
	private readonly PotionInventoryBrewService _brewService = new();
	private bool _ascending = true;
	private string? _activeTraitFilter;
	private string? _activeRiskFilter;
	private GameState _gameState = default!;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("RecipeBookPanel: /root/GameState was not found.");
			return;
		}
		_gameState = gameState;

		_closeButton = GetNode<Button>(CloseButtonPath);
		_clearButton = GetNodeOrNull<Button>(ClearButtonPath);
		_sortButton = GetNode<Button>(SortButtonPath);
		_traitFilter = GetNodeOrNull<OptionButton>(TraitFilterPath);
		_riskFilter = GetNodeOrNull<OptionButton>(RiskFilterPath);
		_recipes = GetNode<VBoxContainer>(RecipesContainerPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		if (_clearButton is not null)
			_clearButton.Pressed += ClearFilters;
		_sortButton.Pressed += ToggleSortOrder;
		if (_traitFilter is not null)
			_traitFilter.ItemSelected += OnTraitSelected;
		if (_riskFilter is not null)
			_riskFilter.ItemSelected += OnRiskSelected;
		_gameState.Changed += Refresh;

		Visible = false;
		UpdateSortButtonLabel();
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearFilters;
		if (_sortButton is not null)
			_sortButton.Pressed -= ToggleSortOrder;
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

	private void ToggleSortOrder()
	{
		_ascending = !_ascending;
		UpdateSortButtonLabel();
		Refresh();
	}

	private void ClearFilters()
	{
		if (_traitFilter is null && _riskFilter is null)
			return;

		if (string.IsNullOrWhiteSpace(_activeTraitFilter) && string.IsNullOrWhiteSpace(_activeRiskFilter))
		{
			if (_traitFilter is not null)
				_traitFilter.Selected = 0;
			if (_riskFilter is not null)
				_riskFilter.Selected = 0;
			return;
		}

		_activeTraitFilter = null;
		_activeRiskFilter = null;
		Refresh();
	}

	private void Refresh()
	{
		foreach (var child in _recipes.GetChildren())
			child.QueueFree();

		var learnedPotionIds = GetLearnedPotionEntries();
		var traitNames = BuildTopTraitNames(learnedPotionIds, 3);
		var riskNames = BuildRiskNames(learnedPotionIds);
		RefreshFilterOptions(_traitFilter, traitNames, "Trait", ref _activeTraitFilter);
		RefreshFilterOptions(_riskFilter, riskNames, "Risk", ref _activeRiskFilter);

		if (!string.IsNullOrWhiteSpace(_activeTraitFilter))
			learnedPotionIds = learnedPotionIds.Where(entry => ItemHasTrait(entry.PotionId, _activeTraitFilter)).ToList();
		if (!string.IsNullOrWhiteSpace(_activeRiskFilter))
			learnedPotionIds = learnedPotionIds.Where(entry => ItemHasRisk(entry.PotionId, _activeRiskFilter)).ToList();

		learnedPotionIds = _ascending
			? learnedPotionIds.OrderBy(x => x.Name).ThenBy(x => x.PotionId).ToList()
			: learnedPotionIds.OrderByDescending(x => x.Name).ThenByDescending(x => x.PotionId).ToList();

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

	private void UpdateSortButtonLabel()
	{
		_sortButton.Text = _ascending ? "A-Z" : "Z-A";
	}

	private void OnTraitSelected(long selectedIndex)
	{
		if (_traitFilter is null)
			return;

		HandleFilterSelected(_traitFilter, selectedIndex, "Trait", ref _activeTraitFilter);
	}

	private void OnRiskSelected(long selectedIndex)
	{
		if (_riskFilter is null)
			return;

		HandleFilterSelected(_riskFilter, selectedIndex, "Risk", ref _activeRiskFilter);
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

	private static void RefreshFilterOptions(OptionButton? filter, List<string> values, string placeholderLabel, ref string? activeFilter)
	{
		if (filter is null)
			return;

		filter.Clear();
		filter.AddItem(placeholderLabel);

		foreach (var value in values)
			filter.AddItem(value);

		if (string.IsNullOrWhiteSpace(activeFilter))
		{
			filter.Selected = 0;
			return;
		}

		for (var index = 1; index < filter.ItemCount; index++)
		{
			var itemText = filter.GetItemText(index);
			if (!string.Equals(itemText, activeFilter, System.StringComparison.OrdinalIgnoreCase))
				continue;

			filter.Selected = index;
			return;
		}

		activeFilter = null;
		filter.Selected = 0;
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
        if (!ItemCatalog.TryGetItem(potionId, out var item))
            return new Label { Text = potionId };

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 170)
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(72, 72),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        icon.Texture = LoadIcon(item.IconPath);

        var textColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        textColumn.AddThemeConstantOverride("separation", 6);

        var title = new Label
        {
            Text = DisplayName(potionId, item.Name)
        };

        var ingredients = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = _brewService.BuildIngredientAvailabilityText(potionId, includeHeading: true),
            FitContent = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };

        var traits = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = $"[color=#59d65f]{FormatTraitDetails(item.Traits, 3)}[/color]",
            FitContent = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };

        var risks = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = $"[color=#e04a4a]{FormatRiskDetails(item.Risks)}[/color]",
            FitContent = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };

        var infoRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 72)
        };
        infoRow.AddThemeConstantOverride("separation", 12);

        var ingredientColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 0),
            SizeFlagsStretchRatio = 3.0f
        };
        ingredientColumn.AddThemeConstantOverride("separation", 4);

        var traitsColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsStretchRatio = 1.0f
        };
        traitsColumn.AddThemeConstantOverride("separation", 4);

        var risksColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsStretchRatio = 1.0f
        };
        risksColumn.AddThemeConstantOverride("separation", 4);

		var description = new RichTextLabel
		{
			Text = item.Description,
			FitContent = true,
			AutowrapMode = TextServer.AutowrapMode.Arbitrary
		};

		var brewButton = new Button
		{
			Text = "Brew"
		};
		brewButton.Pressed += () => TryBrewPotion(potionId);
		UpdateBrewButtonState(potionId, brewButton);

		ingredientColumn.AddChild(ingredients);
		traitsColumn.AddChild(traits);
		risksColumn.AddChild(risks);
		infoRow.AddChild(ingredientColumn);
		infoRow.AddChild(traitsColumn);
		infoRow.AddChild(risksColumn);
		textColumn.AddChild(title);
		textColumn.AddChild(infoRow);
		textColumn.AddChild(description);
		textColumn.AddChild(brewButton);

		row.AddChild(icon);
		row.AddChild(textColumn);
		margin.AddChild(row);
		card.AddChild(margin);
		return card;
	}

	private void TryBrewPotion(string potionId)
	{
		if (_brewService.TryBrewPotion(potionId, out var error))
			return;

		GD.PushError(error);
	}

	private void UpdateBrewButtonState(string potionId, Button brewButton)
	{
		if (!_brewService.TryGetRequiredIngredients(potionId, out var requiredIngredients, out var error))
		{
			brewButton.Disabled = true;
			brewButton.TooltipText = error;
			return;
		}

		var hasIngredients = _brewService.HasRequiredIngredients(requiredIngredients);
		brewButton.Disabled = !hasIngredients;
		brewButton.TooltipText = hasIngredients
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
	}

	private static string FormatDictionary(Dictionary<string, int> values)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join(", ",
			values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Select(x => $"{x.Key}: {x.Value}"));
	}

	private static string FormatTopTraits(Dictionary<string, int> values, int maxCount)
	{
		if (values is null || values.Count == 0)
			return "None";

		return string.Join(", ",
			values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount)
				.Select(x => $"{x.Key}: {x.Value}"));
	}

	private static string FormatTraitDetails(Dictionary<string, int> values, int maxCount)
	{
		var entries = values is null
			? new List<KeyValuePair<string, int>>()
			: values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Take(maxCount)
				.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
				.ToList();

		return FormatMultilineDetails("Traits", entries);
	}

	private static string FormatRiskDetails(Dictionary<string, int> values)
	{
		var entries = values is null
			? new List<KeyValuePair<string, int>>()
			: values
				.OrderByDescending(x => x.Value)
				.ThenBy(x => x.Key)
				.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
				.ToList();

		return FormatMultilineDetails("Risks", entries);
	}

	private static string FormatMultilineDetails(string heading, IReadOnlyList<KeyValuePair<string, int>> entries)
	{
		if (entries.Count == 0)
			return $"{heading}:\nNone";

		var lines = new List<string>(entries.Count);
		foreach (var entry in entries)
			lines.Add($"{entry.Key}: {entry.Value}");

		return $"{heading}:\n{string.Join("\n", lines)}";
	}

	private static bool IsKnownBrewedPotion(string potionId)
	{
		return ItemCatalog.TryGetItem(potionId, out var item) &&
			item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
	}

	private static List<string> BuildTopTraitNames(IEnumerable<LearnedPotionEntry> learnedPotionIds, int maxCount)
	{
		var uniqueNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var entry in learnedPotionIds)
		{
			if (!ItemCatalog.TryGetItem(entry.PotionId, out var item))
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

	private static List<string> BuildRiskNames(IEnumerable<LearnedPotionEntry> learnedPotionIds)
	{
		var uniqueNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var entry in learnedPotionIds)
		{
			if (!ItemCatalog.TryGetItem(entry.PotionId, out var item))
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

		foreach (var trait in item.Traits
			.OrderByDescending(x => x.Value)
			.ThenBy(x => x.Key)
			.Take(3))
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

	private static string ItemName(string itemId)
	{
		return ItemCatalog.GetItemName(itemId);
	}

	private string DisplayName(string itemId, string fallbackName)
	{
		var customName = _gameState.GetPotionDisplayName(itemId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private static Texture2D? LoadIcon(string? iconPath)
	{
		if (string.IsNullOrWhiteSpace(iconPath))
			return null;

		return ResourceLoader.Load<Texture2D>(iconPath);
	}

	private readonly record struct LearnedPotionEntry(string PotionId, string Name);
}
