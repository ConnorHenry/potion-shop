using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class RecipeBookPanel : Control
{
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath SortButtonPath = default!;
	[Export] public NodePath RecipesContainerPath = default!;

	private Button _closeButton = default!;
	private Button _sortButton = default!;
	private VBoxContainer _recipes = default!;
	private readonly PotionInventoryBrewService _brewService = new();
	private bool _ascending = true;
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
		_sortButton = GetNode<Button>(SortButtonPath);
		_recipes = GetNode<VBoxContainer>(RecipesContainerPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_sortButton.Pressed += ToggleSortOrder;
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
		if (_sortButton is not null)
			_sortButton.Pressed -= ToggleSortOrder;
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

	private void Refresh()
	{
		foreach (var child in _recipes.GetChildren())
			child.QueueFree();

		var learnedPotionIds = _gameState.KnownPotions
			.Where(IsKnownBrewedPotion)
			.Select(id => new
			{
				PotionId = id,
				Name = DisplayName(id, ItemName(id))
			})
			.ToList();

		learnedPotionIds = _ascending
			? learnedPotionIds.OrderBy(x => x.Name).ThenBy(x => x.PotionId).ToList()
			: learnedPotionIds.OrderByDescending(x => x.Name).ThenByDescending(x => x.PotionId).ToList();

		if (learnedPotionIds.Count == 0)
		{
			_recipes.AddChild(new Label { Text = "No brewed recipes yet." });
			return;
		}

		foreach (var entry in learnedPotionIds)
			_recipes.AddChild(CreateRecipeCard(entry.PotionId));
	}

	private void UpdateSortButtonLabel()
	{
		_sortButton.Text = _ascending ? "A-Z" : "Z-A";
	}

    private Control CreateRecipeCard(string potionId)
    {
        if (!ItemCatalog.TryGetItem(potionId, out var item))
            return new Label { Text = potionId };

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 0)
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

        var title = new Label
        {
            Text = DisplayName(potionId, item.Name)
        };

        var ingredients = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = _brewService.BuildIngredientAvailabilityText(potionId, includeHeading: true),
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.Arbitrary
        };

        var effects = new Label
        {
            Text = $"Effects: {FormatTopTraits(item.Traits, 3)}",
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
            ClipText = true
        };

        var risks = new Label
        {
            Text = $"Risks: {FormatDictionary(item.Risks)}",
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
            ClipText = true
        };

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

		textColumn.AddChild(title);
		textColumn.AddChild(ingredients);
		textColumn.AddChild(effects);
		textColumn.AddChild(risks);
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

	private static bool IsKnownBrewedPotion(string potionId)
	{
		return ItemCatalog.TryGetItem(potionId, out var item) &&
			item.Tags.Any(tag => string.Equals(tag, "potion", System.StringComparison.OrdinalIgnoreCase));
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
}
