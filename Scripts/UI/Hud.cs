using Godot;
using OccultShop.Autoload;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class Hud : Control
{
	[Export] public NodePath GoldLabelPath = default!;
	[Export] public NodePath DreadLabelPath = default!;
	[Export] public NodePath DayLabelPath = default!;
	[Export] public NodePath ShopTimerLabelPath = default!;

	private Label _gold = default!;
	private Label _dread = default!;
	private Label _day = default!;
	private Label? _shopTimer;
	private Button _endDayButton = default!;
	private Button _serveCustomerButton = default!;
	private Button _brewPotionButton = default!;
	private Button _recipeBookButton = default!;
	private Button _settingsButton = default!;
	private Button _returnToMainMenuButton = default!;
	private Button _saveGameButton = default!;
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
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState is null)
		{
			GD.PushError("Hud: /root/GameState was not found.");
			return;
		}

		var saveGameManager = GetNodeOrNull<SaveGameManager>("/root/SaveGameManager");
		if (saveGameManager is null)
		{
			GD.PushError("Hud: /root/SaveGameManager was not found.");
			return;
		}

		var dayController = GetNodeOrNull<DayController>("/root/Main/DayController");
		if (dayController is null)
		{
			GD.PushError("Hud: /root/Main/DayController was not found.");
			return;
		}

		var brewPanel = GetNodeOrNull<Control>("/root/Main/CanvasLayer/BrewPanel");
		if (brewPanel is null)
		{
			GD.PushError("Hud: /root/Main/CanvasLayer/BrewPanel was not found.");
			return;
		}

		var recipeBookPanel = GetNodeOrNull<Control>("/root/Main/CanvasLayer/RecipeBookPanel");
		if (recipeBookPanel is null)
		{
			GD.PushError("Hud: /root/Main/CanvasLayer/RecipeBookPanel was not found.");
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
		_settingsButton = GetNode<Button>("MainMenu");
		_returnToMainMenuButton = GetNode<Button>("SettingsPanel/Margin/VBox/ReturnToMainMenu");
		_saveGameButton = GetNode<Button>("SettingsPanel/Margin/VBox/SaveGame");
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
		_saveConfirmationCloseButton.Pressed += HideSaveConfirmation;

		_gameState.Changed += Refresh;
		_dayController.ShopStateChanged += RefreshShopState;
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
		var secondsRemaining = _dayController.SecondsRemaining;

		if (_shopTimer is not null)
			_shopTimer.Text = isShopOpen
				? $"Shop Timer: {secondsRemaining}s"
				: "Shop Timer: Closed";

		_serveCustomerButton.Text = isShopOpen ? "Shop Open" : "Start Day";
		_serveCustomerButton.Disabled = isShopOpen;
		_endDayButton.Disabled = isShopOpen;
	}
}
