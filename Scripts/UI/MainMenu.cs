using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class MainMenu : Control
{
	[Export] public NodePath StartButtonPath = default!;
	[Export] public NodePath NewGameButtonPath = default!;
	[Export] public NodePath LoadButtonPath = default!;

	private Button _startButton = default!;
	private Button _newGameButton = default!;
	private Button _loadButton = default!;
	private SaveGameManager _saveGameManager = default!;

	public override void _Ready()
	{
		var saveGameManager = GetNodeOrNull<SaveGameManager>("/root/SaveGameManager");
		if (saveGameManager is null)
		{
			GD.PushError("MainMenu: /root/SaveGameManager was not found.");
			return;
		}
		_saveGameManager = saveGameManager;

		if (!TryGetRequiredButton(StartButtonPath, nameof(StartButtonPath), out _startButton))
			return;
		if (!TryGetRequiredButton(NewGameButtonPath, nameof(NewGameButtonPath), out _newGameButton))
			return;
		if (!TryGetRequiredButton(LoadButtonPath, nameof(LoadButtonPath), out _loadButton))
			return;

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
		if (_startButton is not null)
			_startButton.Pressed -= OnStartButtonPressed;
		if (_newGameButton is not null)
			_newGameButton.Pressed -= OnNewGamePressed;
		if (_loadButton is not null)
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
		_startButton.Text = "Continue";
		_newGameButton.Text = "New Game";
		_loadButton.Text = "Load Game";
	}

	private void UpdateContinueButtonVisibility()
	{
		_startButton.Visible = _saveGameManager.HasSavedGames();
	}

	private void StartNewGame()
	{
		_saveGameManager.StartNewGame();

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
}
