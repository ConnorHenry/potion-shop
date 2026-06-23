using Godot;
using ImGuiGodot;
using OccultShop.Autoload;
using OccultShop.Controllers;
using OccultShop.Infrastructure;
using OccultShop.Systems;

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
	private const string MusicEnabledKey = "music_enabled";
	private const string MusicVolumeKey = "music_volume";
	private const double MusicFadeSeconds = 5.0;
	private const float SilentMusicVolumeDb = -80.0f;
	private const bool DefaultAmbientSoundsEnabled = true;
	private const double DefaultRainfallVolume = 0.7;
	private const bool DefaultMusicEnabled = true;
	private const double DefaultMusicVolume = 0.55;
	private static readonly string[] SoundtrackAudioPaths =
	[
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/01_moonera_and_lucie_cravero_cliffs_of_eire.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/02_fugee_the_troublemakers.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/03_bogar_waking_the_strings.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/04_innigma_and_courtney_pinski_irish_sunset.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/05_hotpotatoes_and_scott_munro_ode_to_a_broken_mandolin.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/06_moonera_and_folkturia_fianna.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/07_stabilisers_and_hoxde_where_lambs_once_grazed.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/08_c4c_and_nathaniel_e_young_patrick_s_story.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/09_detuned_cafe_the_road_back_home.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/10_alpacca_an_old_story_told.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/11_atamatoki_moonera_and_courtney_pinski_selkie_s_song.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/12_pueblo_vista_whispers_of_a_home_barely_forgotten.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/13_eva_gomi_tenshi_and_prithvi_a_long_expected_party.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/14_moonera_and_joshua_hoe_where_we_began.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/15_atamatoki_and_early_garden_lash_the_kettle_on.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/16_stabilisers_and_myceliumbug_green_hills_of_home.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/17_odem_medo_honeycomb.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/18_weviis_and_atamatoki_river_shannon.mp3",
		"res://Assets/Audio/Music/postcards_from_ireland_celtic_lofi_beats/19_moonera_outlander.mp3",
	];

	private enum MusicFadeState
	{
		None,
		FadeIn,
		FadeOut
	}

	[Export] public NodePath GoldLabelPath = new("Content/Status/Gold");
	[Export] public NodePath DateButtonPath = new("Content/Status/Day");
	[Export] public NodePath DayCounterLabelPath = new("Content/Status/DayCounter");
	[Export] public NodePath CalendarPanelPath = new("CalendarPanel");
	[Export] public NodePath StartDayButtonPath = new("Content/Actions/ServeCustomer");
	[Export] public NodePath GardenButtonPath = new("Content/Actions/Garden");
	[Export] public NodePath MapButtonPath = new("Content/Actions/Map");
	[Export] public NodePath SettingsButtonPath = new("Content/Actions/MainMenu");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public NodePath DayControllerPath = new("DayController");

	private Label _gold = default!;
	private Button _dateButton = default!;
	private Label _dayCounter = default!;
	private CalendarPanel _calendarPanel = default!;
	private Button _serveCustomerButton = default!;
	private Button _gardenButton = default!;
	private Button _mapButton = default!;
	private Button _settingsButton = default!;
	private Button _nextTrackButton = default!;
	private Button _returnToMainMenuButton = default!;
	private Button _saveGameButton = default!;
	private Button _openSettingsButton = default!;
	private Button _toggleDebugPanelButton = default!;
	private CheckBox _ambientSoundsToggle = default!;
	private HSlider _rainfallVolumeSlider = default!;
	private AudioStreamPlayer _ambientRainPlayer = default!;
	private CheckBox _musicToggle = default!;
	private HSlider _musicVolumeSlider = default!;
	private AudioStreamPlayer _musicPlayer = default!;
	private Godot.Timer _musicFadeOutTimer = default!;
	private GameState? _gameState;
	private SaveGameManager? _saveGameManager;
	private DayController? _dayController;
	private Control _settingsPanel = default!;
	private Control _settingsDetailsPanel = default!;
	private bool _isSavingGame;
	private bool _ambientSoundsEnabled = DefaultAmbientSoundsEnabled;
	private bool _ambientPlaybackAllowed;
	private double _rainfallVolume = DefaultRainfallVolume;
	private bool _musicEnabled = DefaultMusicEnabled;
	private double _musicVolume = DefaultMusicVolume;
	private readonly Random _soundtrackRandom = new();
	private int[] _soundtrackOrder = [];
	private int _soundtrackOrderIndex;
	private Tween? _musicFadeTween;
	private MusicFadeState _musicFadeState = MusicFadeState.None;

	public override void _Ready()
	{
		_gold = GetNode<Label>(GoldLabelPath);
		_dateButton = GetNode<Button>(DateButtonPath);
		_dayCounter = GetNode<Label>(DayCounterLabelPath);
		_calendarPanel = GetNode<CalendarPanel>(CalendarPanelPath);

		_serveCustomerButton = GetNode<Button>(StartDayButtonPath);
		_gardenButton = GetNode<Button>(GardenButtonPath);
		_mapButton = GetNode<Button>(MapButtonPath);
		_settingsButton = GetNode<Button>(SettingsButtonPath);
		_nextTrackButton = GetNode<Button>("Content/Actions/NextTrack");
		_returnToMainMenuButton = GetNode<Button>("SettingsPanel/Margin/VBox/ReturnToMainMenu");
		_saveGameButton = GetNode<Button>("SettingsPanel/Margin/VBox/SaveGame");
		_openSettingsButton = GetNode<Button>("SettingsPanel/Margin/VBox/OpenSettings");
		_toggleDebugPanelButton = GetNode<Button>("SettingsPanel/Margin/VBox/ToggleDebugPanel");
		_settingsPanel = GetNode<Control>("SettingsPanel");
		_settingsDetailsPanel = GetNode<Control>("Settings");
		_ambientSoundsToggle = GetNode<CheckBox>("Settings/Margin/VBox/AmbientSounds");
		_rainfallVolumeSlider = GetNode<HSlider>("Settings/Margin/VBox/RainfallVolumeRow/RainfallVolume");
		_ambientRainPlayer = GetNode<AudioStreamPlayer>("AmbientRainPlayer");
		_musicToggle = GetNode<CheckBox>("Settings/Margin/VBox/Music");
		_musicVolumeSlider = GetNode<HSlider>("Settings/Margin/VBox/MusicVolumeRow/MusicVolume");
		_musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");
		_musicFadeOutTimer = GetNode<Godot.Timer>("MusicFadeOutTimer");

		_gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (_gameState is null)
			GD.PushError($"Hud: GameState was not found at '{GameStatePath}'.");

		_saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (_saveGameManager is null)
			GD.PushError($"Hud: SaveGameManager was not found at '{SaveGameManagerPath}'.");

		_settingsPanel.ZIndex = SettingsPanelZIndex;
		_settingsDetailsPanel.ZIndex = SettingsDetailsPanelZIndex;
		_calendarPanel.ZIndex = SettingsPanelZIndex;
		LoadAudioSettings();
		ConfigureAmbientRainPlayer();
		ConfigureSoundtrackPlayer();
		ApplyAudioSettingsToControls();
		SetProcessInput(true);

		_serveCustomerButton.Pressed += OnStartDayPressed;
		_dateButton.Pressed += OnDatePressed;
		_gardenButton.Pressed += OnGardenPressed;
		_mapButton.Pressed += OnMapPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_nextTrackButton.Pressed += OnNextTrackPressed;
		_returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
		_saveGameButton.Pressed += OnSaveGamePressed;
		_openSettingsButton.Pressed += OnOpenSettingsPressed;
		_toggleDebugPanelButton.Pressed += OnToggleDebugPanelPressed;
		_ambientSoundsToggle.Toggled += OnAmbientSoundsToggled;
		_rainfallVolumeSlider.ValueChanged += OnRainfallVolumeChanged;
		_musicToggle.Toggled += OnMusicToggled;
		_musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
		_musicPlayer.Finished += OnMusicFinished;
		_musicFadeOutTimer.Timeout += OnMusicFadeOutTimerTimeout;

		if (_gameState is not null)
			_gameState.Changed += Refresh;

		_toggleDebugPanelButton.Text = ImGuiGD.Visible ? "Debug Panel: On" : "Debug Panel: Off";
		_calendarPanel.HidePanel();
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
		if (_dateButton is not null)
			_dateButton.Pressed -= OnDatePressed;
		if (_gardenButton is not null)
			_gardenButton.Pressed -= OnGardenPressed;
		if (_mapButton is not null)
			_mapButton.Pressed -= OnMapPressed;
		if (_settingsButton is not null)
			_settingsButton.Pressed -= OnSettingsPressed;
		if (_nextTrackButton is not null)
			_nextTrackButton.Pressed -= OnNextTrackPressed;
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
		if (_musicToggle is not null)
			_musicToggle.Toggled -= OnMusicToggled;
		if (_musicVolumeSlider is not null)
			_musicVolumeSlider.ValueChanged -= OnMusicVolumeChanged;
		if (_musicPlayer is not null)
			_musicPlayer.Finished -= OnMusicFinished;
		if (_musicFadeOutTimer is not null)
			_musicFadeOutTimer.Timeout -= OnMusicFadeOutTimerTimeout;
		_musicFadeTween?.Kill();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsHudPopupVisible())
			return;

		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
			{
				AcceptEvent();
				HideHudPopups();
			}

			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed)
			return;

		if (IsPointInsideVisibleControl(_settingsPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_settingsDetailsPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_calendarPanel, mouseButton.GlobalPosition)
			|| IsPointInsideVisibleControl(_dateButton, mouseButton.GlobalPosition))
			return;

		AcceptEvent();
		HideHudPopups();
	}

	public void RefreshSceneBindings()
	{
		DisconnectSceneBindings();
		if (_calendarPanel is not null)
			_calendarPanel.HidePanel();

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
		HideHudPopups();
	}

	public void SetAmbientPlaybackAllowed(bool allowed)
	{
		var soundtrackShouldRestart = allowed && !_ambientPlaybackAllowed;
		_ambientPlaybackAllowed = allowed;
		if (soundtrackShouldRestart)
			StartNewSoundtrackCycle();

		RefreshAmbientRainPlayback();
		RefreshSoundtrackPlayback();
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
			_dayCounter.Text = $"Day {_gameState.Day}";
			_dateButton.Text = GameCalendar.ToDate(_gameState.Day).ToHudText();
			_calendarPanel.Refresh();
		}

		RefreshShopState();
	}

	private void OnStartDayPressed()
	{
		if (IsSceneNavigationBlocked())
			return;
		if (_dayController is null)
			return;

		_dayController.StartShopDay();
	}

	private void OnDatePressed()
	{
		if (IsSceneNavigationBlocked())
			return;

		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(false);
		_calendarPanel.TogglePanel();
	}

	private void OnGardenPressed()
	{
		if (IsSceneNavigationBlocked())
			return;
		if (_dayController is not null && _dayController.IsShopOpen)
			return;
		if (_gameState is null || !_gameState.IsGardenUnlocked)
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
		if (IsSceneNavigationBlocked())
			return;
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
		if (IsSceneNavigationBlocked())
			return;

		var shouldOpen = !_settingsPanel.Visible;
		SetSettingsPanelVisible(shouldOpen);
		if (shouldOpen)
		{
			_calendarPanel.HidePanel();
			SetSettingsDetailsVisible(false);
		}
	}

	private void OnOpenSettingsPressed()
	{
		SetSettingsPanelVisible(false);
		SetSettingsDetailsVisible(true);
	}

	private void OnReturnToMainMenuPressed()
	{
		if (IsSceneNavigationBlocked())
			return;

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
		SaveAudioSettings();
		RefreshAmbientRainPlayback();
	}

	private void OnRainfallVolumeChanged(double value)
	{
		_rainfallVolume = ClampNormalizedVolume(value);
		SaveAudioSettings();
		RefreshAmbientRainPlayback();
	}

	private void OnMusicToggled(bool enabled)
	{
		_musicEnabled = enabled;
		SaveAudioSettings();
		RefreshSoundtrackPlayback();
	}

	private void OnMusicVolumeChanged(double value)
	{
		_musicVolume = ClampNormalizedVolume(value);
		SaveAudioSettings();
		RefreshSoundtrackVolume();
	}

	private void OnMusicFinished()
	{
		if (ShouldPlaySoundtrack())
			PlayNextSoundtrackTrack();
	}

	private void OnMusicFadeOutTimerTimeout()
	{
		if (!ShouldPlaySoundtrack() || !_musicPlayer.Playing)
			return;

		StartMusicFade(MusicFadeState.FadeOut, SilentMusicVolumeDb, MusicFadeSeconds);
	}

	private void OnNextTrackPressed()
	{
		if (!ShouldPlaySoundtrack())
			return;

		PlayNextSoundtrackTrack();
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

	private void HideHudPopups()
	{
		if (_settingsPanel is not null)
			SetSettingsPanelVisible(false);
		if (_settingsDetailsPanel is not null)
			SetSettingsDetailsVisible(false);
		if (_calendarPanel is not null)
			_calendarPanel.HidePanel();
	}

	private void LoadAudioSettings()
	{
		var config = new ConfigFile();
		Error error = config.Load(AmbientSettingsPath);
		if (error == Error.FileNotFound)
			return;
		if (error != Error.Ok)
		{
			GD.PushError($"Hud: Failed to load audio settings. Error: {error}");
			return;
		}

		_ambientSoundsEnabled = config
			.GetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, DefaultAmbientSoundsEnabled)
			.AsBool();
		_rainfallVolume = ClampNormalizedVolume(config
			.GetValue(AmbientSettingsSection, RainfallVolumeKey, DefaultRainfallVolume)
			.AsDouble());
		_musicEnabled = config
			.GetValue(AmbientSettingsSection, MusicEnabledKey, DefaultMusicEnabled)
			.AsBool();
		_musicVolume = ClampNormalizedVolume(config
			.GetValue(AmbientSettingsSection, MusicVolumeKey, DefaultMusicVolume)
			.AsDouble());
	}

	private void SaveAudioSettings()
	{
		var config = new ConfigFile();
		config.SetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, _ambientSoundsEnabled);
		config.SetValue(AmbientSettingsSection, RainfallVolumeKey, _rainfallVolume);
		config.SetValue(AmbientSettingsSection, MusicEnabledKey, _musicEnabled);
		config.SetValue(AmbientSettingsSection, MusicVolumeKey, _musicVolume);

		Error error = config.Save(AmbientSettingsPath);
		if (error != Error.Ok)
			GD.PushError($"Hud: Failed to save audio settings. Error: {error}");
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

	private void ConfigureSoundtrackPlayer()
	{
		StartNewSoundtrackCycle();
		if (_soundtrackOrder.Length == 0)
		{
			GD.PushError("Hud: No soundtrack tracks are configured.");
			UpdateNextTrackButtonState();
			return;
		}

		UpdateNextTrackButtonState();
	}

	private void ApplyAudioSettingsToControls()
	{
		_ambientSoundsToggle.ButtonPressed = _ambientSoundsEnabled;
		_rainfallVolumeSlider.Value = _rainfallVolume;
		_musicToggle.ButtonPressed = _musicEnabled;
		_musicVolumeSlider.Value = _musicVolume;
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

	private void RefreshSoundtrackPlayback()
	{
		if (_musicPlayer is null)
			return;

		UpdateNextTrackButtonState();
		if (ShouldPlaySoundtrack())
		{
			if (_musicPlayer.Stream is null && !TryLoadCurrentSoundtrackTrack())
				return;
			if (!_musicPlayer.Playing)
				StartCurrentSoundtrackTrack();
			else
				RefreshSoundtrackVolume();
			return;
		}

		StopSoundtrackPlayback();
	}

	private bool ShouldPlaySoundtrack()
	{
		return _ambientPlaybackAllowed
			&& _musicEnabled
			&& _musicPlayer is not null
			&& _soundtrackOrder.Length > 0;
	}

	private void PlayNextSoundtrackTrack()
	{
		if (_soundtrackOrder.Length == 0)
			return;

		var previousTrackIndex = _soundtrackOrderIndex >= 0 && _soundtrackOrderIndex < _soundtrackOrder.Length
			? _soundtrackOrder[_soundtrackOrderIndex]
			: -1;

		_soundtrackOrderIndex += 1;
		if (_soundtrackOrderIndex >= _soundtrackOrder.Length)
			StartNewSoundtrackCycle(previousTrackIndex);

		if (!TryLoadCurrentSoundtrackTrack())
			return;

		StartCurrentSoundtrackTrack();
	}

	private bool TryLoadCurrentSoundtrackTrack()
	{
		if (_soundtrackOrderIndex < 0 || _soundtrackOrderIndex >= _soundtrackOrder.Length)
			_soundtrackOrderIndex = 0;

		return TryLoadSoundtrackTrack(_soundtrackOrder[_soundtrackOrderIndex]);
	}

	private void StartCurrentSoundtrackTrack()
	{
		if (_musicPlayer.Stream is null)
			return;

		CancelMusicFade();
		_musicPlayer.VolumeDb = SilentMusicVolumeDb;
		_musicPlayer.Play();
		StartMusicFade(MusicFadeState.FadeIn, GetMusicVolumeDb(), MusicFadeSeconds);
		ScheduleMusicFadeOut();
	}

	private void StopSoundtrackPlayback()
	{
		CancelMusicFade();
		if (_musicPlayer.Playing)
			_musicPlayer.Stop();
		_musicPlayer.VolumeDb = SilentMusicVolumeDb;
	}

	private void RefreshSoundtrackVolume()
	{
		if (_musicPlayer is null)
			return;

		if (_musicFadeState == MusicFadeState.FadeIn)
		{
			StartMusicFade(MusicFadeState.FadeIn, GetMusicVolumeDb(), MusicFadeSeconds);
			return;
		}

		if (_musicFadeState == MusicFadeState.FadeOut)
			return;

		_musicPlayer.VolumeDb = _musicPlayer.Playing
			? GetMusicVolumeDb()
			: SilentMusicVolumeDb;
	}

	private void ScheduleMusicFadeOut()
	{
		if (_musicFadeOutTimer is null || _musicPlayer.Stream is null)
			return;

		var trackLengthSeconds = _musicPlayer.Stream.GetLength();
		if (trackLengthSeconds <= MusicFadeSeconds)
			return;

		_musicFadeOutTimer.WaitTime = trackLengthSeconds - MusicFadeSeconds;
		_musicFadeOutTimer.Start();
	}

	private void StartMusicFade(MusicFadeState fadeState, float targetVolumeDb, double durationSeconds)
	{
		_musicFadeTween?.Kill();
		_musicFadeState = fadeState;
		_musicFadeTween = CreateTween();
		_musicFadeTween.SetTrans(Tween.TransitionType.Sine);
		_musicFadeTween.SetEase(fadeState == MusicFadeState.FadeOut
			? Tween.EaseType.In
			: Tween.EaseType.Out);
		_musicFadeTween.TweenProperty(_musicPlayer, "volume_db", targetVolumeDb, durationSeconds);
		_musicFadeTween.Finished += OnMusicFadeFinished;
	}

	private void OnMusicFadeFinished()
	{
		_musicFadeTween = null;
		if (_musicFadeState == MusicFadeState.FadeIn)
			_musicFadeState = MusicFadeState.None;
	}

	private void CancelMusicFade()
	{
		_musicFadeOutTimer?.Stop();
		_musicFadeTween?.Kill();
		_musicFadeTween = null;
		_musicFadeState = MusicFadeState.None;
	}

	private bool TryLoadSoundtrackTrack(int trackIndex)
	{
		if (trackIndex < 0 || trackIndex >= SoundtrackAudioPaths.Length)
		{
			GD.PushError($"Hud: Soundtrack track index '{trackIndex}' is out of range.");
			return false;
		}

		var path = SoundtrackAudioPaths[trackIndex];
		var stream = ResourceLoader.Load<AudioStream>(path);
		if (stream is null)
		{
			GD.PushError($"Hud: Soundtrack audio stream could not be loaded from '{path}'.");
			return false;
		}

		_musicPlayer.Stream = stream;
		_musicPlayer.VolumeDb = GetMusicVolumeDb();
		return true;
	}

	private void StartNewSoundtrackCycle(int previousTrackIndex = -1)
	{
		BuildShuffledSoundtrackOrder(previousTrackIndex);
		if (_soundtrackOrder.Length == 0)
			return;

		_soundtrackOrderIndex = 0;
		TryLoadCurrentSoundtrackTrack();
		_musicPlayer.VolumeDb = SilentMusicVolumeDb;
	}

	private void BuildShuffledSoundtrackOrder(int previousTrackIndex = -1)
	{
		_soundtrackOrder = new int[SoundtrackAudioPaths.Length];
		for (var index = 0; index < _soundtrackOrder.Length; index += 1)
			_soundtrackOrder[index] = index;

		for (var index = 0; index < _soundtrackOrder.Length - 1; index += 1)
		{
			var swapIndex = _soundtrackRandom.Next(index, _soundtrackOrder.Length);
			(_soundtrackOrder[index], _soundtrackOrder[swapIndex]) = (_soundtrackOrder[swapIndex], _soundtrackOrder[index]);
		}

		if (_soundtrackOrder.Length <= 1 || _soundtrackOrder[0] != previousTrackIndex)
			return;

		(_soundtrackOrder[0], _soundtrackOrder[1]) = (_soundtrackOrder[1], _soundtrackOrder[0]);
	}

	private void UpdateNextTrackButtonState()
	{
		if (_nextTrackButton is null)
			return;

		_nextTrackButton.Disabled = !_musicEnabled || _soundtrackOrder.Length <= 1;
	}

	private float GetRainfallVolumeDb()
	{
		return _rainfallVolume <= 0.0001
			? -80.0f
			: Mathf.LinearToDb((float)_rainfallVolume);
	}

	private float GetMusicVolumeDb()
	{
		return _musicVolume <= 0.0001
			? -80.0f
			: Mathf.LinearToDb((float)_musicVolume);
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
		return _settingsPanel.Visible
			|| _settingsDetailsPanel.Visible
			|| _calendarPanel.Visible;
	}

	private bool IsSceneNavigationBlocked()
	{
		return GetTree().CurrentScene is ForestGathering or JuniperGathering;
	}

	private void RefreshShopState()
	{
		var isShopOpen = _dayController is not null && _dayController.IsShopOpen;
		var navigationBlocked = IsSceneNavigationBlocked();
		var gardenUnlocked = _gameState is not null && _gameState.IsGardenUnlocked;
		if (navigationBlocked)
		{
			HideHudPopups();
		}

		_serveCustomerButton.Text = isShopOpen ? "Shop Open" : "Start Day";
		_serveCustomerButton.Disabled = navigationBlocked || _dayController is null || isShopOpen;
		_gardenButton.Disabled = navigationBlocked || isShopOpen || !gardenUnlocked;
		if (GetTree().CurrentScene is Garden)
			_gardenButton.Disabled = true;
		_mapButton.Disabled = navigationBlocked || GetTree().CurrentScene is Map;
		_settingsButton.Disabled = navigationBlocked;
	}
}
