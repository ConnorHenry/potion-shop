using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class IngredientBookPanel : Control
{
	private const int ContentsEntriesPerPage = 10;
	private const int PagesPerSpread = 2;
	private const string UnknownContentsLabel = "???????";

	[Export] public NodePath LeftPageHotspotPath = default!;
	[Export] public NodePath RightPageHotspotPath = default!;
	[Export] public NodePath PageIndicatorLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;
	[Export] public NodePath LeftPageTitleLabelPath = default!;
	[Export] public NodePath LeftPageNumberLabelPath = default!;
	[Export] public NodePath LeftContentsPath = default!;
	[Export] public NodePath LeftIngredientContentPath = default!;
	[Export] public NodePath LeftIconPath = default!;
	[Export] public NodePath LeftUnknownIconPath = default!;
	[Export] public NodePath LeftTraitsLabelPath = default!;
	[Export] public NodePath LeftRisksLabelPath = default!;
	[Export] public NodePath LeftDescriptionLabelPath = default!;
	[Export] public NodePath RightPageTitleLabelPath = default!;
	[Export] public NodePath RightPageNumberLabelPath = default!;
	[Export] public NodePath RightContentsPath = default!;
	[Export] public NodePath RightIngredientContentPath = default!;
	[Export] public NodePath RightIconPath = default!;
	[Export] public NodePath RightUnknownIconPath = default!;
	[Export] public NodePath RightTraitsLabelPath = default!;
	[Export] public NodePath RightRisksLabelPath = default!;
	[Export] public NodePath RightDescriptionLabelPath = default!;
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private Button _leftPageHotspot = default!;
	private Button _rightPageHotspot = default!;
	private Label _pageIndicatorLabel = default!;
	private Button _closeButton = default!;
	private readonly IngredientBookPageView _leftPage = new();
	private readonly IngredientBookPageView _rightPage = new();
	private DataDb _dataDb = default!;
	private GameState _gameState = default!;
	private readonly List<IngredientBookEntry> _pages = new();
	private readonly List<IngredientBookContentsEntry> _contentsEntries = new();
	private int _contentsPageCount = 1;
	private int _currentPageIndex;

	public override void _Ready()
	{
		SetProcessInput(true);

		var dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (dataDb is null)
		{
			GD.PushError($"IngredientBookPanel: DataDb was not found at '{DataDbPath}'.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"IngredientBookPanel: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"IngredientBookPanel: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		_dataDb = dataDb;
		_gameState = gameState;

		_leftPageHotspot = GetNode<Button>(LeftPageHotspotPath);
		_rightPageHotspot = GetNode<Button>(RightPageHotspotPath);
		_pageIndicatorLabel = GetNode<Label>(PageIndicatorLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		ResolvePageView(_leftPage, PageSide.Left);
		ResolvePageView(_rightPage, PageSide.Right);

		_leftPageHotspot.Pressed += OnPreviousPagePressed;
		_rightPageHotspot.Pressed += OnNextPagePressed;
		_closeButton.Pressed += HidePanel;
		_gameState.Changed += OnGameStateChanged;

		_currentPageIndex = 0;
		RebuildPages();
		RefreshSpread();
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_leftPageHotspot is not null)
			_leftPageHotspot.Pressed -= OnPreviousPagePressed;
		if (_rightPageHotspot is not null)
			_rightPageHotspot.Pressed -= OnNextPagePressed;
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_gameState is not null)
			_gameState.Changed -= OnGameStateChanged;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
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

	public void Toggle()
	{
		if (Visible)
		{
			Visible = false;
			return;
		}

		ShowPanel();
	}

	public void ShowPanel()
	{
		var currentIngredientId = GetVisibleIngredientId();
		RebuildPages();
		_currentPageIndex = ResolveSpreadStart(currentIngredientId);
		RefreshSpread();
		Visible = true;
	}

	public void HidePanel()
	{
		Visible = false;
	}

	private void OnGameStateChanged()
	{
		var currentIngredientId = GetVisibleIngredientId();
		RebuildPages();
		_currentPageIndex = ResolveSpreadStart(currentIngredientId);
		if (Visible)
			RefreshSpread();
	}

	private int TotalPages => Math.Max(1, _contentsPageCount + _pages.Count);

	private int MaxSpreadStart => Math.Max(0, ((TotalPages - 1) / PagesPerSpread) * PagesPerSpread);

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

	private void RebuildPages()
	{
		_pages.Clear();

		var knownIngredients = new List<ItemDef>();
		var unknownIngredients = new List<ItemDef>();
		foreach (var item in _dataDb.Items.Values)
		{
			if (!IsBaseAuthoredIngredient(item))
				continue;

			if (_gameState.KnowsIngredient(item.Id))
				knownIngredients.Add(item);
			else
				unknownIngredients.Add(item);
		}

		knownIngredients.Sort(CompareItemsByName);
		unknownIngredients.Sort(CompareItemsByName);

		foreach (var item in knownIngredients)
			_pages.Add(new IngredientBookEntry(item, true));
		foreach (var item in unknownIngredients)
			_pages.Add(new IngredientBookEntry(item, false));

		RebuildContentsEntries();
	}

	private void RebuildContentsEntries()
	{
		_contentsEntries.Clear();
		_contentsPageCount = Math.Max(1, (_pages.Count + ContentsEntriesPerPage - 1) / ContentsEntriesPerPage);

		for (var i = 0; i < _pages.Count; i++)
		{
			var entry = _pages[i];
			_contentsEntries.Add(new IngredientBookContentsEntry(
				entry.Item.Id,
				entry.IsKnown ? entry.Item.Name : UnknownContentsLabel,
				_contentsPageCount + i));
		}
	}

	private void RefreshSpread()
	{
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

	private void RefreshPageView(IngredientBookPageView page, int logicalPageIndex)
	{
		if (logicalPageIndex >= TotalPages)
		{
			ShowBlankPage(page);
			return;
		}

		SetPageNumber(page, logicalPageIndex);
		if (logicalPageIndex < _contentsPageCount)
		{
			ShowContentsPage(page, logicalPageIndex);
			return;
		}

		if (!TryGetIngredientEntryForPage(logicalPageIndex, out var entry))
		{
			ShowBlankPage(page);
			SetPageNumber(page, logicalPageIndex);
			return;
		}

		if (entry.IsKnown)
			ShowKnownIngredientPage(page, entry.Item);
		else
			ShowUnknownIngredientPage(page);
	}

	private void ShowBlankPage(IngredientBookPageView page)
	{
		page.PageTitleLabel.Text = string.Empty;
		if (page.PageNumberLabel is not null)
			page.PageNumberLabel.Text = string.Empty;
		ClearContentsRows(page);
		page.Contents.Visible = false;
		page.IngredientContent.Visible = false;
		page.Icon.Texture = null;
		page.Icon.Visible = false;
		page.UnknownIcon.Visible = false;
		page.TraitsLabel.Text = string.Empty;
		page.RisksLabel.Text = string.Empty;
		page.DescriptionLabel.Text = string.Empty;
	}

	private void ShowContentsPage(IngredientBookPageView page, int logicalPageIndex)
	{
		page.PageTitleLabel.Text = "Contents";
		ClearContentsRows(page);
		page.Contents.Visible = true;
		page.IngredientContent.Visible = false;
		page.Icon.Texture = null;
		page.Icon.Visible = false;
		page.UnknownIcon.Visible = false;
		page.TraitsLabel.Text = string.Empty;
		page.RisksLabel.Text = string.Empty;
		page.DescriptionLabel.Text = string.Empty;

		var startIndex = logicalPageIndex * ContentsEntriesPerPage;
		var endIndex = Math.Min(_contentsEntries.Count, startIndex + ContentsEntriesPerPage);
		for (var i = startIndex; i < endIndex; i++)
			page.Contents.AddChild(CreateContentsButton(_contentsEntries[i]));
	}

	private void SetPageNumber(IngredientBookPageView page, int logicalPageIndex)
	{
		if (page.PageNumberLabel is null)
			return;

		page.PageNumberLabel.Text = $"{logicalPageIndex + 1} / {TotalPages}";
	}

	private void ShowKnownIngredientPage(IngredientBookPageView page, ItemDef item)
	{
		ClearContentsRows(page);
		page.Contents.Visible = false;
		page.PageTitleLabel.Text = item.Name;
		page.IngredientContent.Visible = true;
		page.Icon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		page.Icon.Visible = page.Icon.Texture is not null;
		page.Icon.TooltipText = item.Name;
		page.UnknownIcon.Visible = false;
		page.TraitsLabel.Text = BuildStatsText(item.Traits);
		page.RisksLabel.Text = BuildStatsText(item.Risks);
		page.DescriptionLabel.Text = string.IsNullOrWhiteSpace(item.Description)
			? "No description recorded."
			: item.Description;
	}

	private static void ShowUnknownIngredientPage(IngredientBookPageView page)
	{
		ClearContentsRows(page);
		page.Contents.Visible = false;
		page.PageTitleLabel.Text = "Unknown Ingredient";
		page.IngredientContent.Visible = true;
		page.Icon.Texture = null;
		page.Icon.Visible = false;
		page.Icon.TooltipText = string.Empty;
		page.UnknownIcon.Visible = true;
		page.TraitsLabel.Text = "Unknown";
		page.RisksLabel.Text = "Unknown";
		page.DescriptionLabel.Text = "This ingredient has not been discovered yet.";
	}

	private Button CreateContentsButton(IngredientBookContentsEntry entry)
	{
		var targetPageIndex = entry.TargetPageIndex;
		var button = new Button
		{
			Text = $"{entry.DisplayName}    {targetPageIndex + 1}",
			TooltipText = "Open this ingredient page",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += () => OpenPage(targetPageIndex);
		return button;
	}

	private void OpenPage(int logicalPageIndex)
	{
		_currentPageIndex = ClampToSpreadStart(logicalPageIndex);
		RefreshSpread();
	}

	private static void ClearContentsRows(IngredientBookPageView page)
	{
		foreach (var child in page.Contents.GetChildren())
			child.QueueFree();
	}

	private string? GetVisibleIngredientId()
	{
		if (TryGetIngredientEntryForPage(_currentPageIndex, out var leftEntry))
			return leftEntry.Item.Id;

		if (TryGetIngredientEntryForPage(_currentPageIndex + 1, out var rightEntry))
			return rightEntry.Item.Id;

		return null;
	}

	private bool TryGetIngredientEntryForPage(int logicalPageIndex, out IngredientBookEntry entry)
	{
		entry = default;
		var entryIndex = logicalPageIndex - _contentsPageCount;
		if (entryIndex < 0 || entryIndex >= _pages.Count)
			return false;

		entry = _pages[entryIndex];
		return true;
	}

	private int ResolveSpreadStart(string? preferredIngredientId)
	{
		if (string.IsNullOrWhiteSpace(preferredIngredientId))
			return ClampToSpreadStart(_currentPageIndex);

		for (var i = 0; i < _pages.Count; i++)
		{
			var entry = _pages[i];
			if (!string.Equals(entry.Item.Id, preferredIngredientId, StringComparison.OrdinalIgnoreCase))
				continue;

			return ClampToSpreadStart(i + _contentsPageCount);
		}

		return ClampToSpreadStart(_currentPageIndex);
	}

	private bool IsHoverInsideBook(Control? hoveredControl)
	{
		if (hoveredControl is null)
			return false;
		if (hoveredControl == this)
			return true;

		return IsAncestorOf(hoveredControl);
	}

	private TNode? GetOptionalNode<TNode>(NodePath path) where TNode : Node
	{
		if (path is null || path.IsEmpty)
			return null;

		return GetNodeOrNull<TNode>(path);
	}

	private void ResolvePageView(IngredientBookPageView page, PageSide side)
	{
		if (side == PageSide.Left)
		{
			page.PageTitleLabel = GetNode<Label>(LeftPageTitleLabelPath);
			page.PageNumberLabel = GetOptionalNode<Label>(LeftPageNumberLabelPath);
			page.Contents = GetNode<VBoxContainer>(LeftContentsPath);
			page.IngredientContent = GetNode<Control>(LeftIngredientContentPath);
			page.Icon = GetNode<TextureRect>(LeftIconPath);
			page.UnknownIcon = GetNode<Control>(LeftUnknownIconPath);
			page.TraitsLabel = GetNode<Label>(LeftTraitsLabelPath);
			page.RisksLabel = GetNode<Label>(LeftRisksLabelPath);
			page.DescriptionLabel = GetNode<Label>(LeftDescriptionLabelPath);
			return;
		}

		page.PageTitleLabel = GetNode<Label>(RightPageTitleLabelPath);
		page.PageNumberLabel = GetOptionalNode<Label>(RightPageNumberLabelPath);
		page.Contents = GetNode<VBoxContainer>(RightContentsPath);
		page.IngredientContent = GetNode<Control>(RightIngredientContentPath);
		page.Icon = GetNode<TextureRect>(RightIconPath);
		page.UnknownIcon = GetNode<Control>(RightUnknownIconPath);
		page.TraitsLabel = GetNode<Label>(RightTraitsLabelPath);
		page.RisksLabel = GetNode<Label>(RightRisksLabelPath);
		page.DescriptionLabel = GetNode<Label>(RightDescriptionLabelPath);
	}

	private static bool IsBaseAuthoredIngredient(ItemDef item)
	{
		if (item is null || item.Treatment is not null)
			return false;

		return ItemCatalogService.HasTag(item, ItemTags.Ingredient);
	}

	private static string BuildStatsText(Dictionary<string, int>? values)
	{
		if (values is null || values.Count == 0)
			return "None";

		var lines = new List<string>();
		foreach (var entry in values.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
		{
			if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
				continue;

			lines.Add($"{ToDisplayText(entry.Key)} +{entry.Value}");
		}

		return lines.Count == 0 ? "None" : string.Join("\n", lines);
	}

	private static string ToDisplayText(string rawValue)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
			return "Unknown";

		var normalized = rawValue.Trim().Replace('_', ' ').ToLowerInvariant();
		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
	}

	private static int CompareItemsByName(ItemDef left, ItemDef right)
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

	private sealed class IngredientBookPageView
	{
		public Label PageTitleLabel = default!;
		public Label? PageNumberLabel;
		public VBoxContainer Contents = default!;
		public Control IngredientContent = default!;
		public TextureRect Icon = default!;
		public Control UnknownIcon = default!;
		public Label TraitsLabel = default!;
		public Label RisksLabel = default!;
		public Label DescriptionLabel = default!;
	}

	private readonly record struct IngredientBookEntry(ItemDef Item, bool IsKnown);

	private readonly record struct IngredientBookContentsEntry(string IngredientId, string DisplayName, int TargetPageIndex);
}
