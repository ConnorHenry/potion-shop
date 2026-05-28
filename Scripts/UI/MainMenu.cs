using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class MainMenu : Control
{
	[Export] public NodePath StartButtonPath = default!;
	[Export] public NodePath NewGameButtonPath = default!;
	[Export] public NodePath LoadButtonPath = default!;
	[Export] public NodePath NewGameTutorialPopupPath = default!;
	[Export] public NodePath StartTutorialButtonPath = default!;
	[Export] public NodePath SkipTutorialButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new("/root/SaveGameManager");

	private Button _startButton = default!;
	private Button _newGameButton = default!;
	private Button _loadButton = default!;
	private Control _newGameTutorialPopup = default!;
	private Button _startTutorialButton = default!;
	private Button _skipTutorialButton = default!;
	private SaveGameManager _saveGameManager = default!;

	public override void _Ready()
	{
		var saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (saveGameManager is null)
		{
			GD.PushError($"MainMenu: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return;
		}
		_saveGameManager = saveGameManager;

		if (!TryGetRequiredButton(StartButtonPath, nameof(StartButtonPath), out _startButton))
			return;
		if (!TryGetRequiredButton(NewGameButtonPath, nameof(NewGameButtonPath), out _newGameButton))
			return;
		if (!TryGetRequiredButton(LoadButtonPath, nameof(LoadButtonPath), out _loadButton))
			return;
		if (!TryGetRequiredControl(NewGameTutorialPopupPath, nameof(NewGameTutorialPopupPath), out _newGameTutorialPopup))
			return;
		if (!TryGetRequiredButton(StartTutorialButtonPath, nameof(StartTutorialButtonPath), out _startTutorialButton))
			return;
		if (!TryGetRequiredButton(SkipTutorialButtonPath, nameof(SkipTutorialButtonPath), out _skipTutorialButton))
			return;

		UpdateButtonLabels();
		UpdateContinueButtonVisibility();
		_newGameTutorialPopup.Visible = false;
		_startButton.Pressed += OnStartButtonPressed;
		_newGameButton.Pressed += OnNewGamePressed;
		_loadButton.Pressed += OnLoadButtonPressed;
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
		Error error = GetTree().ChangeSceneToFile("res://Scenes/UI/LoadGameMenu.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load save browser scene. Error: {error}");
		}
	}

	private void UpdateButtonLabels()
	{
		_startButton.Text = "Continue";
		_newGameButton.Text = "New Game";
		_loadButton.Text = "Load Game";
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

		Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

	private void ContinueGame()
	{
		if (!_saveGameManager.LoadLatestGameIfExists())
			_saveGameManager.StartNewGame();

		Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

	private bool TryGetRequiredButton(NodePath path, string exportName, out Button button)
	{
		button = default!;

		if (path.IsEmpty)
		{
			GD.PushError($"MainMenu: {exportName} is not assigned.");
			return false;
		}

		var resolvedButton = GetNodeOrNull<Button>(path);
		if (resolvedButton is null)
		{
			GD.PushError($"MainMenu: Button not found at '{path}'.");
			return false;
		}
		button = resolvedButton;

		return true;
	}

	private bool TryGetRequiredControl(NodePath path, string exportName, out Control control)
	{
		control = default!;

		if (path.IsEmpty)
		{
			GD.PushError($"MainMenu: {exportName} is not assigned.");
			return false;
		}

		var resolvedControl = GetNodeOrNull<Control>(path);
		if (resolvedControl is null)
		{
			GD.PushError($"MainMenu: Control not found at '{path}'.");
			return false;
		}
		control = resolvedControl;

		return true;
	}
}
