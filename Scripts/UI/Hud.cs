using Godot;
using OccultShop.Autoload;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class Hud : Control
{
	[Export] public NodePath GoldLabelPath = default!;
	[Export] public NodePath DreadLabelPath = default!;
	[Export] public NodePath DayLabelPath = default!;

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
	private Control _saveConfirmationPanel = default!;
	private Label _saveConfirmationLabel = default!;
	private Button _saveConfirmationCloseButton = default!;
	private DayController _dayController = default!;
	private Control _brewPanel = default!;
	private Control _recipeBookPanel = default!;
	private Control _settingsPanel = default!;

	public override void _Ready()
	{
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
		_saveConfirmationPanel = GetNode<Control>("SaveConfirmationPanel");
		_saveConfirmationLabel = GetNode<Label>("SaveConfirmationPanel/Panel/Margin/VBox/Message");
		_saveConfirmationCloseButton = GetNode<Button>("SaveConfirmationPanel/Panel/Margin/VBox/Close");
		_settingsPanel = GetNode<Control>("SettingsPanel");

		_endDayButton.Pressed += OnEndDayPressed;
		_serveCustomerButton.Pressed += OnServeCustomerPressed;
		_brewPotionButton.Pressed += OnBrewPotionPressed;
		_recipeBookButton.Pressed += OnRecipeBookPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
		_saveGameButton.Pressed += OnSaveGamePressed;
		_saveConfirmationCloseButton.Pressed += HideSaveConfirmation;

		GameState.Changed += Refresh;
		Refresh();
		HideSaveConfirmation();
	}

	public override void _ExitTree()
	{
		GameState.Changed -= Refresh;
		if (_endDayButton != null)
			_endDayButton.Pressed -= OnEndDayPressed;
		if (_serveCustomerButton != null)
			_serveCustomerButton.Pressed -= OnServeCustomerPressed;
		if (_brewPotionButton != null)
			_brewPotionButton.Pressed -= OnBrewPotionPressed;
		if (_recipeBookButton != null)
			_recipeBookButton.Pressed -= OnRecipeBookPressed;
		if (_settingsButton != null)
			_settingsButton.Pressed -= OnSettingsPressed;
		if (_returnToMainMenuButton != null)
			_returnToMainMenuButton.Pressed -= OnReturnToMainMenuPressed;
		if (_saveGameButton != null)
			_saveGameButton.Pressed -= OnSaveGamePressed;
		if (_saveConfirmationCloseButton != null)
			_saveConfirmationCloseButton.Pressed -= HideSaveConfirmation;
	}

	private void Refresh()
	{
		_gold.Text = $"Gold: {GameState.Gold}";
		_dread.Text = $"Dread: {GameState.Dread}";
		_day.Text = $"Day: {GameState.Day}";
	}

	private void OnEndDayPressed()
	{
		DayController.EndDayAndRunNight();
	}

	private void OnServeCustomerPressed()
	{
		DayController.ServeCustomer();
	}

	private void OnBrewPotionPressed()
	{
		var brewPanel = BrewPanel;
		brewPanel.Visible = !brewPanel.Visible;
	}

	private void OnRecipeBookPressed()
	{
		var recipeBookPanel = RecipeBookPanel;
		recipeBookPanel.Visible = !recipeBookPanel.Visible;
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
		if (!SaveGameManager.SaveGame())
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
		if (_saveConfirmationPanel is null)
			return;

		_saveConfirmationPanel.Visible = false;
	}

	private static GameState GameState => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameState>("GameState");
	private static SaveGameManager SaveGameManager => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<SaveGameManager>("SaveGameManager");
	private static DayController DayController => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<DayController>("Main/DayController");
	private static Control BrewPanel => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<Control>("Main/CanvasLayer/BrewPanel");
	private static Control RecipeBookPanel => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<Control>("Main/CanvasLayer/RecipeBookPanel");
}
