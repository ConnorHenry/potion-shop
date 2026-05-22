using Godot;
using OccultShop.Autoload;

public partial class MainMenu : Control
{
	[Export]
	public NodePath StartButtonPath { get; set; } = new NodePath("");
	[Export]
	public NodePath NewGameButtonPath { get; set; } = new NodePath("");
	[Export]
	public NodePath LoadButtonPath { get; set; } = new NodePath("");

	private Button? _startButton;
	private Button? _newGameButton;
	private Button? _loadButton;

	public override void _Ready()
	{
		if (StartButtonPath.IsEmpty)
		{
			GD.PushError("MainMenu: StartButtonPath is not assigned.");
			return;
		}

		_startButton = GetNodeOrNull<Button>(StartButtonPath);
		if (_startButton == null)
		{
			GD.PushError($"MainMenu: Start button not found at path '{StartButtonPath}'.");
			return;
		}

		if (NewGameButtonPath.IsEmpty)
		{
			GD.PushError("MainMenu: NewGameButtonPath is not assigned.");
			return;
		}

		_newGameButton = GetNodeOrNull<Button>(NewGameButtonPath);
		if (_newGameButton == null)
		{
			GD.PushError($"MainMenu: New Game button not found at path '{NewGameButtonPath}'.");
			return;
		}

		if (LoadButtonPath.IsEmpty)
		{
			GD.PushError("MainMenu: LoadButtonPath is not assigned.");
			return;
		}

		_loadButton = GetNodeOrNull<Button>(LoadButtonPath);
		if (_loadButton == null)
		{
			GD.PushError($"MainMenu: Load button not found at path '{LoadButtonPath}'.");
			return;
		}

		UpdateButtonLabels();
		UpdateContinueButtonVisibility();
		_startButton.Pressed += OnStartButtonPressed;
		_newGameButton.Pressed += OnNewGamePressed;
		_loadButton.Pressed += OnLoadButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		ContinueGame();
	}

	public override void _ExitTree()
	{
		if (_startButton != null)
			_startButton.Pressed -= OnStartButtonPressed;
		if (_newGameButton != null)
			_newGameButton.Pressed -= OnNewGamePressed;
		if (_loadButton != null)
			_loadButton.Pressed -= OnLoadButtonPressed;
	}

	private void OnNewGamePressed()
	{
		StartNewGame();
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
		if (_startButton is not null)
			_startButton.Text = "Continue";

		if (_newGameButton is not null)
			_newGameButton.Text = "New Game";

		if (_loadButton is not null)
			_loadButton.Text = "Load Game";
	}

	private void UpdateContinueButtonVisibility()
	{
		if (_startButton is null)
			return;

		_startButton.Visible = SaveGameManager.HasSavedGames();
	}

	private void StartNewGame()
	{
		SaveGameManager.StartNewGame();

		Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

	private void ContinueGame()
	{
		if (!SaveGameManager.LoadLatestGameIfExists())
			SaveGameManager.StartNewGame();

		Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}

	private static SaveGameManager SaveGameManager => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<SaveGameManager>("/root/SaveGameManager");
}
