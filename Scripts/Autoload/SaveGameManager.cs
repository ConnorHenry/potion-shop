using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using OccultShop.Infrastructure;
using OccultShop.Models;
using OccultShop.Persistence;

namespace OccultShop.Autoload;

public partial class SaveGameManager : Node
{
	private const string SaveDirectoryPath = "user://saves";
	private const string SaveFilePrefix = "save_";
	private const int CurrentSaveVersion = 2;

	[Export] public NodePath GameStatePath { get; set; } = new(AutoloadNodePaths.GameState);
	[Export] public NodePath RuntimeContentDbPath { get; set; } = new(AutoloadNodePaths.RuntimeContentDb);

	private string? _activeSaveFilePath;
	private GameState _gameState = default!;
	private RuntimeContentDb _runtimeContentDb = default!;

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public override void _Ready()
	{
		if (!NodeLookup.TryGetRequiredNode<GameState>(
			this,
			GameStatePath,
			nameof(SaveGameManager),
			nameof(GameStatePath),
			out _gameState))
		{
			return;
		}

		if (!NodeLookup.TryGetRequiredNode<RuntimeContentDb>(
			this,
			RuntimeContentDbPath,
			nameof(SaveGameManager),
			nameof(RuntimeContentDbPath),
			out _runtimeContentDb))
		{
			return;
		}
	}

	public bool HasSaveFile()
	{
		return HasSavedGames();
	}

	public bool HasSavedGames()
	{
		return GetSavedGames().Count > 0;
	}

