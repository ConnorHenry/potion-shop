using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

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
	[Export] public NodePath BrewButtonPath = default!;
	[Export] public NodePath PageIndicatorLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath DataDbPath = new("/root/DataDb");
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");

	private Button _leftArrowButton = default!;
	private Button _rightArrowButton = default!;
	private Label _pageTitleLabel = default!;
	private Label _introLabel = default!;
	private Control _recipeContent = default!;
	private Label _ingredientsLabel = default!;
	private Label _traitsLabel = default!;
	private Button _brewButton = default!;
	private Label _pageIndicatorLabel = default!;
	private Button _closeButton = default!;
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private PotionInventoryBrewService _brewService = default!;
	private readonly List<PotionRecipeDef> _recipes = new();
	private int _currentPageIndex;
	private bool _dragFromWholePanel;
	private bool _dragging;
	private Vector2 _dragOffset;

	public override void _Ready()
	{
		_dragFromWholePanel = true;
		SetProcessInput(true);

		// Convert from centered anchors to absolute positioning so the book can be dragged freely.
		var rect = GetGlobalRect();
		AnchorLeft = 0.0f;
		AnchorTop = 0.0f;
		AnchorRight = 0.0f;
		AnchorBottom = 0.0f;
		Position = rect.Position;
		Size = rect.Size;

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

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"PotionBookPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		_dataDb = dataDb;
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewService = new PotionInventoryBrewService(_gameState, _itemCatalog);

		_leftArrowButton = GetNode<Button>(LeftArrowButtonPath);
		_rightArrowButton = GetNode<Button>(RightArrowButtonPath);
		_pageTitleLabel = GetNode<Label>(PageTitleLabelPath);
		_introLabel = GetNode<Label>(IntroLabelPath);
		_recipeContent = GetNode<Control>(RecipeContentPath);
		_ingredientsLabel = GetNode<Label>(IngredientsLabelPath);
		_traitsLabel = GetNode<Label>(TraitsLabelPath);
		_brewButton = GetNode<Button>(BrewButtonPath);
		_pageIndicatorLabel = GetNode<Label>(PageIndicatorLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);

		_leftArrowButton.Pressed += OnPreviousPagePressed;
		_rightArrowButton.Pressed += OnNextPagePressed;
		_brewButton.Pressed += TryBrewCurrentPagePotion;
		_closeButton.Pressed += HidePanel;
		_gameState.Changed += OnGameStateChanged;

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
		if (_brewButton is not null)
			_brewButton.Pressed -= TryBrewCurrentPagePotion;
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		if (HandleWholePanelDragInput(@event))
			return;

		if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
			return;

		var hoveredControl = GetViewport().GuiGetHoveredControl();
		if (!IsHoverInsideBook(hoveredControl))
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

	private bool HandleWholePanelDragInput(InputEvent @event)
	{
		if (!_dragFromWholePanel)
			return false;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				if (!GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
					return false;
				if (IsPressOnInteractiveChildControl())
					return false;

				_dragging = true;
				_dragOffset = mouseButton.GlobalPosition - Position;
				AcceptEvent();
				return true;
			}

			if (!_dragging)
				return false;

			_dragging = false;
			AcceptEvent();
			return true;
		}

		if (_dragging && @event is InputEventMouseMotion mouseMotion)
		{
			Position = mouseMotion.GlobalPosition - _dragOffset;
			AcceptEvent();
			return true;
		}

		return false;
	}

	private bool IsHoverInsideBook(Control? hoveredControl)
	{
		if (hoveredControl is null)
			return false;

		if (hoveredControl == this)
			return true;

		return IsAncestorOf(hoveredControl);
	}

	private bool IsPressOnInteractiveChildControl()
	{
		var hoveredControl = GetViewport().GuiGetHoveredControl();
		if (hoveredControl is null)
			return false;
		if (hoveredControl == this)
			return false;
		if (!IsAncestorOf(hoveredControl))
			return false;

		return hoveredControl is BaseButton;
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (!Visible)
			return;

		var currentPotionId = GetCurrentPotionId();
		RebuildRecipePages();
		_currentPageIndex = ResolvePageIndex(currentPotionId);
		RefreshPage();
	}

	private void OnGameStateChanged()
	{
		var currentPotionId = GetCurrentPotionId();
		RebuildRecipePages();
		_currentPageIndex = ResolvePageIndex(currentPotionId);
		if (Visible)
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
		var authoredPotionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var recipe in _dataDb.PotionRecipes)
		{
			AddAuthoredRecipePage(recipe, authoredPotionIds);
		}

		foreach (var potionId in _gameState.KnownPotionOrder)
			AddLearnedPotionPage(potionId, authoredPotionIds);

		_recipes.Sort(CompareRecipesByName);
	}

	private void AddAuthoredRecipePage(PotionRecipeDef? recipe, HashSet<string> authoredPotionIds)
	{
		if (recipe is null || string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name))
			return;
		if (recipe.IngredientIds is null || recipe.IngredientIds.Count == 0)
			return;

		var ingredientIds = recipe.IngredientIds
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Select(id => id.Trim())
			.ToList();
		if (ingredientIds.Count == 0)
			return;

		var traits = recipe.Traits.Count == 0
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
		authoredPotionIds.Add(recipe.Id);
		authoredPotionIds.Add(BuildPredefinedPotionItemId(recipe.Id));
	}

	private void AddLearnedPotionPage(string potionId, HashSet<string> authoredPotionIds)
	{
		if (string.IsNullOrWhiteSpace(potionId))
			return;
		if (authoredPotionIds.Contains(potionId))
			return;
		if (!_gameState.TryGetPotionRecipe(potionId, out var ingredientIds) || ingredientIds.Count == 0)
			return;
		if (!_itemCatalog.TryGetItem(potionId, out var potion))
			return;
		if (potion.Treatment is not null)
			return;
		if (HasActiveRisk(potion))
			return;

		var sanitizedIngredientIds = ingredientIds
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Select(id => id.Trim())
			.ToList();
		if (sanitizedIngredientIds.Count == 0)
			return;

		var traits = potion.Traits.Count == 0
			? new List<string>()
			: potion.Traits
				.Where(trait => !string.IsNullOrWhiteSpace(trait.Key))
				.Select(trait => trait.Key.Trim())
				.ToList();

		_recipes.Add(new PotionRecipeDef
		{
			Id = potionId,
			Name = GetPotionDisplayName(potionId, potion.Name),
			IngredientIds = sanitizedIngredientIds,
			Traits = traits
		});
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
		UpdateBrewButtonState(recipe);
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

	private string GetPotionDisplayName(string potionId, string fallbackName)
	{
		var customName = _gameState.GetPotionDisplayName(potionId);
		return string.IsNullOrWhiteSpace(customName) ? fallbackName : customName;
	}

	private void UpdateBrewButtonState(PotionRecipeDef recipe)
	{
		_brewButton.Text = "Brew";
		_brewButton.Visible = true;

		if (!TryResolveKnownPotionItemId(recipe, out var potionItemId))
		{
			_brewButton.Disabled = true;
			_brewButton.TooltipText = "Brew this potion once before using the potion book shortcut.";
			return;
		}

		if (!_brewService.TryGetRequiredIngredients(potionItemId, out var requiredIngredients, out var error))
		{
			_brewButton.Disabled = true;
			_brewButton.TooltipText = error;
			return;
		}

		var hasIngredients = _brewService.HasRequiredIngredients(requiredIngredients);
		_brewButton.Disabled = !hasIngredients;
		_brewButton.TooltipText = hasIngredients
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
	}

	private void TryBrewCurrentPagePotion()
	{
		if (_currentPageIndex <= 0 || _currentPageIndex > _recipes.Count)
			return;

		var recipe = _recipes[_currentPageIndex - 1];
		if (!TryResolveKnownPotionItemId(recipe, out var potionItemId))
		{
			GD.PushError("PotionBookPanel: Potion has not been brewed before.");
			return;
		}

		if (_brewService.TryBrewPotion(potionItemId, out var error))
			return;

		CursorToast.Show(this, error);
		RefreshPage();
	}

	private bool TryResolveKnownPotionItemId(PotionRecipeDef recipe, out string potionItemId)
	{
		potionItemId = string.Empty;

		if (TryUseKnownPotionItemId(recipe.Id, out potionItemId))
			return true;

		return TryUseKnownPotionItemId(BuildPredefinedPotionItemId(recipe.Id), out potionItemId);
	}

	private bool TryUseKnownPotionItemId(string candidatePotionItemId, out string potionItemId)
	{
		potionItemId = string.Empty;
		if (string.IsNullOrWhiteSpace(candidatePotionItemId))
			return false;
		if (!_gameState.KnowsPotion(candidatePotionItemId))
			return false;
		if (!_itemCatalog.IsPotion(candidatePotionItemId))
			return false;
		if (!_itemCatalog.TryGetItem(candidatePotionItemId, out var item) || item.Treatment is not null)
			return false;
		if (!_gameState.TryGetPotionRecipe(candidatePotionItemId, out var ingredientIds) || ingredientIds.Count == 0)
			return false;

		potionItemId = candidatePotionItemId;
		return true;
	}

	private static string BuildPredefinedPotionItemId(string recipeId)
	{
		return $"potion_{recipeId}";
	}

	private static bool HasActiveRisk(ItemDef item)
	{
		if (item.Risks is null || item.Risks.Count == 0)
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
				return true;
		}

		return false;
	}

	private string? GetCurrentPotionId()
	{
		if (_currentPageIndex <= 0 || _currentPageIndex > _recipes.Count)
			return null;

		return _recipes[_currentPageIndex - 1].Id;
	}

	private int ResolvePageIndex(string? preferredPotionId)
	{
		if (string.IsNullOrWhiteSpace(preferredPotionId))
			return Math.Clamp(_currentPageIndex, 0, Math.Max(0, TotalPages - 1));

		for (var i = 0; i < _recipes.Count; i++)
		{
			if (!string.Equals(_recipes[i].Id, preferredPotionId, StringComparison.OrdinalIgnoreCase))
				continue;

			return i + 1;
		}

		return Math.Clamp(_currentPageIndex, 0, Math.Max(0, TotalPages - 1));
	}

	private static int CompareRecipesByName(PotionRecipeDef left, PotionRecipeDef right)
	{
		var nameComparison = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
		if (nameComparison != 0)
			return nameComparison;

		return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
	}
}
