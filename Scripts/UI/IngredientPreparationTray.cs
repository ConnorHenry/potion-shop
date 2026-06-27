using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class IngredientPreparationTray : Control
{
	[Signal]
	public delegate void IngredientSelectedEventHandler(string itemId);

	[Signal]
	public delegate void IngredientPreparedEventHandler(string ingredientId, string preparationId, string preparedItemId);

	private const string DefaultIngredientName = "Drop raw ingredient";
	private const string DefaultStatusText = "Select a preparation.";
	private const string MissingPreviewText = "Unavailable";
	private const string IngredientAlreadyQueuedStatusText = "That ingredient has already been added to the brew.";

	[Export] public NodePath IngredientDropBoxPath = default!;
	[Export] public NodePath IngredientIconPath = default!;
	[Export] public NodePath IngredientNamePath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath PreparationButtonsContainerPath = default!;
	[Export] public NodePath ClearButtonPath = default!;
	[Export] public NodePath BrewPanelPath = new("../BrewPanel");
	[Export] public NodePath BoilingMiniGameWindowPath = new("../BoilingMiniGameWindow");
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
	private BoilingMiniGameWindow? _boilingMiniGameWindow;
	private RuntimeContentDb _runtimeContentDb = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private string _selectedIngredientId = string.Empty;
	private string _pendingBoilingIngredientId = string.Empty;
	private Control.GuiInputEventHandler? _dropBoxGuiInputHandler;
	private readonly Dictionary<string, Button> _preparationButtonsById = new(StringComparer.OrdinalIgnoreCase);
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
		_boilingMiniGameWindow = GetNodeOrNull<BoilingMiniGameWindow>(BoilingMiniGameWindowPath);
		if (_boilingMiniGameWindow is null)
			GD.PushError($"IngredientPreparationTray: BoilingMiniGameWindow was not found at '{BoilingMiniGameWindowPath}'.");
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
		if (_boilingMiniGameWindow is not null)
			_boilingMiniGameWindow.Completed += OnBoilingMiniGameCompleted;
		_gameState.Changed += Refresh;
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
		if (_boilingMiniGameWindow is not null)
			_boilingMiniGameWindow.Completed -= OnBoilingMiniGameCompleted;
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
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

		if (IsIngredientAlreadyQueuedForBrew(itemId))
		{
			SetStatus(IngredientAlreadyQueuedStatusText);
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
		// Let tutorial locks advance before refreshing buttons so the selected ingredient can enable Raw immediately.
		EmitSignal(SignalName.IngredientSelected, itemId);
		Refresh();
		return true;
	}

	public Control? GetPreparationDropBox()
	{
		return _ingredientDropBox;
	}

	public Button? GetPreparationButton(string preparationId)
	{
		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		return _preparationButtonsById.TryGetValue(normalizedPreparationId, out var button)
			? button
			: null;
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

		_preparationButtonsById.Clear();
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
			_preparationButtonsById[preparationId] = button;
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

		if (!_gameState.IsIngredientPreparationMethodEnabled(preparationId))
		{
			var preparationName = IngredientPreparationCatalog.GetDisplayName(preparationId);
			SetStatus($"{preparationName} preparation is disabled.");
			return;
		}

		if (!_itemCatalog.TryGetItem(_selectedIngredientId, out var baseIngredient))
		{
			SetStatus("Selected ingredient is missing.");
			ClearSelection(returnIngredient: false);
			return;
		}

		if (IsIngredientAlreadyQueuedForBrew(baseIngredient.Id))
		{
			SetStatus(IngredientAlreadyQueuedStatusText);
			return;
		}

		var normalizedPreparationId = IngredientPreparationCatalog.NormalizePreparationId(preparationId);
		if (string.Equals(normalizedPreparationId, IngredientPreparationCatalog.BoiledPreparationId, StringComparison.OrdinalIgnoreCase))
		{
			if (_gameState.DebugSkipBoilingMiniGame)
			{
				TryBuildAndQueuePreparedIngredient(baseIngredient, normalizedPreparationId, failedBoiling: false);
				return;
			}

			TryStartBoilingMiniGame(baseIngredient);
			return;
		}

		TryBuildAndQueuePreparedIngredient(baseIngredient, normalizedPreparationId, failedBoiling: false);
	}

	private void TryStartBoilingMiniGame(ItemDef baseIngredient)
	{
		if (_boilingMiniGameWindow is null)
		{
			SetStatus("Boiling station is not available.");
			return;
		}

		if (!IngredientPreparationCatalog.TryGetPreparation(
			baseIngredient,
			IngredientPreparationCatalog.BoiledPreparationId,
			out var boiledPreparation))
		{
			SetStatus($"{baseIngredient.Name} cannot be boiled.");
			return;
		}

		if (boiledPreparation.BoilingGame is null)
		{
			SetStatus("Boiling mini game is not configured.");
			GD.PushError($"IngredientPreparationTray: '{baseIngredient.Id}' has no boiling mini game data.");
			return;
		}

		_pendingBoilingIngredientId = _selectedIngredientId;
		_boilingMiniGameWindow.ShowForIngredient(baseIngredient.Name, baseIngredient.IconPath ?? string.Empty, boiledPreparation.BoilingGame);
		SetStatus("Boiling...");
	}

	private void OnBoilingMiniGameCompleted(bool succeeded)
	{
		var ingredientId = _pendingBoilingIngredientId;
		_pendingBoilingIngredientId = string.Empty;
		if (string.IsNullOrWhiteSpace(ingredientId))
			return;

		if (!_itemCatalog.TryGetItem(ingredientId, out var baseIngredient))
		{
			SetStatus("Selected ingredient is missing.");
			ClearSelection(returnIngredient: false);
			return;
		}

		TryBuildAndQueuePreparedIngredient(baseIngredient, IngredientPreparationCatalog.BoiledPreparationId, failedBoiling: !succeeded);
	}

	private void TryBuildAndQueuePreparedIngredient(ItemDef baseIngredient, string preparationId, bool failedBoiling)
	{
		if (IsIngredientAlreadyQueuedForBrew(baseIngredient.Id))
		{
			SetStatus(IngredientAlreadyQueuedStatusText);
			return;
		}

		ItemDef preparedIngredient;
		string error;
		if (failedBoiling)
		{
			if (!IngredientPreparationCatalog.TryGetPreparation(baseIngredient, preparationId, out var preparation) ||
				preparation.BoilingGame is null)
			{
				SetStatus("Boiling failure data is missing.");
				return;
			}

			if (!PreparedIngredientFactory.TryBuildFailedBoiledIngredient(baseIngredient, preparation.BoilingGame, out preparedIngredient, out error))
			{
				SetStatus(error);
				return;
			}
		}
		else if (!PreparedIngredientFactory.TryBuildPreparedIngredient(baseIngredient, preparationId, out preparedIngredient, out error))
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

		SetStatus(failedBoiling
			? $"{preparedIngredient.Name} spoiled and added to brew."
			: $"{preparedIngredient.Name} added to brew.");
		EmitSignal(SignalName.IngredientPrepared, baseIngredient.Id, preparationId, preparedIngredient.Id);
		ClearSelection(returnIngredient: false);
	}

	private bool IsIngredientAlreadyQueuedForBrew(string ingredientId)
	{
		if (string.IsNullOrWhiteSpace(ingredientId))
			return false;

		foreach (var queuedIngredient in _gameState.CloneQueuedBrewIngredients())
		{
			if (string.Equals(queuedIngredient.IngredientId, ingredientId, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private void ClearSelection()
	{
		ClearSelection(returnIngredient: true);
	}

	private void ClearSelection(bool returnIngredient)
	{
		if (returnIngredient)
			ReturnSelectedIngredient();

		_pendingBoilingIngredientId = string.Empty;
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

		foreach (var option in IngredientPreparationCatalog.AllOptions)
		{
			if (!_preparationButtonsById.TryGetValue(option.Id, out var button))
				continue;

			var preparationEnabled = _gameState.IsIngredientPreparationMethodEnabled(option.Id);
			var isRawPreparation = string.Equals(
				option.Id,
				IngredientPreparationCatalog.RawPreparationId,
				StringComparison.OrdinalIgnoreCase);
			button.Disabled = isRawPreparation ? false : !hasSelection || !preparationEnabled;
			button.TooltipText = preparationEnabled
				? $"Prepare as {option.DisplayName}."
				: $"{option.DisplayName} preparation is disabled.";
		}

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
