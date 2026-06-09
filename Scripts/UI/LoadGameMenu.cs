using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;
using OccultShop.Persistence;

namespace OccultShop.UI;

public partial class LoadGameMenu : Control
{
	[Export] public NodePath SaveListPath = default!;
	[Export] public NodePath EmptyStateLabelPath = default!;
	[Export] public NodePath BackButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);

	private VBoxContainer _saveList = default!;
	private Label _emptyStateLabel = default!;
	private Button _backButton = default!;
	private SaveGameManager _saveGameManager = default!;

	public override void _Ready()
	{
		if (!NodeLookup.TryGetRequiredNode<SaveGameManager>(
			this,
			SaveGameManagerPath,
			nameof(LoadGameMenu),
			nameof(SaveGameManagerPath),
			out _saveGameManager))
		{
			return;
		}

		if (!NodeLookup.TryGetRequiredNode<VBoxContainer>(this, SaveListPath, nameof(LoadGameMenu), nameof(SaveListPath), out _saveList))
			return;
		if (!NodeLookup.TryGetRequiredNode<Label>(this, EmptyStateLabelPath, nameof(LoadGameMenu), nameof(EmptyStateLabelPath), out _emptyStateLabel))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, BackButtonPath, nameof(LoadGameMenu), nameof(BackButtonPath), out _backButton))
			return;

		_backButton.Pressed += OnBackPressed;
		RefreshSaveList();
	}

	public override void _ExitTree()
	{
		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;
	}

	private void RefreshSaveList()
	{
		ClearChildren(_saveList);

		var savedGames = _saveGameManager.GetSavedGames();
		var hasSavedGames = savedGames.Count > 0;

		_saveList.Visible = hasSavedGames;
		_emptyStateLabel.Visible = !hasSavedGames;

		if (!hasSavedGames)
			return;

		foreach (var save in savedGames)
		{
			var saveEntry = save;
			_saveList.AddChild(CreateSaveRow(saveEntry));
		}
	}

	private Control CreateSaveRow(SaveGameSummary save)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 56)
		};

		var loadButton = new Button
		{
			Text = save.BuildDisplayText(),
			TooltipText = $"{save.FileName}\n{save.FilePath}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		loadButton.Pressed += () => OnSaveSelected(save);

		var deleteButton = new Button
		{
			Text = "Delete",
			CustomMinimumSize = new Vector2(96, 0)
		};
		deleteButton.Pressed += () => OnDeleteSaveSelected(save);

		row.AddChild(loadButton);
		row.AddChild(deleteButton);
		return row;
	}

	private void OnSaveSelected(SaveGameSummary save)
	{
		if (!_saveGameManager.LoadGame(save.FilePath))
		{
			GD.PushError($"LoadGameMenu: Failed to load save '{save.FileName}'.");
			RefreshSaveList();
			return;
		}

		var error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
		{
			GD.PushError($"LoadGameMenu: Failed to load main scene. Error: {error}");
		}
	}

	private void OnDeleteSaveSelected(SaveGameSummary save)
	{
		if (!_saveGameManager.DeleteSaveGame(save.FilePath))
		{
			GD.PushError($"LoadGameMenu: Failed to delete save '{save.FileName}'.");
			return;
		}

		RefreshSaveList();
	}

	private void OnBackPressed()
	{
		var error = GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
		if (error != Error.Ok)
		{
			GD.PushError($"LoadGameMenu: Failed to load main menu scene. Error: {error}");
		}
	}

	private static void ClearChildren(VBoxContainer container)
	{
		foreach (var child in container.GetChildren())
			child.QueueFree();
	}
}
