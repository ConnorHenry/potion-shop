using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class PotionBookPanel : Control
{
	[Export] public NodePath LeftArrowButtonPath = default!;
	[Export] public NodePath RightArrowButtonPath = default!;
	[Export] public NodePath PageTitleLabelPath = default!;
	[Export] public NodePath IntroLabelPath = default!;
	[Export] public NodePath RecipeContentPath = default!;
	[Export] public NodePath IngredientsLabelPath = default!;
	[Export] public NodePath TraitsLabelPath = default!;
	[Export] public NodePath PageIndicatorLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath DataDbPath = new("/root/DataDb");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private Button _leftArrowButton = default!;
	private Button _rightArrowButton = default!;
	private Label _pageTitleLabel = default!;
	private Label _introLabel = default!;
	private Control _recipeContent = default!;
	private Label _ingredientsLabel = default!;
	private Label _traitsLabel = default!;
	private Label _pageIndicatorLabel = default!;
	private Button _closeButton = default!;
	private DataDb _dataDb = default!;
	private ItemCatalogService _itemCatalog = default!;
	private readonly List<PotionRecipeDef> _recipes = new();
	private int _currentPageIndex;

	public override void _Ready()
	{
		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"PotionBookPanel: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"PotionBookPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_dataDb = dataDb;
		_itemCatalog = itemCatalog;

		_leftArrowButton = GetNode<Button>(LeftArrowButtonPath);
		_rightArrowButton = GetNode<Button>(RightArrowButtonPath);
		_pageTitleLabel = GetNode<Label>(PageTitleLabelPath);
		_introLabel = GetNode<Label>(IntroLabelPath);
		_recipeContent = GetNode<Control>(RecipeContentPath);
		_ingredientsLabel = GetNode<Label>(IngredientsLabelPath);
		_traitsLabel = GetNode<Label>(TraitsLabelPath);
		_pageIndicatorLabel = GetNode<Label>(PageIndicatorLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);

		_leftArrowButton.Pressed += OnPreviousPagePressed;
		_rightArrowButton.Pressed += OnNextPagePressed;
		_closeButton.Pressed += HidePanel;

		_currentPageIndex = 0;
		RebuildRecipePages();
		RefreshPage();
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_leftArrowButton is not null)
			_leftArrowButton.Pressed -= OnPreviousPagePressed;
		if (_rightArrowButton is not null)
			_rightArrowButton.Pressed -= OnNextPagePressed;
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
			return;

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			ShowPreviousPage();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			ShowNextPage();
			GetViewport().SetInputAsHandled();
		}
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (!Visible)
			return;

		RebuildRecipePages();
		_currentPageIndex = Math.Clamp(_currentPageIndex, 0, Math.Max(0, TotalPages - 1));
		RefreshPage();
	}

	private int TotalPages => _recipes.Count + 1;

	private void HidePanel()
	{
		Visible = false;
	}

	private void OnPreviousPagePressed()
	{
		ShowPreviousPage();
	}

	private void OnNextPagePressed()
	{
		ShowNextPage();
	}

	private void ShowPreviousPage()
	{
		if (_currentPageIndex <= 0)
			return;

		_currentPageIndex -= 1;
		RefreshPage();
	}

	private void ShowNextPage()
	{
		if (_currentPageIndex >= TotalPages - 1)
			return;

		_currentPageIndex += 1;
		RefreshPage();
	}

	private void RebuildRecipePages()
	{
		_recipes.Clear();

		foreach (var recipe in _dataDb.PotionRecipes)
		{
			if (recipe is null || string.IsNullOrWhiteSpace(recipe.Name))
				continue;
			if (recipe.IngredientIds is null || recipe.IngredientIds.Count == 0)
				continue;

			var ingredientIds = recipe.IngredientIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Select(id => id.Trim())
				.ToList();
			if (ingredientIds.Count == 0)
				continue;

			var traits = recipe.Traits is null
				? new List<string>()
				: recipe.Traits
					.Where(trait => !string.IsNullOrWhiteSpace(trait))
					.Select(trait => trait.Trim())
					.ToList();

			_recipes.Add(new PotionRecipeDef
			{
				Id = recipe.Id,
				Name = recipe.Name,
				IngredientIds = ingredientIds,
				Traits = traits
			});
		}
	}

	private void RefreshPage()
	{
		if (TotalPages <= 0)
			return;

		_currentPageIndex = Math.Clamp(_currentPageIndex, 0, TotalPages - 1);
		_pageIndicatorLabel.Text = $"Page {_currentPageIndex + 1}/{TotalPages}";

		_leftArrowButton.Disabled = _currentPageIndex <= 0;
		_rightArrowButton.Disabled = _currentPageIndex >= TotalPages - 1;

		if (_currentPageIndex == 0)
		{
			_pageTitleLabel.Text = "Potion Book";
			_introLabel.Text = "Turn the page to browse the potions you can brew.";
			_introLabel.Visible = true;
			_recipeContent.Visible = false;
			return;
		}

		var recipe = _recipes[_currentPageIndex - 1];
		_pageTitleLabel.Text = recipe.Name;
		_introLabel.Visible = false;
		_recipeContent.Visible = true;
		_ingredientsLabel.Text = BuildIngredientsText(recipe);
		_traitsLabel.Text = BuildTraitsText(recipe);
	}

	private string BuildIngredientsText(PotionRecipeDef recipe)
	{
		var lines = new List<string>(recipe.IngredientIds.Count);
		foreach (var ingredientId in recipe.IngredientIds)
			lines.Add($"- {_itemCatalog.GetItemName(ingredientId)}");

		return lines.Count == 0
			? "None"
			: string.Join("\n", lines);
	}

	private static string BuildTraitsText(PotionRecipeDef recipe)
	{
		var lines = new List<string>(recipe.Traits.Count);
		foreach (var trait in recipe.Traits)
			lines.Add($"- {ToDisplayText(trait)}");

		return lines.Count == 0
			? "None"
			: string.Join("\n", lines);
	}

	private static string ToDisplayText(string rawValue)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
			return "Unknown";

		var normalized = rawValue.Trim().Replace('_', ' ').ToLowerInvariant();
		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
	}
}
