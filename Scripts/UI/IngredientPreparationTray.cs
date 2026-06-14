using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class IngredientPreparationTray : Control
{
	private const string DefaultIngredientName = "Drop raw ingredient";
	private const string DefaultStatusText = "Select a preparation.";
	private const string MissingPreviewText = "Unavailable";

	[Export] public NodePath IngredientDropBoxPath = default!;
	[Export] public NodePath IngredientIconPath = default!;
	[Export] public NodePath IngredientNamePath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath PreparationButtonsContainerPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath BrewPanelPath = new("../BrewPanel");
	[Export] public NodePath RuntimeContentDbPath = new(AutoloadNodePaths.RuntimeContentDb);
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);

	private BrewDropBox _ingredientDropBox = default!;
	private TextureRect _ingredientIcon = default!;
	private Label _ingredientName = default!;
	private Label _statusLabel = default!;
	private Container _preparationButtonsContainer = default!;
	private Button _clearButton = default!;
	private BrewPanel _brewPanel = default!;
	private RuntimeContentDb _runtimeContentDb = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private string _selectedIngredientId = string.Empty;
	private Control.GuiInputEventHandler? _dropBoxGuiInputHandler;
	private readonly List<Button> _preparationButtons = new();
	private readonly Dictionary<string, RichTextLabel> _preparationPreviewLabels = new(StringComparer.OrdinalIgnoreCase);

	public override void _Ready()
	{
		var runtimeContentDb = GetNodeOrNull<RuntimeContentDb>(RuntimeContentDbPath);
		if (runtimeContentDb is null)
		{
			GD.PushError($"IngredientPreparationTray: RuntimeContentDb was not found at '{RuntimeContentDbPath}'.");
			return;
		}

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"IngredientPreparationTray: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"IngredientPreparationTray: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var brewPanel = GetNodeOrNull<BrewPanel>(BrewPanelPath);
		if (brewPanel is null)
		{
			GD.PushError($"IngredientPreparationTray: BrewPanel was not found at '{BrewPanelPath}'.");
			return;
		}

		_runtimeContentDb = runtimeContentDb;
		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_brewPanel = brewPanel;
		_ingredientDropBox = GetNode<BrewDropBox>(IngredientDropBoxPath);
		_ingredientIcon = GetNode<TextureRect>(IngredientIconPath);
		_ingredientName = GetNode<Label>(IngredientNamePath);
		_statusLabel = GetNode<Label>(StatusLabelPath);
		_preparationButtonsContainer = GetNode<Container>(PreparationButtonsContainerPath);
		_clearButton = GetNode<Button>(ClearButtonPath);

		_ingredientDropBox.ItemDropped += OnIngredientDropped;
		_dropBoxGuiInputHandler = OnIngredientDropBoxGuiInput;
		_ingredientDropBox.GuiInput += _dropBoxGuiInputHandler;
		_clearButton.Pressed += ClearSelection;
		BuildPreparationButtons();

		MouseFilter = MouseFilterEnum.Stop;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_ingredientDropBox is not null)
			_ingredientDropBox.ItemDropped -= OnIngredientDropped;
		if (_ingredientDropBox is not null && _dropBoxGuiInputHandler is not null)
			_ingredientDropBox.GuiInput -= _dropBoxGuiInputHandler;
		if (_clearButton is not null)
			_clearButton.Pressed -= ClearSelection;
	}

	private void OnIngredientDropped(string itemId)
	{
		TrySelectIngredientFromInventory(itemId);
	}

	public bool TrySelectIngredientFromInventory(string itemId)
	{
		if (!_itemCatalog.TryGetItem(itemId, out var item))
		{
			SetStatus("That item is not recognized.");
			return false;
		}

		if (!_itemCatalog.IsIngredient(itemId))
		{
			SetStatus("Only ingredients can be prepared.");
			return false;
		}

		if (_itemCatalog.IsPreparedIngredient(itemId))
		{
			SetStatus("That ingredient is already prepared.");
			return false;
		}

		if (item.Preparations is null || item.Preparations.Count == 0)
		{
			SetStatus("This ingredient has no preparation data.");
			return false;
		}

		if (!ReserveSelectedIngredient(itemId))
			return false;

		_selectedIngredientId = itemId;
		_ingredientIcon.Texture = UiIconLoader.LoadIcon(item.IconPath);
		_ingredientName.Text = item.Name;
		SetStatus(DefaultStatusText);
		Refresh();
		return true;
	}

	private void OnIngredientDropBoxGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton rightMouseButton ||
			rightMouseButton.ButtonIndex != MouseButton.Right ||
			!rightMouseButton.Pressed)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(_selectedIngredientId))
			return;

		ClearSelection();
		AcceptEvent();
	}

	private bool ReserveSelectedIngredient(string itemId)
	{
		if (string.Equals(_selectedIngredientId, itemId, System.StringComparison.OrdinalIgnoreCase))
			return true;

		if (!_gameState.HasItem(itemId, 1))
		{
			SetStatus("Not enough stock for that ingredient.");
			return false;
		}

		if (!_gameState.ConsumeItem(itemId, 1))
		{
			SetStatus("Could not take that ingredient.");
			return false;
		}

		ReturnSelectedIngredient();
		return true;
	}

	private void BuildPreparationButtons()
	{
		foreach (var child in _preparationButtonsContainer.GetChildren())
		{
			_preparationButtonsContainer.RemoveChild(child);
			child.QueueFree();
		}

		_preparationButtons.Clear();
		_preparationPreviewLabels.Clear();

		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			var preparationId = option.Id;
			var column = new VBoxContainer
			{
				CustomMinimumSize = new Vector2(96, 74),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};

			var button = new Button
			{
				Text = option.DisplayName,
				TooltipText = $"Prepare as {option.DisplayName}.",
				CustomMinimumSize = new Vector2(96, 34),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			button.Pressed += () => TryPrepareSelectedIngredient(preparationId);

			var preview = new RichTextLabel
			{
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(96, 36),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FitContent = true,
				ScrollActive = false,
				MouseFilter = MouseFilterEnum.Ignore,
				BbcodeEnabled = true
			};
			preview.AddThemeFontSizeOverride("normal_font_size", 13);
			preview.AddThemeColorOverride("default_color", new Color(0.82f, 0.88f, 0.78f, 1f));

			column.AddChild(button);
			column.AddChild(preview);
			_preparationButtonsContainer.AddChild(column);
			_preparationButtons.Add(button);
			_preparationPreviewLabels[preparationId] = preview;
		}
	}

	private void TryPrepareSelectedIngredient(string preparationId)
	{
		if (string.IsNullOrWhiteSpace(_selectedIngredientId))
		{
			SetStatus("Choose an ingredient first.");
			return;
		}

		if (!_itemCatalog.TryGetItem(_selectedIngredientId, out var baseIngredient))
		{
			SetStatus("Selected ingredient is missing.");
			ClearSelection(returnIngredient: false);
			return;
		}

		if (!PreparedIngredientFactory.TryBuildPreparedIngredient(baseIngredient, preparationId, out var preparedIngredient, out var error))
		{
			SetStatus(error);
			return;
		}

		if (!_runtimeContentDb.UpsertRuntimeItem(preparedIngredient))
		{
			SetStatus("Could not register prepared ingredient.");
			return;
		}

		if (!_brewPanel.Visible)
			_brewPanel.ShowPanel();

		if (!_brewPanel.TryQueueReservedIngredient(preparedIngredient.Id))
		{
			SetStatus("Could not add prepared ingredient to brew.");
			return;
		}

		SetStatus($"{preparedIngredient.Name} added to brew.");
		ClearSelection(returnIngredient: false);
	}

	private void ClearSelection()
	{
		ClearSelection(returnIngredient: true);
	}

	private void ClearSelection(bool returnIngredient)
	{
		if (returnIngredient)
			ReturnSelectedIngredient();

		_selectedIngredientId = string.Empty;
		Refresh();
	}

	private void ReturnSelectedIngredient()
	{
		if (!string.IsNullOrWhiteSpace(_selectedIngredientId))
			_gameState.AddItem(_selectedIngredientId, 1);
	}

	private void Refresh()
	{
		ItemDef? selectedItem = null;
		if (string.IsNullOrWhiteSpace(_selectedIngredientId) ||
			!_itemCatalog.TryGetItem(_selectedIngredientId, out var item))
		{
			_ingredientIcon.Texture = null;
			_ingredientName.Text = DefaultIngredientName;
		}
		else
		{
			selectedItem = item;
			_ingredientIcon.Texture = UiIconLoader.LoadIcon(item.IconPath);
			_ingredientName.Text = item.Name;
		}

		var hasSelection = selectedItem is not null;
		_clearButton.Visible = hasSelection;
		_clearButton.Disabled = !hasSelection;

		foreach (var button in _preparationButtons)
			button.Disabled = !hasSelection;

		RefreshPreparationPreviews(selectedItem);

		if (!hasSelection && string.IsNullOrWhiteSpace(_statusLabel.Text))
			SetStatus(DefaultStatusText);
	}

	private void RefreshPreparationPreviews(ItemDef? item)
	{
		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (!_preparationPreviewLabels.TryGetValue(option.Id, out var label))
				continue;

			label.Text = item is null
				? string.Empty
				: BuildPreparationPreviewText(item, option.Id);
		}
	}

	private string BuildPreparationPreviewText(ItemDef item, string preparationId)
	{
		var preparationName = IngredientPreparationCatalog.GetDisplayName(preparationId);
		if (!_gameState.KnowsIngredientPreparation(item.Id, preparationId))
			return $"{preparationName}: {InventoryItemTextFormatter.UnknownPreparationStatsLabel}";

		if (!IngredientPreparationCatalog.TryGetPreparation(item, preparationId, out var preparation))
			return MissingPreviewText;

		var lines = new List<string>();
		foreach (var trait in preparation.Traits)
		{
			if (string.IsNullOrWhiteSpace(trait.Key) || trait.Value <= 0)
				continue;

			var traitName = BrewPanelTextFormatter.EscapeBbCodeText(InventoryItemTextFormatter.DisplayStatName(trait.Key));
			lines.Add($"[color=#6ED775]{traitName} +{trait.Value}[/color]");
		}

		foreach (var risk in preparation.Risks)
		{
			if (string.IsNullOrWhiteSpace(risk.Key) || risk.Value <= 0)
				continue;

			var riskName = BrewPanelTextFormatter.EscapeBbCodeText(InventoryItemTextFormatter.DisplayStatName(risk.Key));
			lines.Add($"[color=#F0544F]{riskName} +{risk.Value}[/color]");
		}

		return lines.Count == 0 ? $"{preparationName}: None" : $"{preparationName}: {string.Join("\n", lines)}";
	}

	private void SetStatus(string text)
	{
		if (_statusLabel is not null)
			_statusLabel.Text = text;
	}
}
