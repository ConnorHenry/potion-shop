using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class Map : Control
{
	[Export] public NodePath BackButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);

	private Button _backButton = default!;
	private SaveGameManager? _saveGameManager;

	public override void _Ready()
	{
		_backButton = GetNode<Button>(BackButtonPath);
		_backButton.Pressed += OnBackPressed;

		_saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (_saveGameManager is null)
		{
			GD.PushError($"Map: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return;
		}

		TryAutoSave("entering the map");
	}

	public override void _ExitTree()
	{
		if (_backButton is not null)
			_backButton.Pressed -= OnBackPressed;
	}

	private void OnBackPressed()
	{
		TryAutoSave("leaving the map");
		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
		{
			GD.PushError($"Map: Failed to load main scene. Error: {error}");
		}
	}

	private bool TryAutoSave(string context)
	{
		if (_saveGameManager is null)
		{
			GD.PushError($"Map: Cannot auto-save while {context} because SaveGameManager is missing.");
			return false;
		}

		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"Map: Auto-save failed while {context}.");

		return saveSucceeded;
	}
}
