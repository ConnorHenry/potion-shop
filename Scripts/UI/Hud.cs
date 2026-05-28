using Godot;
using ImGuiGodot;
using OccultShop.Autoload;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class Hud : Control
{
	[Export] public NodePath GoldLabelPath = default!;
	[Export] public NodePath DreadLabelPath = default!;
	[Export] public NodePath DayLabelPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath SaveGameManagerPath = new("/root/SaveGameManager");
	[Export] public NodePath DayControllerPath = new("/root/Main/DayController");
	[Export] public NodePath BrewPanelPath = new("../BrewPanel");
	[Export] public NodePath RecipeBookPanelPath = new("../RecipeBookPanel");

	private Label _gold = default!;
	private Label _dread = default!;
	private Label _day = default!;
	private Button _endDayButton = default!;
	private Button _serveCustomerButton = default!;
	private Button _brewPotionButton = default!;
	private Button _recipeBookButton = default!;
	private Button _settingsButton = default!;
	private Button _returnToMainMenuButton = default!;
	private Button _saveGameButton = default!;
	private Button _toggleDebugPanelButton = default!;
	private Control _saveConfirmationPanel = default!;
	private Label _saveConfirmationLabel = default!;
	private Button _saveConfirmationCloseButton = default!;
	private GameState? _gameState;
	private SaveGameManager? _saveGameManager;
	private DayController? _dayController;
	private Control? _brewPanel;
	private Control? _recipeBookPanel;
	private Control _settingsPanel = default!;

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

		_endDayButton = GetNode<Button>("EndDay");
		_serveCustomerButton = GetNode<Button>("ServeCustomer");
		_brewPotionButton = GetNode<Button>("BrewPotion");
		_recipeBookButton = GetNode<Button>("RecipeBook");
		_settingsButton = GetNode<Button>("MainMenu");
		_returnToMainMenuButton = GetNode<Button>("SettingsPanel/Margin/VBox/ReturnToMainMenu");
		_saveGameButton = GetNode<Button>("SettingsPanel/Margin/VBox/SaveGame");
		_toggleDebugPanelButton = GetNode<Button>("SettingsPanel/Margin/VBox/ToggleDebugPanel");
		_saveConfirmationPanel = GetNode<Control>("SaveConfirmationPanel");
		_saveConfirmationLabel = GetNode<Label>("SaveConfirmationPanel/Panel/Margin/VBox/Message");
		_saveConfirmationCloseButton = GetNode<Button>("SaveConfirmationPanel/Panel/Margin/VBox/Close");
		_settingsPanel = GetNode<Control>("SettingsPanel");

		_endDayButton.Pressed += OnEndDayPressed;
		_serveCustomerButton.Pressed += OnStartDayPressed;
		_brewPotionButton.Pressed += OnBrewPotionPressed;
		_recipeBookButton.Pressed += OnRecipeBookPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
		_saveGameButton.Pressed += OnSaveGamePressed;
		_toggleDebugPanelButton.Pressed += OnToggleDebugPanelPressed;
		_saveConfirmationCloseButton.Pressed += HideSaveConfirmation;

		_gameState.Changed += Refresh;
		_dayController.ShopStateChanged += RefreshShopState;
		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
		Refresh();
		RefreshShopState();
		HideSaveConfirmation();
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
		if (_settingsButton is not null)
			_settingsButton.Pressed -= OnSettingsPressed;
		if (_returnToMainMenuButton is not null)
			_returnToMainMenuButton.Pressed -= OnReturnToMainMenuPressed;
		if (_saveGameButton is not null)
			_saveGameButton.Pressed -= OnSaveGamePressed;
		if (_toggleDebugPanelButton is not null)
			_toggleDebugPanelButton.Pressed -= OnToggleDebugPanelPressed;
		if (_saveConfirmationCloseButton is not null)
			_saveConfirmationCloseButton.Pressed -= HideSaveConfirmation;
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

	private void OnSettingsPressed()
	{
		_settingsPanel.Visible = !_settingsPanel.Visible;
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
		if (_saveGameManager is null)
			return;

		if (!_saveGameManager.SaveGame())
		{
			GD.PushError("Hud: Save failed.");
			return;
		}

		ShowSaveConfirmation("Game saved successfully.");
	}

	private void OnToggleDebugPanelPressed()
	{
		ImGuiGD.Visible = !ImGuiGD.Visible;
		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
	}

	private void ShowSaveConfirmation(string message)
	{
		_saveConfirmationLabel.Text = message;
		_saveConfirmationPanel.Visible = true;
	}

	private void HideSaveConfirmation()
	{
		_saveConfirmationPanel.Visible = false;
	}

	private void RefreshShopState()
	{
		if (_dayController is null)
			return;

		var isShopOpen = _dayController.IsShopOpen;

		_serveCustomerButton.Text = isShopOpen ? "Shop Open" : "Start Day";
		_serveCustomerButton.Disabled = isShopOpen;
		_endDayButton.Disabled = isShopOpen;
	}
}
