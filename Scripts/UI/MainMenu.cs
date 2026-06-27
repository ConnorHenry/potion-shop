using System;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class MainMenu : Control
{
	private const int PlayerNameMaxLength = 20;
	private const string PlayerNamePreviewEmptyText = "Your name: ...";

	private static readonly string[] KeyboardRows =
	{
		"QWERTYUIOP",
		"ASDFGHJKL",
		"ZXCVBNM"
	};

	[Export] public NodePath StartButtonPath = default!;
	[Export] public NodePath NewGameButtonPath = default!;
	[Export] public NodePath LoadButtonPath = default!;
	[Export] public NodePath ExitToDesktopButtonPath = default!;
	[Export] public NodePath NewGameTutorialPopupPath = default!;
	[Export] public NodePath StartTutorialButtonPath = default!;
	[Export] public NodePath SkipTutorialButtonPath = default!;
	[Export] public NodePath NewGameNamePopupPath = default!;
	[Export] public NodePath PlayerNameInputPath = default!;
	[Export] public NodePath PlayerNamePreviewLabelPath = default!;
	[Export] public NodePath PlayerNameValidationLabelPath = default!;
	[Export] public NodePath KeyboardRowsPath = default!;
	[Export] public NodePath NameConfirmPopupPath = default!;
	[Export] public NodePath NameConfirmMessageLabelPath = default!;
	[Export] public NodePath NameConfirmAcceptButtonPath = default!;
	[Export] public NodePath NameConfirmEditButtonPath = default!;
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);

	private Button _startButton = default!;
	private Button _newGameButton = default!;
	private Button _loadButton = default!;
	private Button _exitToDesktopButton = default!;
	private Control _newGameTutorialPopup = default!;
	private Button _startTutorialButton = default!;
	private Button _skipTutorialButton = default!;
	private Control _newGameNamePopup = default!;
	private LineEdit _playerNameInput = default!;
	private Label _playerNamePreviewLabel = default!;
	private Label _playerNameValidationLabel = default!;
	private VBoxContainer _keyboardRows = default!;
	private Control _nameConfirmPopup = default!;
	private Label _nameConfirmMessageLabel = default!;
	private Button _nameConfirmAcceptButton = default!;
	private Button _nameConfirmEditButton = default!;
	private SaveGameManager _saveGameManager = default!;
	private Button? _keyboardConfirmButton;
	private bool _pendingStartTutorial;
	private string _pendingConfirmedPlayerName = string.Empty;

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
		if (!NodeLookup.TryGetRequiredNode<Control>(this, NewGameNamePopupPath, nameof(MainMenu), nameof(NewGameNamePopupPath), out _newGameNamePopup))
			return;
		if (!NodeLookup.TryGetRequiredNode<LineEdit>(this, PlayerNameInputPath, nameof(MainMenu), nameof(PlayerNameInputPath), out _playerNameInput))
			return;
		if (!NodeLookup.TryGetRequiredNode<Label>(this, PlayerNamePreviewLabelPath, nameof(MainMenu), nameof(PlayerNamePreviewLabelPath), out _playerNamePreviewLabel))
			return;
		if (!NodeLookup.TryGetRequiredNode<Label>(this, PlayerNameValidationLabelPath, nameof(MainMenu), nameof(PlayerNameValidationLabelPath), out _playerNameValidationLabel))
			return;
		if (!NodeLookup.TryGetRequiredNode<VBoxContainer>(this, KeyboardRowsPath, nameof(MainMenu), nameof(KeyboardRowsPath), out _keyboardRows))
			return;
		if (!NodeLookup.TryGetRequiredNode<Control>(this, NameConfirmPopupPath, nameof(MainMenu), nameof(NameConfirmPopupPath), out _nameConfirmPopup))
			return;
		if (!NodeLookup.TryGetRequiredNode<Label>(this, NameConfirmMessageLabelPath, nameof(MainMenu), nameof(NameConfirmMessageLabelPath), out _nameConfirmMessageLabel))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, NameConfirmAcceptButtonPath, nameof(MainMenu), nameof(NameConfirmAcceptButtonPath), out _nameConfirmAcceptButton))
			return;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, NameConfirmEditButtonPath, nameof(MainMenu), nameof(NameConfirmEditButtonPath), out _nameConfirmEditButton))
			return;

		UpdateButtonLabels();
		UpdateContinueButtonVisibility();
		_newGameTutorialPopup.Visible = false;
		_newGameNamePopup.Visible = false;
		_nameConfirmPopup.Visible = false;
		_playerNameInput.MaxLength = PlayerNameMaxLength;
		BuildOnScreenKeyboard();
		UpdatePlayerNamePreview(showValidation: false);

		_startButton.Pressed += OnStartButtonPressed;
		_newGameButton.Pressed += OnNewGamePressed;
		_loadButton.Pressed += OnLoadButtonPressed;
		_exitToDesktopButton.Pressed += OnExitToDesktopPressed;
		_startTutorialButton.Pressed += OnStartTutorialPressed;
		_skipTutorialButton.Pressed += OnSkipTutorialPressed;
		_playerNameInput.TextChanged += OnPlayerNameChanged;
		_playerNameInput.TextSubmitted += OnPlayerNameSubmitted;
		_nameConfirmAcceptButton.Pressed += OnNameConfirmAccepted;
		_nameConfirmEditButton.Pressed += OnNameConfirmEditPressed;
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
		if (_playerNameInput is not null)
		{
			_playerNameInput.TextChanged -= OnPlayerNameChanged;
			_playerNameInput.TextSubmitted -= OnPlayerNameSubmitted;
		}
		if (_nameConfirmAcceptButton is not null)
			_nameConfirmAcceptButton.Pressed -= OnNameConfirmAccepted;
		if (_nameConfirmEditButton is not null)
			_nameConfirmEditButton.Pressed -= OnNameConfirmEditPressed;
	}

	private void OnNewGamePressed()
	{
		ShowNewGameTutorialPopup();
	}

	private void OnStartTutorialPressed()
	{
		ShowPlayerNamePrompt(startTutorial: true);
	}

	private void OnSkipTutorialPressed()
	{
		ShowPlayerNamePrompt(startTutorial: false);
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

	private void ShowPlayerNamePrompt(bool startTutorial)
	{
		_newGameTutorialPopup.Visible = false;
		_newGameNamePopup.Visible = true;
		_newGameNamePopup.MoveToFront();
		_nameConfirmPopup.Visible = false;
		_pendingStartTutorial = startTutorial;
		_pendingConfirmedPlayerName = string.Empty;
		_playerNameInput.Text = string.Empty;
		UpdatePlayerNamePreview(showValidation: false);
		_playerNameInput.GrabFocus();
	}

	private void OnPlayerNameChanged(string newText)
	{
		if (_nameConfirmPopup.Visible)
			_nameConfirmPopup.Visible = false;

		UpdatePlayerNamePreview(showValidation: !string.IsNullOrEmpty(newText));
	}

	private void OnPlayerNameSubmitted(string newText)
	{
		TryShowNameConfirmation();
	}

	private void OnNameConfirmAccepted()
	{
		if (string.IsNullOrWhiteSpace(_pendingConfirmedPlayerName))
		{
			TryShowNameConfirmation();
			return;
		}

		StartNewGame(_pendingStartTutorial, _pendingConfirmedPlayerName);
	}

	private void OnNameConfirmEditPressed()
	{
		_nameConfirmPopup.Visible = false;
		_playerNameInput.GrabFocus();
	}

	private void TryShowNameConfirmation()
	{
		if (!TryValidatePlayerName(_playerNameInput.Text, out var normalizedName, out var validationMessage))
		{
			_playerNameValidationLabel.Text = validationMessage;
			_playerNameValidationLabel.Visible = true;
			_playerNameInput.GrabFocus();
			return;
		}

		_pendingConfirmedPlayerName = normalizedName;
		_nameConfirmMessageLabel.Text = $"Start game as \"{normalizedName}\"?";
		_nameConfirmPopup.Visible = true;
		_nameConfirmPopup.MoveToFront();
		_nameConfirmAcceptButton.GrabFocus();
	}

	private void StartNewGame(bool startTutorial, string playerName)
	{
		_newGameTutorialPopup.Visible = false;
		_newGameNamePopup.Visible = false;
		_saveGameManager.StartNewGame(startTutorial, playerName);

		Error error = GetTree().ChangeSceneToFile(ScenePaths.IntroCutscene);
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load intro cutscene scene. Error: {error}");
		}
	}

	private void BuildOnScreenKeyboard()
	{
		ClearChildren(_keyboardRows);
		_keyboardConfirmButton = null;

		foreach (var rowText in KeyboardRows)
		{
			var row = new HBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			_keyboardRows.AddChild(row);

			foreach (var key in rowText)
			{
				var keyText = key.ToString();
				AddKeyboardButton(row, keyText, () => AppendNameText(keyText), minWidth: 44, expand: true);
			}
		}

		var commandRow = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_keyboardRows.AddChild(commandRow);

		AddKeyboardButton(commandRow, "Space", () => AppendNameText(" "), minWidth: 120, expand: true);
		AddKeyboardButton(commandRow, "Backspace", BackspaceNameText, minWidth: 120, expand: false);
		AddKeyboardButton(commandRow, "Clear", ClearNameText, minWidth: 88, expand: false);
		_keyboardConfirmButton = AddKeyboardButton(commandRow, "Confirm", TryShowNameConfirmation, minWidth: 110, expand: false);
	}

	private Button AddKeyboardButton(HBoxContainer row, string text, Action action, float minWidth, bool expand)
	{
		var button = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(minWidth, 44),
			SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter
		};
		button.Pressed += action;
		row.AddChild(button);
		return button;
	}

	private void AppendNameText(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		var currentText = _playerNameInput.Text ?? string.Empty;
		if (currentText.Length >= PlayerNameMaxLength)
		{
			_playerNameInput.GrabFocus();
			return;
		}

		var availableLength = PlayerNameMaxLength - currentText.Length;
		var textToAppend = text.Length > availableLength ? text[..availableLength] : text;
		_playerNameInput.Text = currentText + textToAppend;
		_playerNameInput.CaretColumn = _playerNameInput.Text.Length;
		_playerNameInput.GrabFocus();
		UpdatePlayerNamePreview(showValidation: true);
	}

	private void BackspaceNameText()
	{
		var currentText = _playerNameInput.Text ?? string.Empty;
		if (currentText.Length == 0)
		{
			_playerNameInput.GrabFocus();
			return;
		}

		_playerNameInput.Text = currentText[..^1];
		_playerNameInput.CaretColumn = _playerNameInput.Text.Length;
		_playerNameInput.GrabFocus();
		UpdatePlayerNamePreview(showValidation: true);
	}

	private void ClearNameText()
	{
		_playerNameInput.Text = string.Empty;
		_playerNameInput.GrabFocus();
		UpdatePlayerNamePreview(showValidation: false);
	}

	private void UpdatePlayerNamePreview(bool showValidation)
	{
		var isValid = TryValidatePlayerName(_playerNameInput.Text, out var normalizedName, out var validationMessage);
		_playerNamePreviewLabel.Text = string.IsNullOrWhiteSpace(normalizedName)
			? PlayerNamePreviewEmptyText
			: $"Your name: {normalizedName}";
		_playerNameValidationLabel.Text = showValidation && !isValid ? validationMessage : string.Empty;
		_playerNameValidationLabel.Visible = !string.IsNullOrWhiteSpace(_playerNameValidationLabel.Text);

		if (_keyboardConfirmButton is not null)
			_keyboardConfirmButton.Disabled = !isValid;
	}

	private static bool TryValidatePlayerName(string rawName, out string normalizedName, out string validationMessage)
	{
		normalizedName = string.IsNullOrWhiteSpace(rawName) ? string.Empty : rawName.Trim();
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			validationMessage = "Enter your name.";
			return false;
		}

		if (normalizedName.Length > PlayerNameMaxLength)
		{
			validationMessage = $"Name must be {PlayerNameMaxLength} characters or fewer.";
			return false;
		}

		foreach (var character in normalizedName)
		{
			if (char.IsLetterOrDigit(character) || character == ' ' || character == '\'' || character == '-')
				continue;

			validationMessage = "Use only letters, numbers, spaces, apostrophes, and hyphens.";
			return false;
		}

		validationMessage = string.Empty;
		return true;
	}

	private static void ClearChildren(Node container)
	{
		foreach (var child in container.GetChildren())
			child.QueueFree();
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
