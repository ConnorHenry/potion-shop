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
	private const int IntroPageCount = 1;
	private const int PagesPerSpread = 2;

	[Export] public NodePath LeftPageHotspotPath = default!;
	[Export] public NodePath RightPageHotspotPath = default!;
	[Export] public NodePath PageIndicatorLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath LeftPageTitleLabelPath = default!;
	[Export] public NodePath LeftIntroLabelPath = default!;
	[Export] public NodePath LeftRecipeContentPath = default!;
	[Export] public NodePath LeftIngredientsLabelPath = default!;
	[Export] public NodePath LeftTraitsLabelPath = default!;
	[Export] public NodePath LeftBrewButtonPath = default!;
	[Export] public NodePath LeftIngredientIconOnePath = new("BookRow/BookPanel/Margin/VBox/Pages/LeftPage/RecipeContent/IngredientIllustrations/IconOneFrame/Icon");
	[Export] public NodePath LeftIngredientIconTwoPath = new("BookRow/BookPanel/Margin/VBox/Pages/LeftPage/RecipeContent/IngredientIllustrations/IconTwoFrame/Icon");
	[Export] public NodePath LeftIngredientIconThreePath = new("BookRow/BookPanel/Margin/VBox/Pages/LeftPage/RecipeContent/IngredientIllustrations/IconThreeFrame/Icon");
	[Export] public NodePath LeftBrewStatusLabelPath = new("BookRow/BookPanel/Margin/VBox/Pages/LeftPage/RecipeContent/BrewStatus");
	[Export] public NodePath RightPageTitleLabelPath = default!;
	[Export] public NodePath RightIntroLabelPath = default!;
	[Export] public NodePath RightRecipeContentPath = default!;
	[Export] public NodePath RightIngredientsLabelPath = default!;
	[Export] public NodePath RightTraitsLabelPath = default!;
	[Export] public NodePath RightBrewButtonPath = default!;
	[Export] public NodePath RightIngredientIconOnePath = new("BookRow/BookPanel/Margin/VBox/Pages/RightPage/RecipeContent/IngredientIllustrations/IconOneFrame/Icon");
	[Export] public NodePath RightIngredientIconTwoPath = new("BookRow/BookPanel/Margin/VBox/Pages/RightPage/RecipeContent/IngredientIllustrations/IconTwoFrame/Icon");
	[Export] public NodePath RightIngredientIconThreePath = new("BookRow/BookPanel/Margin/VBox/Pages/RightPage/RecipeContent/IngredientIllustrations/IconThreeFrame/Icon");
	[Export] public NodePath RightBrewStatusLabelPath = new("BookRow/BookPanel/Margin/VBox/Pages/RightPage/RecipeContent/BrewStatus");
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private Button _leftPageHotspot = default!;
	private Button _rightPageHotspot = default!;
	private Label _pageIndicatorLabel = default!;
	private Button _closeButton = default!;
	private readonly PotionBookPageView _leftPage = new();
	private readonly PotionBookPageView _rightPage = new();
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

		_leftPageHotspot = GetNode<Button>(LeftPageHotspotPath);
		_rightPageHotspot = GetNode<Button>(RightPageHotspotPath);
		_pageIndicatorLabel = GetNode<Label>(PageIndicatorLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		ResolvePageView(_leftPage, PageSide.Left);
		ResolvePageView(_rightPage, PageSide.Right);

		_leftPageHotspot.Pressed += OnPreviousPagePressed;
		_rightPageHotspot.Pressed += OnNextPagePressed;
		_leftPage.BrewButton.Pressed += OnLeftBrewPressed;
		_rightPage.BrewButton.Pressed += OnRightBrewPressed;
		_closeButton.Pressed += HidePanel;
		_gameState.Changed += OnGameStateChanged;

		_currentPageIndex = 0;
		RebuildRecipePages();
		RefreshSpread();
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_leftPageHotspot is not null)
			_leftPageHotspot.Pressed -= OnPreviousPagePressed;
		if (_rightPageHotspot is not null)
			_rightPageHotspot.Pressed -= OnNextPagePressed;
		if (_leftPage.BrewButton is not null)
			_leftPage.BrewButton.Pressed -= OnLeftBrewPressed;
		if (_rightPage.BrewButton is not null)
			_rightPage.BrewButton.Pressed -= OnRightBrewPressed;
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

	private TNode? GetOptionalNode<TNode>(NodePath path) where TNode : Node
	{
		if (path is null || path.IsEmpty)
			return null;

		return GetNodeOrNull<TNode>(path);
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (!Visible)
			return;

		var currentPotionId = GetVisiblePotionId();
		RebuildRecipePages();
		_currentPageIndex = ResolveSpreadStart(currentPotionId);
		RefreshSpread();
	}

	private void OnGameStateChanged()
	{
		var currentPotionId = GetVisiblePotionId();
		RebuildRecipePages();
		_currentPageIndex = ResolveSpreadStart(currentPotionId);
		if (Visible)
			RefreshSpread();
	}

	private int TotalPages => _recipes.Count + IntroPageCount;

	private int MaxSpreadStart => Math.Max(0, ((TotalPages - 1) / PagesPerSpread) * PagesPerSpread);

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

		_currentPageIndex = Math.Max(0, _currentPageIndex - PagesPerSpread);
		RefreshSpread();
	}

	private void ShowNextPage()
	{
		if (_currentPageIndex >= MaxSpreadStart)
			return;

		_currentPageIndex = Math.Min(MaxSpreadStart, _currentPageIndex + PagesPerSpread);
		RefreshSpread();
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

	private void RefreshSpread()
	{
		if (TotalPages <= 0)
			return;

		_currentPageIndex = ClampToSpreadStart(_currentPageIndex);
		_pageIndicatorLabel.Text = BuildPageIndicatorText();

		_leftPageHotspot.Disabled = _currentPageIndex <= 0;
		_rightPageHotspot.Disabled = _currentPageIndex >= MaxSpreadStart;

		RefreshPageView(_leftPage, _currentPageIndex);
		RefreshPageView(_rightPage, _currentPageIndex + 1);
	}

	private string BuildPageIndicatorText()
	{
		var leftPageNumber = _currentPageIndex + 1;
		var rightPageNumber = Math.Min(_currentPageIndex + PagesPerSpread, TotalPages);
		if (leftPageNumber == rightPageNumber)
			return $"Page {leftPageNumber}/{TotalPages}";

		return $"Pages {leftPageNumber}-{rightPageNumber}/{TotalPages}";
	}

	private int ClampToSpreadStart(int pageIndex)
	{
		var clampedPageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, TotalPages - 1));
		return Math.Min(MaxSpreadStart, clampedPageIndex - (clampedPageIndex % PagesPerSpread));
	}

	private void RefreshPageView(PotionBookPageView page, int logicalPageIndex)
	{
		if (logicalPageIndex >= TotalPages)
		{
			ShowBlankPage(page);
			return;
		}

		if (!TryGetRecipeForPage(logicalPageIndex, out var recipe))
		{
			ShowBlankPage(page);
			return;
		}

		ShowRecipePage(page, recipe);
	}

	private void ShowBlankPage(PotionBookPageView page)
	{
		page.PageTitleLabel.Text = string.Empty;
		page.IntroLabel.Text = string.Empty;
		page.IntroLabel.Visible = false;
		page.RecipeContent.Visible = false;
		ClearIngredientIcons(page);
		SetBrewStatus(page, string.Empty);
	}

	private void ShowRecipePage(PotionBookPageView page, PotionRecipeDef recipe)
	{
		page.PageTitleLabel.Text = recipe.Name;
		page.IntroLabel.Visible = false;
		page.RecipeContent.Visible = true;
		page.IngredientsLabel.Text = BuildIngredientsText(recipe);
		page.TraitsLabel.Text = BuildTraitsText(recipe);
		UpdateIngredientIcons(page, recipe);
		UpdateBrewButtonState(page, recipe);
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

	private void UpdateBrewButtonState(PotionBookPageView page, PotionRecipeDef recipe)
	{
		page.BrewButton.Text = "Brew";
		page.BrewButton.Visible = true;

		if (!TryResolveKnownPotionItemId(recipe, out var potionItemId))
		{
			page.BrewButton.Disabled = true;
			page.BrewButton.TooltipText = "Brew this potion once before using the potion book shortcut.";
			SetBrewStatus(page, "Shortcut unlocks after the potion is brewed once.");
			return;
		}

		if (!_brewService.TryGetRequiredIngredients(potionItemId, out var requiredIngredients, out var error))
		{
			page.BrewButton.Disabled = true;
			page.BrewButton.TooltipText = error;
			SetBrewStatus(page, error);
			return;
		}

		var hasIngredients = _brewService.HasRequiredIngredients(requiredIngredients);
		page.BrewButton.Disabled = !hasIngredients;
		page.BrewButton.TooltipText = hasIngredients
			? "Brew this potion from discovered ingredients."
			: _brewService.BuildMissingIngredientsText(requiredIngredients);
		SetBrewStatus(page, hasIngredients ? "Ready to brew from recorded ingredients." : "Missing ingredients in inventory.");
	}

	private void UpdateIngredientIcons(PotionBookPageView page, PotionRecipeDef recipe)
	{
		UpdateIngredientIcon(page.IngredientIconOne, recipe, 0);
		UpdateIngredientIcon(page.IngredientIconTwo, recipe, 1);
		UpdateIngredientIcon(page.IngredientIconThree, recipe, 2);
	}

	private void UpdateIngredientIcon(TextureRect? icon, PotionRecipeDef recipe, int ingredientIndex)
	{
		if (ingredientIndex >= recipe.IngredientIds.Count)
		{
			SetIngredientIcon(icon, null, string.Empty);
			return;
		}

		var ingredientId = recipe.IngredientIds[ingredientIndex];
		if (!_itemCatalog.TryGetItem(ingredientId, out var item))
		{
			SetIngredientIcon(icon, null, string.Empty);
			return;
		}

		SetIngredientIcon(icon, UiIconLoader.LoadIcon(item.IconPath), item.Name);
	}

	private static void ClearIngredientIcons(PotionBookPageView page)
	{
		SetIngredientIcon(page.IngredientIconOne, null, string.Empty);
		SetIngredientIcon(page.IngredientIconTwo, null, string.Empty);
		SetIngredientIcon(page.IngredientIconThree, null, string.Empty);
	}

	private static void SetIngredientIcon(TextureRect? icon, Texture2D? texture, string tooltipText)
	{
		if (icon is null)
			return;

		icon.Texture = texture;
		icon.Visible = texture is not null;
		icon.TooltipText = tooltipText;
	}

	private static void SetBrewStatus(PotionBookPageView page, string text)
	{
		if (page.BrewStatusLabel is null)
			return;

		page.BrewStatusLabel.Text = text;
	}

	private void OnLeftBrewPressed()
	{
		TryBrewVisiblePotion(_currentPageIndex);
	}

	private void OnRightBrewPressed()
	{
		TryBrewVisiblePotion(_currentPageIndex + 1);
	}

	private void TryBrewVisiblePotion(int logicalPageIndex)
	{
		if (!TryGetRecipeForPage(logicalPageIndex, out var recipe))
			return;

		if (!TryResolveKnownPotionItemId(recipe, out var potionItemId))
		{
			GD.PushError("PotionBookPanel: Potion has not been brewed before.");
			return;
		}

		if (_brewService.TryBrewPotion(potionItemId, out var error))
			return;

		CursorToast.Show(this, error);
		RefreshSpread();
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

	private string? GetVisiblePotionId()
	{
		if (TryGetRecipeForPage(_currentPageIndex, out var leftRecipe))
			return leftRecipe.Id;

		if (TryGetRecipeForPage(_currentPageIndex + 1, out var rightRecipe))
			return rightRecipe.Id;

		return null;
	}

	private bool TryGetRecipeForPage(int logicalPageIndex, out PotionRecipeDef recipe)
	{
		recipe = default!;

		if (logicalPageIndex < IntroPageCount)
			return false;

		var recipeIndex = logicalPageIndex - IntroPageCount;
		if (recipeIndex < 0 || recipeIndex >= _recipes.Count)
			return false;

		recipe = _recipes[recipeIndex];
		return true;
	}

	private int ResolveSpreadStart(string? preferredPotionId)
	{
		if (string.IsNullOrWhiteSpace(preferredPotionId))
			return ClampToSpreadStart(_currentPageIndex);

		for (var i = 0; i < _recipes.Count; i++)
		{
			if (!string.Equals(_recipes[i].Id, preferredPotionId, StringComparison.OrdinalIgnoreCase))
				continue;

			return ClampToSpreadStart(i + IntroPageCount);
		}

		return ClampToSpreadStart(_currentPageIndex);
	}

	private void ResolvePageView(PotionBookPageView page, PageSide side)
	{
		if (side == PageSide.Left)
		{
			page.PageTitleLabel = GetNode<Label>(LeftPageTitleLabelPath);
			page.IntroLabel = GetNode<Label>(LeftIntroLabelPath);
			page.RecipeContent = GetNode<Control>(LeftRecipeContentPath);
			page.IngredientsLabel = GetNode<Label>(LeftIngredientsLabelPath);
			page.TraitsLabel = GetNode<Label>(LeftTraitsLabelPath);
			page.BrewButton = GetNode<Button>(LeftBrewButtonPath);
			page.IngredientIconOne = GetOptionalNode<TextureRect>(LeftIngredientIconOnePath);
			page.IngredientIconTwo = GetOptionalNode<TextureRect>(LeftIngredientIconTwoPath);
			page.IngredientIconThree = GetOptionalNode<TextureRect>(LeftIngredientIconThreePath);
			page.BrewStatusLabel = GetOptionalNode<Label>(LeftBrewStatusLabelPath);
			return;
		}

		page.PageTitleLabel = GetNode<Label>(RightPageTitleLabelPath);
		page.IntroLabel = GetNode<Label>(RightIntroLabelPath);
		page.RecipeContent = GetNode<Control>(RightRecipeContentPath);
		page.IngredientsLabel = GetNode<Label>(RightIngredientsLabelPath);
		page.TraitsLabel = GetNode<Label>(RightTraitsLabelPath);
		page.BrewButton = GetNode<Button>(RightBrewButtonPath);
		page.IngredientIconOne = GetOptionalNode<TextureRect>(RightIngredientIconOnePath);
		page.IngredientIconTwo = GetOptionalNode<TextureRect>(RightIngredientIconTwoPath);
		page.IngredientIconThree = GetOptionalNode<TextureRect>(RightIngredientIconThreePath);
		page.BrewStatusLabel = GetOptionalNode<Label>(RightBrewStatusLabelPath);
	}

	private static int CompareRecipesByName(PotionRecipeDef left, PotionRecipeDef right)
	{
		var nameComparison = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
		if (nameComparison != 0)
			return nameComparison;

		return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
	}

	private enum PageSide
	{
		Left,
		Right
	}

	private sealed class PotionBookPageView
	{
		public Label PageTitleLabel = default!;
		public Label IntroLabel = default!;
		public Control RecipeContent = default!;
		public Label IngredientsLabel = default!;
		public Label TraitsLabel = default!;
		public Button BrewButton = default!;
		public TextureRect? IngredientIconOne;
		public TextureRect? IngredientIconTwo;
		public TextureRect? IngredientIconThree;
		public Label? BrewStatusLabel;
	}
}
