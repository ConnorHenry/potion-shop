using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Dialogue;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class TenYearsLaterCutscene : Control
{
	private const int MaxVisibleOptions = 2;
	private const string MotherSpeakerName = "Mother";
	private const string OpeningNodeId = "juniper_invitation";
	private const string FunOptionId = "really_fun";
	private const string FirstTimeOptionId = "first_time_juniper_picking";
	private const string TimeSkipTitleText = "10 Years Later";
	private const string KitchenNarrationText = "Mother is brewing in the kitchen.";

	[Export] public NodePath TitlePath = new("Title");
	[Export] public NodePath ConversationPath = new("Root/Margin/VBox/Conversation");
	[Export] public NodePath OptionsPath = new("Root/Margin/VBox/Options");
	[Export] public NodePath OptionOneButtonPath = new("Root/Margin/VBox/Options/OptionOne");
	[Export] public NodePath OptionTwoButtonPath = new("Root/Margin/VBox/Options/OptionTwo");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public NodePath SceneTransitionPath = new(AutoloadNodePaths.SceneTransition);
	[Export] public int DialogueTypewriterCharactersPerSecond = 45;
	[Export] public double TitleFadeSeconds = 0.65;
	[Export] public double TitleHoldSeconds = 1.45;

	private GameState _gameState = default!;
	private SaveGameManager _saveGameManager = default!;
	private SceneTransition _sceneTransition = default!;
	private Label _title = default!;
	private RichTextLabel _conversation = default!;
	private VBoxContainer _options = default!;
	private Button _optionOneButton = default!;
	private Button _optionTwoButton = default!;
	private NarrativeTextPresenter? _dialoguePresenter;
	private DialogueSession? _dialogueSession;
	private Tween? _titleTween;
	private CutsceneState _state = CutsceneState.TitleSequence;
	private bool _transitionStarted;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;

		SetProcessInput(true);
		_options.Visible = false;
		_conversation.Visible = false;
		_title.Text = TimeSkipTitleText;
		_optionOneButton.Pressed += OnOptionOnePressed;
		_optionTwoButton.Pressed += OnOptionTwoPressed;

		_dialoguePresenter = new NarrativeTextPresenter(this, _conversation)
		{
			DefaultCharactersPerSecond = DialogueTypewriterCharactersPerSecond
		};

		_gameState.RecordTenYearsLaterCutsceneStarted();
		TryAutoSave("starting the ten years later cutscene");
		StartTitleSequence();
	}

	public override void _ExitTree()
	{
		StopTitleTween();
		if (_optionOneButton is not null)
			_optionOneButton.Pressed -= OnOptionOnePressed;
		if (_optionTwoButton is not null)
			_optionTwoButton.Pressed -= OnOptionTwoPressed;

		_dialoguePresenter?.Dispose();
		_dialoguePresenter = null;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			return;

		if (_state == CutsceneState.TitleSequence || _state == CutsceneState.Choices || _transitionStarted)
			return;

		GetViewport().SetInputAsHandled();
		_dialoguePresenter?.AdvanceQueuedPresentation();
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode<GameState>(this, GameStatePath, nameof(TenYearsLaterCutscene), nameof(GameStatePath), out _gameState))
			return false;
		if (!NodeLookup.TryGetRequiredNode<SaveGameManager>(this, SaveGameManagerPath, nameof(TenYearsLaterCutscene), nameof(SaveGameManagerPath), out _saveGameManager))
			return false;
		if (!NodeLookup.TryGetRequiredNode<SceneTransition>(this, SceneTransitionPath, nameof(TenYearsLaterCutscene), nameof(SceneTransitionPath), out _sceneTransition))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Label>(this, TitlePath, nameof(TenYearsLaterCutscene), nameof(TitlePath), out _title))
			return false;
		if (!NodeLookup.TryGetRequiredNode<RichTextLabel>(this, ConversationPath, nameof(TenYearsLaterCutscene), nameof(ConversationPath), out _conversation))
			return false;
		if (!NodeLookup.TryGetRequiredNode<VBoxContainer>(this, OptionsPath, nameof(TenYearsLaterCutscene), nameof(OptionsPath), out _options))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, OptionOneButtonPath, nameof(TenYearsLaterCutscene), nameof(OptionOneButtonPath), out _optionOneButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, OptionTwoButtonPath, nameof(TenYearsLaterCutscene), nameof(OptionTwoButtonPath), out _optionTwoButton))
			return false;

		return true;
	}

	private void StartTitleSequence()
	{
		_state = CutsceneState.TitleSequence;
		_title.Visible = true;
		SetControlAlpha(_title, 0.0f);
		StopTitleTween();
		_titleTween = CreateTween();
		_titleTween.SetTrans(Tween.TransitionType.Sine);
		_titleTween.SetEase(Tween.EaseType.InOut);
		_titleTween.TweenProperty(_title, "modulate:a", 1.0f, TitleFadeSeconds);
		_titleTween.TweenInterval(TitleHoldSeconds);
		_titleTween.TweenProperty(_title, "modulate:a", 0.0f, TitleFadeSeconds);
		_titleTween.Finished += StartKitchenDialogue;
	}

	private void StartKitchenDialogue()
	{
		StopTitleTween();
		_title.Visible = false;
		_conversation.Visible = true;

		var graph = BuildDialogueGraph(GetPlayerNameForDialogue());
		_dialogueSession = new DialogueSession(graph, _ => true, MaxVisibleOptions);
		if (!_dialogueSession.TryStart(out var startNode) || startNode is null)
		{
			GD.PushError("TenYearsLaterCutscene: Could not start dialogue graph.");
			return;
		}

		_state = CutsceneState.Dialogue;
		QueueDialogueNodeText(startNode);
		PlayQueuedDialogueLines(ShowChoices);
	}

	private void ShowChoices()
	{
		var session = _dialogueSession;
		if (session is null)
		{
			GD.PushError("TenYearsLaterCutscene: Dialogue session was missing before choices could be shown.");
			return;
		}

		var visibleOptions = session.RefreshVisibleOptions();
		if (visibleOptions.Count < MaxVisibleOptions)
		{
			GD.PushError("TenYearsLaterCutscene: Dialogue graph did not provide both required options.");
			return;
		}

		_state = CutsceneState.Choices;
		_options.Visible = true;
		SetOptionButton(_optionOneButton, visibleOptions[0]);
		SetOptionButton(_optionTwoButton, visibleOptions[1]);
		_optionOneButton.GrabFocus();
	}

	private void OnOptionOnePressed()
	{
		TrySelectOption(0);
	}

	private void OnOptionTwoPressed()
	{
		TrySelectOption(1);
	}

	private void TrySelectOption(int optionIndex)
	{
		var session = _dialogueSession;
		if (session is null)
		{
			GD.PushError("TenYearsLaterCutscene: Dialogue session was missing when an option was selected.");
			return;
		}

		if (!session.TrySelectVisibleOption(optionIndex, out var option) || option is null)
		{
			GD.PushError($"TenYearsLaterCutscene: Dialogue option index '{optionIndex}' was not available.");
			return;
		}

		_state = CutsceneState.Response;
		_options.Visible = false;
		QueuePlayerLine(option.Label);
		PlayQueuedDialogueLines(TransitionToJuniperGathering);
	}

	private void QueueDialogueNodeText(DialogueNode node)
	{
		QueueDialogueLines(node.Lines, node.Text, null);
	}

	private void QueueDialogueLines(
		IReadOnlyList<DialogueLine> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		if (_dialoguePresenter is null)
			return;

		foreach (var line in DialogueNarrativeLineBuilder.BuildNarrativeLines(lines, fallbackText, fallbackSpeaker))
			_dialoguePresenter.QueueLine(line);
	}

	private void QueuePlayerLine(string text)
	{
		_dialoguePresenter?.QueueLine(new NarrativeTextLine(
			CustomerDialogueTextFormatter.PlayerSpeakerName,
			text,
			allowMarkup: false));
	}

	private void PlayQueuedDialogueLines(System.Action? completedAction)
	{
		if (_dialoguePresenter is null)
		{
			completedAction?.Invoke();
			return;
		}

		_dialoguePresenter.DefaultCharactersPerSecond = DialogueTypewriterCharactersPerSecond;
		_dialoguePresenter.PlayQueued(completedAction);
	}

	private void TransitionToJuniperGathering()
	{
		if (_transitionStarted)
			return;

		_transitionStarted = true;
		_gameState.RecordTenYearsLaterCutsceneCompleted();
		TryAutoSave("completing the ten years later cutscene");
		_sceneTransition.ChangeSceneWithFade(ScenePaths.JuniperGathering);
	}

	private bool TryAutoSave(string context)
	{
		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"TenYearsLaterCutscene: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private string GetPlayerNameForDialogue()
	{
		return string.IsNullOrWhiteSpace(_gameState.PlayerName)
			? "there"
			: _gameState.PlayerName.Trim();
	}

	private void StopTitleTween()
	{
		if (_titleTween is null)
			return;

		_titleTween.Finished -= StartKitchenDialogue;
		_titleTween.Kill();
		_titleTween = null;
	}

	private static void SetControlAlpha(CanvasItem control, float alpha)
	{
		var color = control.Modulate;
		color.A = alpha;
		control.Modulate = color;
	}

	private static void SetOptionButton(Button button, DialogueOption option)
	{
		button.Text = option.Label;
		button.Visible = true;
		button.Disabled = false;
	}

	private static DialogueGraph BuildDialogueGraph(string playerName)
	{
		return new DialogueGraph
		{
			StartNodeId = OpeningNodeId,
			Nodes =
			{
				new DialogueNode
				{
					Id = OpeningNodeId,
					Lines =
					{
						new DialogueLine
						{
							Text = KitchenNarrationText
						},
						new DialogueLine
						{
							Speaker = MotherSpeakerName,
							Text = $"Come on {playerName}, we need to go juniper picking"
						}
					},
					Options =
					{
						new DialogueOption
						{
							Id = FunOptionId,
							Label = "Really?? Fun!"
						},
						new DialogueOption
						{
							Id = FirstTimeOptionId,
							Label = "You've never let me come juniper picking before?"
						}
					}
				}
			}
		};
	}

	private enum CutsceneState
	{
		TitleSequence,
		Dialogue,
		Choices,
		Response
	}
}
