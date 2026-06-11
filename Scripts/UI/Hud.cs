using Godot;
using ImGuiGodot;
using OccultShop.Autoload;
using OccultShop.Controllers;
using OccultShop.Infrastructure;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class Hud : Control
{
	private const int SettingsPanelZIndex = 4096;
	private const int SettingsDetailsPanelZIndex = SettingsPanelZIndex;
	private const string SaveGameButtonDefaultText = "Save Game";
	private const string SaveGameButtonSavingText = "Saving Game...";
	private const string SaveGameButtonSavedText = "Game Saved!";
	private const string RainfallAudioPath = "res://Assets/Audio/rain-sounds.mp3";
	private const string AmbientSettingsPath = "user://settings.cfg";
	private const string AmbientSettingsSection = "audio";
	private const string AmbientSoundsEnabledKey = "ambient_sounds_enabled";
	private const string RainfallVolumeKey = "rainfall_volume";
	private const int RequestPanelZIndex = SettingsPanelZIndex;
	private const float RequestPanelTopOffset = 54.0f;
	private const float RequestPanelHorizontalMargin = 16.0f;
	private const float RequestPanelFallbackWidth = 340.0f;
	private const float RequestPanelMinimumHeight = 0.0f;
	private const bool DefaultAmbientSoundsEnabled = true;
	private const double DefaultRainfallVolume = 0.7;

	[Export] public NodePath GoldLabelPath = new("Content/Status/Gold");
	[Export] public NodePath DayLabelPath = new("Content/Status/Day");
	[Export] public NodePath RequestAlertButtonPath = new("Content/Status/RequestAlert");
	[Export] public NodePath RequestPanelPath = new("RequestPanel");
	[Export] public NodePath RequestDescriptionLabelPath = new("RequestPanel/Margin/VBox/Description");
	[Export] public NodePath RequestDesiredTraitsLabelPath = new("RequestPanel/Margin/VBox/Traits/DesiredColumn/DesiredTraits");
	[Export] public NodePath RequestBadTraitsLabelPath = new("RequestPanel/Margin/VBox/Traits/BadColumn/BadTraits");
	[Export] public NodePath StartDayButtonPath = new("Content/Actions/ServeCustomer");
	[Export] public NodePath GardenButtonPath = new("Content/Actions/Garden");
	[Export] public NodePath MapButtonPath = new("Content/Actions/Map");
	[Export] public NodePath SettingsButtonPath = new("Content/Actions/MainMenu");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public NodePath DayControllerPath = new("DayController");

	private Label _gold = default!;
	private Label _day = default!;
	private Button _requestAlertButton = default!;
	private Button _serveCustomerButton = default!;
	private Button _gardenButton = default!;
	private Button _mapButton = default!;
	private Button _settingsButton = default!;
	private Button _returnToMainMenuButton = default!;
	private Button _saveGameButton = default!;
	private Button _openSettingsButton = default!;
	private Button _toggleDebugPanelButton = default!;
	private CheckBox _ambientSoundsToggle = default!;
	private HSlider _rainfallVolumeSlider = default!;
	private AudioStreamPlayer _ambientRainPlayer = default!;
	private Control _requestPanel = default!;
	private RichTextLabel _requestDescription = default!;
	private RichTextLabel _requestDesiredTraits = default!;
	private RichTextLabel _requestBadTraits = default!;
	private GameState? _gameState;
	private SaveGameManager? _saveGameManager;
	private DayController? _dayController;
	private Control _settingsPanel = default!;
	private Control _settingsDetailsPanel = default!;
	private bool _isSavingGame;
	private bool _ambientSoundsEnabled = DefaultAmbientSoundsEnabled;
	private bool _ambientPlaybackAllowed;
	private double _rainfallVolume = DefaultRainfallVolume;

	public override void _Ready()
	{
		_gold = GetNode<Label>(GoldLabelPath);
		_day = GetNode<Label>(DayLabelPath);

		_serveCustomerButton = GetNode<Button>(StartDayButtonPath);
		_requestAlertButton = GetNode<Button>(RequestAlertButtonPath);
		_gardenButton = GetNode<Button>(GardenButtonPath);
		_mapButton = GetNode<Button>(MapButtonPath);
		_settingsButton = GetNode<Button>(SettingsButtonPath);
		_returnToMainMenuButton = GetNode<Button>("SettingsPanel/Margin/VBox/ReturnToMainMenu");
		_saveGameButton = GetNode<Button>("SettingsPanel/Margin/VBox/SaveGame");
		_openSettingsButton = GetNode<Button>("SettingsPanel/Margin/VBox/OpenSettings");
		_toggleDebugPanelButton = GetNode<Button>("SettingsPanel/Margin/VBox/ToggleDebugPanel");
		_settingsPanel = GetNode<Control>("SettingsPanel");
		_settingsDetailsPanel = GetNode<Control>("Settings");
		_ambientSoundsToggle = GetNode<CheckBox>("Settings/Margin/VBox/AmbientSounds");
		_rainfallVolumeSlider = GetNode<HSlider>("Settings/Margin/VBox/RainfallVolumeRow/RainfallVolume");
		_ambientRainPlayer = GetNode<AudioStreamPlayer>("AmbientRainPlayer");
		_requestPanel = GetNode<Control>(RequestPanelPath);
		_requestDescription = GetNode<RichTextLabel>(RequestDescriptionLabelPath);
		_requestDesiredTraits = GetNode<RichTextLabel>(RequestDesiredTraitsLabelPath);
		_requestBadTraits = GetNode<RichTextLabel>(RequestBadTraitsLabelPath);

		_gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (_gameState is null)
			GD.PushError($"Hud: GameState was not found at '{GameStatePath}'.");

		_saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (_saveGameManager is null)
			GD.PushError($"Hud: SaveGameManager was not found at '{SaveGameManagerPath}'.");

		_settingsPanel.ZIndex = SettingsPanelZIndex;
		_settingsDetailsPanel.ZIndex = SettingsDetailsPanelZIndex;
		_requestPanel.ZIndex = RequestPanelZIndex;
		_requestDescription.BbcodeEnabled = true;
		_requestDesiredTraits.BbcodeEnabled = true;
		_requestBadTraits.BbcodeEnabled = true;
		LoadAmbientSettings();
		ConfigureAmbientRainPlayer();
		ApplyAmbientSettingsToControls();
		SetProcessInput(true);

		_serveCustomerButton.Pressed += OnStartDayPressed;
		_requestAlertButton.Pressed += OnRequestAlertPressed;
		_gardenButton.Pressed += OnGardenPressed;
		_mapButton.Pressed += OnMapPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
		_saveGameButton.Pressed += OnSaveGamePressed;
		_openSettingsButton.Pressed += OnOpenSettingsPressed;
		_toggleDebugPanelButton.Pressed += OnToggleDebugPanelPressed;
		_ambientSoundsToggle.Toggled += OnAmbientSoundsToggled;
		_rainfallVolumeSlider.ValueChanged += OnRainfallVolumeChanged;
		_ambientRainPlayer.Finished += OnAmbientRainFinished;

		if (_gameState is not null)
			_gameState.Changed += Refresh;

		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
		SetRequestPanelVisible(false);
		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(false);
		RefreshSceneBindings();
		Refresh();
		_saveGameButton.Text = SaveGameButtonDefaultText;
		_saveGameButton.Disabled = _saveGameManager is null;
		SetAmbientPlaybackAllowed(Visible);
	}

	public override void _ExitTree()
	{
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
		DisconnectSceneBindings();
		if (_serveCustomerButton is not null)
			_serveCustomerButton.Pressed -= OnStartDayPressed;
		if (_requestAlertButton is not null)
			_requestAlertButton.Pressed -= OnRequestAlertPressed;
		if (_gardenButton is not null)
			_gardenButton.Pressed -= OnGardenPressed;
		if (_mapButton is not null)
			_mapButton.Pressed -= OnMapPressed;
		if (_settingsButton is not null)
			_settingsButton.Pressed -= OnSettingsPressed;
		if (_returnToMainMenuButton is not null)
			_returnToMainMenuButton.Pressed -= OnReturnToMainMenuPressed;
		if (_saveGameButton is not null)
			_saveGameButton.Pressed -= OnSaveGamePressed;
		if (_openSettingsButton is not null)
			_openSettingsButton.Pressed -= OnOpenSettingsPressed;
		if (_toggleDebugPanelButton is not null)
			_toggleDebugPanelButton.Pressed -= OnToggleDebugPanelPressed;
		if (_ambientSoundsToggle is not null)
			_ambientSoundsToggle.Toggled -= OnAmbientSoundsToggled;
		if (_rainfallVolumeSlider is not null)
			_rainfallVolumeSlider.ValueChanged -= OnRainfallVolumeChanged;
		if (_ambientRainPlayer is not null)
			_ambientRainPlayer.Finished -= OnAmbientRainFinished;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsHudPopupVisible())
			return;

		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		if (IsPointInsideVisibleControl(_settingsPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_settingsDetailsPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_requestPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_requestAlertButton, mouseButton.GlobalPosition))
			return;

		AcceptEvent();
		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(false);
		SetRequestPanelVisible(false);
	}

	public void RefreshSceneBindings()
	{
		DisconnectSceneBindings();
		if (_requestPanel is not null)
			SetRequestPanelVisible(false);

		var currentScene = GetTree().CurrentScene;
		if (currentScene is not null)
		{
			_dayController = GetSceneNodeOrNull<DayController>(currentScene, DayControllerPath);
		}

		if (_dayController is not null)
			_dayController.ShopStateChanged += RefreshShopState;

		Refresh();
	}

	public void HideSettingsPanel()
	{
		if (_settingsPanel is null)
			return;

		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(false);
	}

	public void SetAmbientPlaybackAllowed(bool allowed)
	{
		_ambientPlaybackAllowed = allowed;
		RefreshAmbientRainPlayback();
	}

	private void DisconnectSceneBindings()
	{
		if (_dayController is not null)
			_dayController.ShopStateChanged -= RefreshShopState;

		_dayController = null;
	}

	private static TNode? GetSceneNodeOrNull<TNode>(Node currentScene, NodePath path) where TNode : Node
	{
		return currentScene.GetNodeOrNull<TNode>(path);
	}

	private void Refresh()
	{
		if (_gameState is not null)
		{
			_gold.Text = $"Gold: {_gameState.Gold}";
			_day.Text = $"Day: {_gameState.Day}";
		}

		RefreshRequestAlert();
		RefreshShopState();
	}

	private void OnStartDayPressed()
	{
		if (_dayController is null)
			return;

		_dayController.StartShopDay();
	}

	private void OnRequestAlertPressed()
	{
		if (_gameState?.ActiveCustomerRequest is null)
			return;

		SetRequestPanelVisible(!_requestPanel.Visible);
	}

	private void OnGardenPressed()
	{
		if (_dayController is not null && _dayController.IsShopOpen)
			return;
		if (GetTree().CurrentScene is Garden)
			return;

		TryAutoSave("entering the garden");
		Error error = GetTree().ChangeSceneToFile(ScenePaths.Garden);
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load garden scene. Error: {error}");
		}
	}

	private void OnMapPressed()
	{
		if (GetTree().CurrentScene is Map)
			return;

		TryAutoSave("entering the map");
		Error error = GetTree().ChangeSceneToFile(ScenePaths.Map);
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load map scene. Error: {error}");
		}
	}

	private void OnSettingsPressed()
	{
		var shouldOpen = !_settingsPanel.Visible;
		SetSettingsPanelVisible(shouldOpen);
		if (shouldOpen)
			SetSettingsDetailsVisible(false);
	}

	private void OnOpenSettingsPressed()
	{
		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(true);
	}

	private void OnReturnToMainMenuPressed()
	{
		Error error = GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
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

	private void OnAmbientSoundsToggled(bool enabled)
	{
		_ambientSoundsEnabled = enabled;
		SaveAmbientSettings();
		RefreshAmbientRainPlayback();
	}

	private void OnRainfallVolumeChanged(double value)
	{
		_rainfallVolume = ClampNormalizedVolume(value);
		SaveAmbientSettings();
		RefreshAmbientRainPlayback();
	}

	private void OnAmbientRainFinished()
	{
		if (ShouldPlayAmbientRain())
			_ambientRainPlayer.Play();
	}

	private void FinishSaveGame(bool saveSucceeded)
	{
		_isSavingGame = false;
		_saveGameButton.Disabled = _saveGameManager is null;

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
			_saveGameButton.Disabled = _saveGameManager is null;
	}

	private void SetSettingsPanelVisible(bool visible)
	{
		_settingsPanel.Visible = visible;

		if (visible)
			_settingsPanel.MoveToFront();

		if (!visible)
			ResetSaveGameButtonText();
	}

	private void SetSettingsDetailsVisible(bool visible)
	{
		_settingsDetailsPanel.Visible = visible;

		if (visible)
			_settingsDetailsPanel.MoveToFront();
	}

	private void RefreshRequestAlert()
	{
		var request = _gameState?.ActiveCustomerRequest;
		if (request is null)
		{
			_requestAlertButton.Visible = false;
			_requestAlertButton.Disabled = true;
			ClearRequestPanelText();
			SetRequestPanelVisible(false);
			return;
		}

		_requestAlertButton.Visible = true;
		_requestAlertButton.Disabled = false;
		SetRequestPanelText(request);
		if (_requestPanel.Visible)
			ResizeAndPositionRequestPanelUnderAlert();
	}

	private void SetRequestPanelText(CustomerRequestDef request)
	{
		_requestDescription.Text = CustomerDialogueTextFormatter.EscapeBbCodeText(request.Description);
		_requestDesiredTraits.Text = CustomerDialogueTextFormatter.BuildDesiredRequestText(request, null);
		_requestBadTraits.Text = CustomerDialogueTextFormatter.BuildBadRequestText(request, null, null);
	}

	private void ClearRequestPanelText()
	{
		_requestDescription.Text = "";
		_requestDesiredTraits.Text = "";
		_requestBadTraits.Text = "";
	}

	private void SetRequestPanelVisible(bool visible)
	{
		_requestPanel.Visible = visible;

		if (!visible)
			return;

		ResizeAndPositionRequestPanelUnderAlert();
		_requestPanel.MoveToFront();
	}

	private void ResizeAndPositionRequestPanelUnderAlert()
	{
		var alertRect = _requestAlertButton.GetGlobalRect();
		var hudRect = GetGlobalRect();
		var panelWidth = GetRequestPanelWidth();
		var panelHeight = GetRequestPanelHeight();

		var maxX = Mathf.Max(RequestPanelHorizontalMargin, Size.X - panelWidth - RequestPanelHorizontalMargin);
		var localX = alertRect.Position.X - hudRect.Position.X;
		localX = Mathf.Clamp(localX, RequestPanelHorizontalMargin, maxX);
		_requestPanel.Position = new Vector2(localX, RequestPanelTopOffset);
		_requestPanel.Size = new Vector2(panelWidth, panelHeight);
	}

	private float GetRequestPanelWidth()
	{
		var panelWidth = _requestPanel.CustomMinimumSize.X;
		if (panelWidth <= 0.0f)
			panelWidth = _requestPanel.GetCombinedMinimumSize().X;
		if (panelWidth <= 0.0f)
			panelWidth = RequestPanelFallbackWidth;

		return panelWidth;
	}

	private float GetRequestPanelHeight()
	{
		var panelHeight = _requestPanel.GetCombinedMinimumSize().Y;
		if (panelHeight < RequestPanelMinimumHeight)
			panelHeight = RequestPanelMinimumHeight;

		return panelHeight;
	}

	private void LoadAmbientSettings()
	{
		var config = new ConfigFile();
		Error error = config.Load(AmbientSettingsPath);
		if (error == Error.FileNotFound)
			return;
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load ambient audio settings. Error: {error}");
			return;
		}

		_ambientSoundsEnabled = config
			.GetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, DefaultAmbientSoundsEnabled)
			.AsBool();
		_rainfallVolume = ClampNormalizedVolume(config
			.GetValue(AmbientSettingsSection, RainfallVolumeKey, DefaultRainfallVolume)
			.AsDouble());
	}

	private void SaveAmbientSettings()
	{
		var config = new ConfigFile();
		config.SetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, _ambientSoundsEnabled);
		config.SetValue(AmbientSettingsSection, RainfallVolumeKey, _rainfallVolume);

		Error error = config.Save(AmbientSettingsPath);
		if (error != Error.Ok)
			GD.PushError($"Hud: Failed to save ambient audio settings. Error: {error}");
	}

	private void ConfigureAmbientRainPlayer()
	{
		var stream = ResourceLoader.Load<AudioStream>(RainfallAudioPath);
		if (stream is null)
		{
			GD.PushError($"Hud: Rainfall audio stream could not be loaded from '{RainfallAudioPath}'.");
			return;
		}

		_ambientRainPlayer.Stream = stream;
		_ambientRainPlayer.VolumeDb = GetRainfallVolumeDb();
	}

	private void ApplyAmbientSettingsToControls()
	{
		_ambientSoundsToggle.ButtonPressed = _ambientSoundsEnabled;
		_rainfallVolumeSlider.Value = _rainfallVolume;
	}

	private void RefreshAmbientRainPlayback()
	{
		if (_ambientRainPlayer is null)
			return;

		_ambientRainPlayer.VolumeDb = GetRainfallVolumeDb();
		if (ShouldPlayAmbientRain())
		{
			if (!_ambientRainPlayer.Playing)
				_ambientRainPlayer.Play();
			return;
		}

		if (_ambientRainPlayer.Playing)
			_ambientRainPlayer.Stop();
	}

	private bool ShouldPlayAmbientRain()
	{
		return _ambientPlaybackAllowed
			&& _ambientSoundsEnabled
			&& _ambientRainPlayer is not null
			&& _ambientRainPlayer.Stream is not null;
	}

	private float GetRainfallVolumeDb()
	{
		return _rainfallVolume <= 0.0001
			? -80.0f
			: Mathf.LinearToDb((float)_rainfallVolume);
	}

	private static double ClampNormalizedVolume(double value)
	{
		if (value < 0.0)
			return 0.0;
		if (value > 1.0)
			return 1.0;

		return value;
	}

	private static bool IsPointInsideVisibleControl(Control control, Vector2 point)
	{
		return control.Visible && control.GetGlobalRect().HasPoint(point);
	}

	private bool IsHudPopupVisible()
	{
		return _settingsPanel.Visible || _settingsDetailsPanel.Visible || _requestPanel.Visible;
	}

	private void RefreshShopState()
	{
		var isShopOpen = _dayController is not null && _dayController.IsShopOpen;

		_serveCustomerButton.Text = isShopOpen ? "Shop Open" : "Start Day";
		_serveCustomerButton.Disabled = _dayController is null || isShopOpen;
		_gardenButton.Disabled = isShopOpen;
		if (GetTree().CurrentScene is Garden)
			_gardenButton.Disabled = true;
		_mapButton.Disabled = GetTree().CurrentScene is Map;
	}
}