	public bool SaveGame()
	{
		try
		{
			EnsureSaveDirectoryExists();

			var saveData = new SaveFileData
			{
				Version = CurrentSaveVersion,
				SavedAtUtc = DateTime.UtcNow,
				GameState = _gameState.BuildSnapshot(),
				RuntimeItems = _runtimeContentDb.BuildRuntimeItemSnapshot()
			};

			var saveFilePath = string.IsNullOrWhiteSpace(_activeSaveFilePath)
				? BuildUniqueSaveFilePath(saveData.SavedAtUtc)
				: _activeSaveFilePath;

			var json = JsonSerializer.Serialize(saveData, JsonOpts);
			using var file = Godot.FileAccess.Open(saveFilePath, Godot.FileAccess.ModeFlags.Write);
			if (file is null)
			{
				GD.PushError($"SaveGameManager: Could not open save file for write. Error: {Godot.FileAccess.GetOpenError()}");
				return false;
			}

			file.StoreString(json);
			_activeSaveFilePath = saveFilePath;
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"SaveGameManager: Save failed. {ex.Message}");
			return false;
		}
	}

	public bool LoadGameIfExists()
	{
		return LoadLatestGameIfExists();
	}

	public bool LoadLatestGameIfExists()
	{
		var latestSave = GetSavedGames().FirstOrDefault();
		if (latestSave is null)
			return false;

		return LoadGame(latestSave.FilePath);
	}

	public IReadOnlyList<SaveGameSummary> GetSavedGames()
	{
		EnsureSaveDirectoryExists();

		var saveDirectory = GetSaveDirectoryAbsolutePath();
		if (!Directory.Exists(saveDirectory))
			return Array.Empty<SaveGameSummary>();

		var saveFiles = Directory.GetFiles(saveDirectory, "*.json", SearchOption.TopDirectoryOnly);
		var saves = new List<SaveGameSummary>(saveFiles.Length);

		foreach (var absolutePath in saveFiles)
		{
			if (!TryBuildSaveSummary(absolutePath, out var summary))
				continue;

			saves.Add(summary);
		}

		return saves
			.OrderByDescending(x => x.SavedAtUtc)
			.ThenByDescending(x => x.FileName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public bool LoadGame(string saveFilePath)
	{
		if (string.IsNullOrWhiteSpace(saveFilePath))
		{
			GD.PushError("SaveGameManager: Save file path is missing.");
			return false;
		}

		if (!IsSavePathAllowed(saveFilePath))
		{
			GD.PushError($"SaveGameManager: Save path '{saveFilePath}' is outside the save directory.");
			return false;
		}

		if (!TryReadSaveData(saveFilePath, out var saveData))
			return false;

		if (saveData.Version > CurrentSaveVersion)
		{
			GD.PushError($"SaveGameManager: Save version {saveData.Version} is newer than supported version {CurrentSaveVersion}.");
			return false;
		}

		_runtimeContentDb.RestoreRuntimeItems(saveData.RuntimeItems);
		_gameState.ApplySnapshot(saveData.GameState);
		_activeSaveFilePath = saveFilePath;
		return true;
	}

	public bool DeleteSaveGame(string saveFilePath)
	{
		if (string.IsNullOrWhiteSpace(saveFilePath))
		{
			GD.PushError("SaveGameManager: Save file path is missing.");
			return false;
		}

		if (!IsSavePathAllowed(saveFilePath))
		{
			GD.PushError($"SaveGameManager: Save path '{saveFilePath}' is outside the save directory.");
			return false;
		}

		if (!Godot.FileAccess.FileExists(saveFilePath))
			return false;

		var absolutePath = ProjectSettings.GlobalizePath(saveFilePath);
		var result = DirAccess.RemoveAbsolute(absolutePath);
		if (result != Error.Ok)
		{
			GD.PushError($"SaveGameManager: Failed to delete save '{saveFilePath}'. Error: {result}");
			return false;
		}

		if (string.Equals(_activeSaveFilePath, saveFilePath, StringComparison.OrdinalIgnoreCase))
			_activeSaveFilePath = null;

		return true;
	}

	public void StartNewGame()
	{
		StartNewGameWithTutorialState(tutorialRequested: false, tutorialSkipped: false);
	}

	public void StartNewGame(bool startTutorial)
	{
		StartNewGameWithTutorialState(tutorialRequested: startTutorial, tutorialSkipped: !startTutorial);
	}

	private void StartNewGameWithTutorialState(bool tutorialRequested, bool tutorialSkipped)
	{
		_activeSaveFilePath = null;
		_runtimeContentDb.ClearRuntimeItems();
		_gameState.ResetForNewGame();

		if (tutorialRequested)
		{
			_gameState.RequestTutorial();
			return;
		}

		if (tutorialSkipped)
			_gameState.SkipTutorial();
	}

	private bool TryReadSaveData(string saveFilePath, out SaveFileData saveData)
	{
		saveData = new SaveFileData();

		try
		{
			using var file = Godot.FileAccess.Open(saveFilePath, Godot.FileAccess.ModeFlags.Read);
			if (file is null)
			{
				GD.PushError($"SaveGameManager: Could not open save file for read. Error: {Godot.FileAccess.GetOpenError()}");
				return false;
			}

			var json = file.GetAsText();
			if (string.IsNullOrWhiteSpace(json))
			{
				GD.PushError("SaveGameManager: Save file is empty.");
				return false;
			}

			var parsed = JsonSerializer.Deserialize<SaveFileData>(json, JsonOpts);
			if (parsed is null)
			{
				GD.PushError("SaveGameManager: Save JSON could not be parsed.");
				return false;
			}

			saveData = NormalizeSaveData(parsed);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"SaveGameManager: Load failed. {ex.Message}");
			return false;
		}
	}

	private bool TryBuildSaveSummary(string absolutePath, out SaveGameSummary summary)
	{
		summary = new SaveGameSummary();
		var userPath = ToUserSavePath(absolutePath);

		if (!TryReadSaveData(userPath, out var saveData))
			return false;

		summary.FilePath = userPath;
		summary.FileName = Path.GetFileName(absolutePath);
		summary.SavedAtUtc = saveData.SavedAtUtc;
		summary.Day = saveData.GameState.Day;
		summary.Gold = saveData.GameState.Gold;
		summary.Dread = saveData.GameState.Dread;
		return true;
	}

	private static SaveFileData NormalizeSaveData(SaveFileData parsed)
	{
		parsed.GameState ??= new GameStateSnapshot();
		parsed.RuntimeItems ??= new List<ItemDef>();
		return parsed;
	}

	private string BuildUniqueSaveFilePath(DateTime savedAtUtc)
	{
		var timestamp = savedAtUtc.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture);
		var baseFileName = $"{SaveFilePrefix}{timestamp}";
		var candidate = $"{SaveDirectoryPath}/{baseFileName}.json";
		var suffix = 1;

		while (Godot.FileAccess.FileExists(candidate))
		{
			candidate = $"{SaveDirectoryPath}/{baseFileName}_{suffix}.json";
			suffix++;
		}

		return candidate;
	}

	private void EnsureSaveDirectoryExists()
	{
		Directory.CreateDirectory(GetSaveDirectoryAbsolutePath());
	}

	private string GetSaveDirectoryAbsolutePath()
	{
		return ProjectSettings.GlobalizePath(SaveDirectoryPath);
	}

	private static string ToUserSavePath(string absolutePath)
	{
		var fileName = Path.GetFileName(absolutePath);
		return $"{SaveDirectoryPath}/{fileName}";
	}

	private bool IsSavePathAllowed(string saveFilePath)
	{
		return saveFilePath.StartsWith(SaveDirectoryPath + "/", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(saveFilePath, SaveDirectoryPath, StringComparison.OrdinalIgnoreCase);
	}

}
