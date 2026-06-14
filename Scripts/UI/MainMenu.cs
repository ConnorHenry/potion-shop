using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class MainMenu : Control
{
	[Export] public NodePath StartButtonPath = default!;
	[Export] public NodePath NewGameButtonPath = default!;
	[Export] public NodePath LoadButtonPath = default!;
	[Export] public NodePath ExitToDesktopButtonPath = default!;
	[Export] public NodePath NewGameTutorialPopupPath = default!;
	[Export] public NodePath StartTutorialButtonPath = default!;
	[Export] public NodePath SkipTutorialButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);

	private Button _startButton = default!;
	private Button _newGameButton = default!;
	private Button _loadButton = default!;
	private Button _exitToDesktopButton = default!;
	private Control _newGameTutorialPopup = default!;
	private Button _startTutorialButton = default!;
	private Button _skipTutorialButton = default!;
	private SaveGameManager _saveGameManager = default!;

	public override void _Ready()
	{
		if (!NodeLookup.TryGetRequiredNode<SaveGameManager>(
			this,
			SaveGameManagerPath,
			nameof(MainMenu),
			nameof(SaveGameManagerPath),
			out _saveGameManager))
		{
			return;
		}

		if (!NodeLookup.TryGetRequiredNode<Button>(this, StartButtonPath, nameof(MainMenu), nameof(StartButtonPath), out _startButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, NewGameButtonPath, nameof(MainMenu), nameof(NewGameButtonPath), out _newGameButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, LoadButtonPath, nameof(MainMenu), nameof(LoadButtonPath), out _loadButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, ExitToDesktopButtonPath, nameof(MainMenu), nameof(ExitToDesktopButtonPath), out _exitToDesktopButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Control>(this, NewGameTutorialPopupPath, nameof(MainMenu), nameof(NewGameTutorialPopupPath), out _newGameTutorialPopup))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, StartTutorialButtonPath, nameof(MainMenu), nameof(StartTutorialButtonPath), out _startTutorialButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, SkipTutorialButtonPath, nameof(MainMenu), nameof(SkipTutorialButtonPath), out _skipTutorialButton))
			return;

		UpdateButtonLabels();
		UpdateContinueButtonVisibility();
		_newGameTutorialPopup.Visible = false;
		_startButton.Pressed += OnStartButtonPressed;
		_newGameButton.Pressed += OnNewGamePressed;
		_loadButton.Pressed += OnLoadButtonPressed;
		_exitToDesktopButton.Pressed += OnExitToDesktopPressed;
		_startTutorialButton.Pressed += OnStartTutorialPressed;
		_skipTutorialButton.Pressed += OnSkipTutorialPressed;
	}

	private void OnStartButtonPressed()
	{
		ContinueGame();
	}

	public override void _ExitTree()
	{
		if (_startButton is not null)
			_startButton.Pressed -= OnStartButtonPressed;
		if (_newGameButton is not null)
			_newGameButton.Pressed -= OnNewGamePressed;
		if (_loadButton is not null)
			_loadButton.Pressed -= OnLoadButtonPressed;
		if (_exitToDesktopButton is not null)
			_exitToDesktopButton.Pressed -= OnExitToDesktopPressed;
		if (_startTutorialButton is not null)
			_startTutorialButton.Pressed -= OnStartTutorialPressed;
		if (_skipTutorialButton is not null)
			_skipTutorialButton.Pressed -= OnSkipTutorialPressed;
	}

	private void OnNewGamePressed()
	{
		ShowNewGameTutorialPopup();
	}

	private void OnStartTutorialPressed()
	{
		StartNewGame(startTutorial: true);
	}

	private void OnSkipTutorialPressed()
	{
		StartNewGame(startTutorial: false);
	}

	private void OnLoadButtonPressed()
	{
		Error error = GetTree().ChangeSceneToFile(ScenePaths.LoadGameMenu);
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load save browser scene. Error: {error}");
		}
	}

	private void OnExitToDesktopPressed()
	{
		GetTree().Quit();
	}

	private void UpdateButtonLabels()
	{
		_startButton.Text = "Continue";
		_newGameButton.Text = "New Game";
		_loadButton.Text = "Load Game";
		_exitToDesktopButton.Text = "Exit to desktop";
	}

	private void UpdateContinueButtonVisibility()
	{
		_startButton.Visible = _saveGameManager.HasSavedGames();
	}

	private void ShowNewGameTutorialPopup()
	{
		_newGameTutorialPopup.Visible = true;
		_newGameTutorialPopup.MoveToFront();
		_startTutorialButton.GrabFocus();
	}

	private void StartNewGame(bool startTutorial)
	{
		_newGameTutorialPopup.Visible = false;
		_saveGameManager.StartNewGame(startTutorial);

		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

	private void ContinueGame()
	{
		if (!_saveGameManager.LoadLatestGameIfExists())
			_saveGameManager.StartNewGame();

		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

}
