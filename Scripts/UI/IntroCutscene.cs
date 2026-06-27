using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Dialogue;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class IntroCutscene : Control
{
	private const int MaxVisibleOptions = 2;
	private const string RainfallAudioPath = "res://Assets/Audio/rain-sounds.mp3";
	private const string MotherSpeakerName = "Mother";
	private const string OpeningNodeId = "opening";
	private const string AskOkayOptionId = "ask_okay";
	private const string SunNotUpOptionId = "sun_not_up";
	private const string OpeningNarrationText =
		"Your mother stands hunched over beside your bed, lit by only a candle. That and the sound of rain are the only senses you take in.";

	[Export] public NodePath ConversationPath = new("Root/Margin/VBox/Conversation");
	[Export] public NodePath OptionsPath = new("Root/Margin/VBox/Options");
	[Export] public NodePath OptionOneButtonPath = new("Root/Margin/VBox/Options/OptionOne");
	[Export] public NodePath OptionTwoButtonPath = new("Root/Margin/VBox/Options/OptionTwo");
	[Export] public NodePath RainPlayerPath = new("RainPlayer");
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath SceneTransitionPath = new(AutoloadNodePaths.SceneTransition);
	[Export] public int DialogueTypewriterCharactersPerSecond = 45;

	private GameState _gameState = default!;
	private SceneTransition _sceneTransition = default!;
	private RichTextLabel _conversation = default!;
	private VBoxContainer _options = default!;
	private Button _optionOneButton = default!;
	private Button _optionTwoButton = default!;
	private AudioStreamPlayer _rainPlayer = default!;
	private NarrativeTextPresenter? _dialoguePresenter;
	private DialogueSession? _dialogueSession;
	private IntroCutsceneState _state = IntroCutsceneState.Opening;
	private bool _transitionStarted;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;

		SetProcessInput(true);
		_options.Visible = false;
		_optionOneButton.Pressed += OnOptionOnePressed;
		_optionTwoButton.Pressed += OnOptionTwoPressed;

		_dialoguePresenter = new NarrativeTextPresenter(this, _conversation)
		{
			DefaultCharactersPerSecond = DialogueTypewriterCharactersPerSecond
		};

		ConfigureRainPlayer();
		StartOpeningDialogue();
	}

	public override void _ExitTree()
	{
		if (_optionOneButton is not null)
			_optionOneButton.Pressed -= OnOptionOnePressed;
		if (_optionTwoButton is not null)
			_optionTwoButton.Pressed -= OnOptionTwoPressed;

		if (_rainPlayer is not null && _rainPlayer.Playing)
			_rainPlayer.Stop();

		_dialoguePresenter?.Dispose();
		_dialoguePresenter = null;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			return;

		if (_state == IntroCutsceneState.Choices || _transitionStarted)
			return;

		GetViewport().SetInputAsHandled();
		if (_state == IntroCutsceneState.AwaitingFinalClick)
		{
			TransitionToMainScene();
			return;
		}

		_dialoguePresenter?.AdvanceQueuedPresentation();
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode<GameState>(this, GameStatePath, nameof(IntroCutscene), nameof(GameStatePath), out _gameState))
			return false;
		if (!NodeLookup.TryGetRequiredNode<SceneTransition>(this, SceneTransitionPath, nameof(IntroCutscene), nameof(SceneTransitionPath), out _sceneTransition))
			return false;
		if (!NodeLookup.TryGetRequiredNode<RichTextLabel>(this, ConversationPath, nameof(IntroCutscene), nameof(ConversationPath), out _conversation))
			return false;
		if (!NodeLookup.TryGetRequiredNode<VBoxContainer>(this, OptionsPath, nameof(IntroCutscene), nameof(OptionsPath), out _options))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, OptionOneButtonPath, nameof(IntroCutscene), nameof(OptionOneButtonPath), out _optionOneButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode<Button>(this, OptionTwoButtonPath, nameof(IntroCutscene), nameof(OptionTwoButtonPath), out _optionTwoButton))
			return false;
		if (!NodeLookup.TryGetRequiredNode<AudioStreamPlayer>(this, RainPlayerPath, nameof(IntroCutscene), nameof(RainPlayerPath), out _rainPlayer))
			return false;

		return true;
	}

	private void ConfigureRainPlayer()
	{
		var stream = ResourceLoader.Load<AudioStream>(RainfallAudioPath);
		if (stream is null)
		{
			GD.PushError($"IntroCutscene: Rainfall audio stream could not be loaded from '{RainfallAudioPath}'.");
			return;
		}

		_rainPlayer.Stream = stream;
		_rainPlayer.Play();
	}

	private void StartOpeningDialogue()
	{
		var graph = BuildDialogueGraph(GetPlayerNameForDialogue());
		_dialogueSession = new DialogueSession(graph, _ => true, MaxVisibleOptions);
		if (!_dialogueSession.TryStart(out var startNode) || startNode is null)
		{
			GD.PushError("IntroCutscene: Could not start intro dialogue graph.");
			return;
		}

		_state = IntroCutsceneState.Opening;
		QueueDialogueNodeText(startNode);
		PlayQueuedDialogueLines(ShowChoices);
	}

	private void ShowChoices()
	{
		var session = _dialogueSession;
		if (session is null)
		{
			GD.PushError("IntroCutscene: Dialogue session was missing before choices could be shown.");
			return;
		}

		var visibleOptions = session.RefreshVisibleOptions();
		if (visibleOptions.Count < MaxVisibleOptions)
		{
			GD.PushError("IntroCutscene: Intro dialogue graph did not provide both required options.");
			return;
		}

		_state = IntroCutsceneState.Choices;
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
			GD.PushError("IntroCutscene: Dialogue session was missing when an option was selected.");
			return;
		}

		if (!session.TrySelectVisibleOption(optionIndex, out var option) || option is null)
		{
			GD.PushError($"IntroCutscene: Dialogue option index '{optionIndex}' was not available.");
			return;
		}

		_state = IntroCutsceneState.Response;
		_options.Visible = false;
		QueuePlayerLine(option.Label);
		QueueDialogueLines(option.ResponseLines, option.ResponseText, MotherSpeakerName);
		PlayQueuedDialogueLines(() => _state = IntroCutsceneState.AwaitingFinalClick);
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

	private void TransitionToMainScene()
	{
		if (_transitionStarted)
			return;

		_transitionStarted = true;
		_gameState.RecordIntroCutsceneCompleted();
		_sceneTransition.ChangeSceneWithFade(ScenePaths.Main);
	}

	private string GetPlayerNameForDialogue()
	{
		return string.IsNullOrWhiteSpace(_gameState.PlayerName)
			? "there"
			: _gameState.PlayerName.Trim();
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
							Speaker = MotherSpeakerName,
							Text = $"Hey, hey wake up {playerName}."
						},
						new DialogueLine
						{
							Text = OpeningNarrationText
						}
					},
					Options =
					{
						new DialogueOption
						{
							Id = AskOkayOptionId,
							Label = "Is everything okay?",
							ResponseLines =
							{
								new DialogueLine
								{
									Speaker = MotherSpeakerName,
									Text = "Yes, worry not, everything's fine. I just need your help with something."
								}
							}
						},
						new DialogueOption
						{
							Id = SunNotUpOptionId,
							Label = "The sun isn't even up yet.",
							ResponseLines =
							{
								new DialogueLine
								{
									Speaker = MotherSpeakerName,
									Text = "I know dear but I just need your help with something."
								}
							}
						}
					}
				}
			}
		};
	}

	private enum IntroCutsceneState
	{
		Opening,
		Choices,
		Response,
		AwaitingFinalClick
	}
}
