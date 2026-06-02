using Godot;
using ImGuiGodot;
using OccultShop.Autoload;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class Hud : Control
{
	private const int SettingsPanelZIndex = 4096;
	private const string SaveGameButtonDefaultText = "Save Game";
	private const string SaveGameButtonSavingText = "Saving Game...";
	private const string SaveGameButtonSavedText = "Game Saved!";
	private const string GardenScenePath = "res://Scenes/Main/Garden.tscn";

	[Export] public NodePath GoldLabelPath = default!;
	[Export] public NodePath DreadLabelPath = default!;
	[Export] public NodePath DayLabelPath = default!;
	[Export] public NodePath ShopTimerLabelPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath SaveGameManagerPath = new("/root/SaveGameManager");
	[Export] public NodePath DayControllerPath = new("/root/Main/DayController");
	[Export] public NodePath BrewPanelPath = new("../BrewPanel");
	[Export] public NodePath RecipeBookPanelPath = new("../RecipeBookPanel");

	private Label _gold = default!;
	private Label _dread = default!;
	private Label _day = default!;
	private Label? _shopTimer;
	private Button _endDayButton = default!;
	private Button _serveCustomerButton = default!;
	private Button _brewPotionButton = default!;
	private Button _recipeBookButton = default!;
	private Button _gardenButton = default!;
	private Button _settingsButton = default!;
	private Button _returnToMainMenuButton = default!;
	private Button _saveGameButton = default!;
	private Button _toggleDebugPanelButton = default!;
	private GameState? _gameState;
	private SaveGameManager? _saveGameManager;
	private DayController? _dayController;
	private Control? _brewPanel;
	private Control? _recipeBookPanel;
	private Control _settingsPanel = default!;
	private bool _isSavingGame;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"Hud: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (saveGameManager is null)
		{
			GD.PushError($"Hud: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return;
		}

		var dayController = GetNodeOrNull<DayController>(DayControllerPath);
		if (dayController is null)
		{
			GD.PushError($"Hud: DayController was not found at '{DayControllerPath}'.");
			return;
		}

		var brewPanel = GetNodeOrNull<Control>(BrewPanelPath);
		if (brewPanel is null)
		{
			GD.PushError($"Hud: BrewPanel was not found at '{BrewPanelPath}'.");
			return;
		}

		var recipeBookPanel = GetNodeOrNull<Control>(RecipeBookPanelPath);
		if (recipeBookPanel is null)
		{
			GD.PushError($"Hud: RecipeBookPanel was not found at '{RecipeBookPanelPath}'.");
			return;
		}

		_gameState = gameState;
		_saveGameManager = saveGameManager;
		_dayController = dayController;
		_brewPanel = brewPanel;
		_recipeBookPanel = recipeBookPanel;

		_gold = GetNode<Label>(GoldLabelPath);
		_dread = GetNode<Label>(DreadLabelPath);
		_day = GetNode<Label>(DayLabelPath);
		_shopTimer = GetNodeOrNull<Label>(ShopTimerLabelPath);
		if (_shopTimer is null)
			_shopTimer = GetNodeOrNull<Label>("ShopTimer");

		if (_shopTimer is null)
			GD.PushError("Hud: Shop timer label node is missing.");

		_endDayButton = GetNode<Button>("EndDay");
		_serveCustomerButton = GetNode<Button>("ServeCustomer");
		_brewPotionButton = GetNode<Button>("BrewPotion");
		_recipeBookButton = GetNode<Button>("RecipeBook");
		_gardenButton = GetNode<Button>("Garden");
		_settingsButton = GetNode<Button>("MainMenu");
		_returnToMainMenuButton = GetNode<Button>("SettingsPanel/Margin/VBox/ReturnToMainMenu");
		_saveGameButton = GetNode<Button>("SettingsPanel/Margin/VBox/SaveGame");
		_toggleDebugPanelButton = GetNode<Button>("SettingsPanel/Margin/VBox/ToggleDebugPanel");
		_settingsPanel = GetNode<Control>("SettingsPanel");

		_settingsPanel.ZIndex = SettingsPanelZIndex;
		SetProcessInput(true);

		_endDayButton.Pressed += OnEndDayPressed;
		_serveCustomerButton.Pressed += OnStartDayPressed;
		_brewPotionButton.Pressed += OnBrewPotionPressed;
		_recipeBookButton.Pressed += OnRecipeBookPressed;
		_gardenButton.Pressed += OnGardenPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
		_saveGameButton.Pressed += OnSaveGamePressed;
		_toggleDebugPanelButton.Pressed += OnToggleDebugPanelPressed;

		_gameState.Changed += Refresh;
		_dayController.ShopStateChanged += RefreshShopState;
		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
		SetSettingsPanelVisible(false);
		Refresh();
		RefreshShopState();
		_saveGameButton.Text = SaveGameButtonDefaultText;
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		if (_dayController is not null)
			_dayController.ShopStateChanged -= RefreshShopState;
		if (_endDayButton is not null)
			_endDayButton.Pressed -= OnEndDayPressed;
		if (_serveCustomerButton is not null)
			_serveCustomerButton.Pressed -= OnStartDayPressed;
		if (_brewPotionButton is not null)
			_brewPotionButton.Pressed -= OnBrewPotionPressed;
		if (_recipeBookButton is not null)
			_recipeBookButton.Pressed -= OnRecipeBookPressed;
		if (_gardenButton is not null)
			_gardenButton.Pressed -= OnGardenPressed;
		if (_settingsButton is not null)
			_settingsButton.Pressed -= OnSettingsPressed;
		if (_returnToMainMenuButton is not null)
			_returnToMainMenuButton.Pressed -= OnReturnToMainMenuPressed;
		if (_saveGameButton is not null)
			_saveGameButton.Pressed -= OnSaveGamePressed;
		if (_toggleDebugPanelButton is not null)
			_toggleDebugPanelButton.Pressed -= OnToggleDebugPanelPressed;
	}

	public override void _Input(InputEvent @event)
	{
		if (!_settingsPanel.Visible)
			return;

		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		if (_settingsPanel.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
			return;

		AcceptEvent();
		SetSettingsPanelVisible(false);
	}

	private void Refresh()
	{
		if (_gameState is null)
			return;

		_gold.Text = $"Gold: {_gameState.Gold}";
		_dread.Text = $"Dread: {_gameState.Dread}";
		_day.Text = $"Day: {_gameState.Day}";
		RefreshShopState();
	}

	private void OnEndDayPressed()
	{
		if (_dayController is null)
			return;

		_dayController.EndDayAndRunNight();
	}

	private void OnStartDayPressed()
	{
		if (_dayController is null)
			return;

		_dayController.StartShopDay();
	}

	private void OnBrewPotionPressed()
	{
		if (_brewPanel is null)
			return;

		_brewPanel.Visible = !_brewPanel.Visible;
	}

	private void OnRecipeBookPressed()
	{
		if (_recipeBookPanel is null)
			return;

		if (_recipeBookPanel is PotionBookPanel potionBookPanel)
		{
			potionBookPanel.Toggle();
			return;
		}

		_recipeBookPanel.Visible = !_recipeBookPanel.Visible;
	}

	private void OnGardenPressed()
	{
		if (_dayController is null)
			return;
		if (_dayController.IsShopOpen)
			return;

		TryAutoSave("entering the garden");
		Error error = GetTree().ChangeSceneToFile(GardenScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load garden scene. Error: {error}");
		}
	}

	private void OnSettingsPressed()
	{
		SetSettingsPanelVisible(!_settingsPanel.Visible);
	}

	private void OnReturnToMainMenuPressed()
	{
		Error error = GetTree().ChangeSceneToFile("res://MainMenu.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load main menu scene. Error: {error}");
		}
	}

	private void OnSaveGamePressed()
	{
		if (_saveGameManager is null || _isSavingGame)
			return;

		_isSavingGame = true;
		_saveGameButton.Disabled = true;
		_saveGameButton.Text = SaveGameButtonSavingText;
		Callable.From(PerformSaveGame).CallDeferred();
	}

	private void PerformSaveGame()
	{
		if (_saveGameManager is null)
		{
			FinishSaveGame(saveSucceeded: false);
			return;
		}

		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError("Hud: Save failed.");

		FinishSaveGame(saveSucceeded);
	}

	private bool TryAutoSave(string context)
	{
		if (_saveGameManager is null)
			return false;

		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"Hud: Auto-save failed before {context}.");

		return saveSucceeded;
	}

	private void OnToggleDebugPanelPressed()
	{
		ImGuiGD.Visible = !ImGuiGD.Visible;
		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
	}

	private void FinishSaveGame(bool saveSucceeded)
	{
		_isSavingGame = false;
		_saveGameButton.Disabled = false;

		if (!_settingsPanel.Visible)
		{
			_saveGameButton.Text = SaveGameButtonDefaultText;
			return;
		}

		_saveGameButton.Text = saveSucceeded
			? SaveGameButtonSavedText
			: SaveGameButtonDefaultText;
	}

	private void ResetSaveGameButtonText()
	{
		_saveGameButton.Text = SaveGameButtonDefaultText;
		if (!_isSavingGame)
			_saveGameButton.Disabled = false;
	}

	private void SetSettingsPanelVisible(bool visible)
	{
		_settingsPanel.Visible = visible;

		if (visible)
			_settingsPanel.MoveToFront();

		if (!visible)
			ResetSaveGameButtonText();
	}

	private void RefreshShopState()
	{
		if (_dayController is null)
			return;

		var isShopOpen = _dayController.IsShopOpen;
		var secondsRemaining = _dayController.SecondsRemaining;

		if (_shopTimer is not null)
			_shopTimer.Text = isShopOpen
				? $"Shop Timer: {secondsRemaining}s"
				: "Shop Timer: Closed";

		_serveCustomerButton.Text = isShopOpen ? "Shop Open" : "Start Day";
		_serveCustomerButton.Disabled = isShopOpen;
		_endDayButton.Disabled = isShopOpen;
		_gardenButton.Disabled = isShopOpen;
	}
}
