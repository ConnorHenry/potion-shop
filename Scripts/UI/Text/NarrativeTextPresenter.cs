using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace OccultShop.UI;

public sealed class NarrativeTextLine
{
	public NarrativeTextLine(string? speaker, string text, bool allowMarkup = true, string characterImageKey = "")
	{
		Speaker = speaker;
		Text = text ?? string.Empty;
		AllowMarkup = allowMarkup;
		CharacterImageKey = characterImageKey ?? string.Empty;
	}

	public string? Speaker { get; }
	public string Text { get; }
	public bool AllowMarkup { get; }
	public string CharacterImageKey { get; }
}

public sealed class NarrativeTextPresenter : IDisposable
{
	private const int SeparatorVisibleCharacters = 2;

	private readonly Node _owner;
	private readonly RichTextLabel _label;
	private readonly Godot.Timer _timer;
	private readonly List<PresentedLine> _history = new();
	private readonly Queue<NarrativeTextLine> _pendingLines = new();

	private PresentedLine? _activeLine;
	private int _activeVisibleCharacters;
	private int _activeCommandIndex;
	private int _activeCharactersPerSecond;
	private bool _timerAdvancesCharacter;
	private Action? _queueCompletedAction;
	private bool _disposed;

	public NarrativeTextPresenter(Node owner, RichTextLabel label)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		_label = label ?? throw new ArgumentNullException(nameof(label));
		_label.BbcodeEnabled = true;
		_label.VisibleCharacters = -1;

		_timer = new Godot.Timer
		{
			Name = "NarrativeTextPresenterTimer",
			OneShot = true,
			Autostart = false,
			WaitTime = GetCharacterIntervalSeconds()
		};
		_owner.AddChild(_timer);
		_timer.Timeout += OnTimerTimeout;
	}

	public int DefaultCharactersPerSecond { get; set; } = 45;
	public bool HasActiveLine => _activeLine is not null;
	public bool HasPendingLines => _pendingLines.Count > 0;
	public event Action<NarrativeTextLine>? LineStarted;

	public void Clear()
	{
		StopTimer();
		_history.Clear();
		_pendingLines.Clear();
		_activeLine = null;
		_activeVisibleCharacters = 0;
		_activeCommandIndex = 0;
		_queueCompletedAction = null;
		_label.Text = string.Empty;
		_label.VisibleCharacters = -1;
	}

	public void SetHistory(IReadOnlyList<NarrativeTextLine> lines)
	{
		Clear();
		AddHistoryLines(lines);
	}

	public void AddHistoryLines(IReadOnlyList<NarrativeTextLine> lines)
	{
		foreach (var line in lines)
			AddHistoryLine(line, updateDisplay: false);

		UpdateDisplayText();
		_label.VisibleCharacters = -1;
	}

	public void AddHistoryLine(NarrativeTextLine line)
	{
		AddHistoryLine(line, updateDisplay: true);
	}

	public void QueueLine(NarrativeTextLine line)
	{
		if (string.IsNullOrWhiteSpace(line.Text))
			return;

		_pendingLines.Enqueue(line);
	}

	public void PlayQueued(Action? completedAction)
	{
		_queueCompletedAction = completedAction;
		if (_activeLine is null && _pendingLines.Count > 0)
			StartNextQueuedLine();

		TryCompleteQueue();
	}

	public void AdvanceQueuedPresentation()
	{
		if (_activeLine is not null)
		{
			CompleteActiveLine();
			return;
		}

		if (_pendingLines.Count > 0)
			StartNextQueuedLine();
		else
			TryCompleteQueue();
	}

	public void StopQueuedPresentation()
	{
		StopTimer();
		_pendingLines.Clear();
		_activeLine = null;
		_activeVisibleCharacters = 0;
		_activeCommandIndex = 0;
		_queueCompletedAction = null;
		UpdateDisplayText();
		_label.VisibleCharacters = -1;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_timer.Timeout -= OnTimerTimeout;
		StopTimer();
		_timer.QueueFree();
	}

	private void AddHistoryLine(NarrativeTextLine line, bool updateDisplay)
	{
		if (string.IsNullOrWhiteSpace(line.Text))
			return;

		if (_activeLine is not null)
			CompleteActiveLine();

		LineStarted?.Invoke(line);
		_history.Add(BuildPresentedLine(line));

		if (!updateDisplay)
			return;

		UpdateDisplayText();
		_label.VisibleCharacters = -1;
	}

	private void StartNextQueuedLine()
	{
		if (_pendingLines.Count == 0)
		{
			TryCompleteQueue();
			return;
		}

		StopTimer();
		var line = _pendingLines.Dequeue();
		LineStarted?.Invoke(line);
		_activeLine = BuildPresentedLine(line);
		_activeVisibleCharacters = _activeLine.InitialVisibleCharacters;
		_activeCommandIndex = 0;
		_activeCharactersPerSecond = Math.Max(1, DefaultCharactersPerSecond);

		UpdateDisplayText();
		UpdateVisibleCharacters();

		if (_activeVisibleCharacters >= _activeLine.TotalVisibleCharacters)
		{
			CompleteActiveLine();
			return;
		}

		ScheduleNextTick();
	}

	private PresentedLine BuildPresentedLine(NarrativeTextLine line)
	{
		var document = line.AllowMarkup
			? CustomerDialogueMarkupConverter.ConvertToBbCode(line.Text)
			: CustomerDialogueMarkupConverter.ConvertPlainText(line.Text);

		foreach (var warning in document.Warnings)
			GD.PushWarning($"Dialogue text markup: {warning}");

		if (string.IsNullOrWhiteSpace(line.Speaker))
		{
			return new PresentedLine(
				document.BbCode,
				document.VisibleCharacterCount,
				initialVisibleCharacters: 0,
				document.Commands);
		}

		var speakerText = line.Speaker.Trim();
		var speakerVisibleCharacters = CustomerDialogueMarkupConverter.CountVisibleCharacters(speakerText) + 1;
		var shiftedCommands = new List<NarrativeTextCommand>(document.Commands.Count);
		foreach (var command in document.Commands)
		{
			shiftedCommands.Add(new NarrativeTextCommand(
				command.Kind,
				command.VisibleCharacterIndex + speakerVisibleCharacters,
				command.NumericValue));
		}

		return new PresentedLine(
			$"{CustomerDialogueTextFormatter.FormatSpeakerName(speakerText)}\n{document.BbCode}",
			speakerVisibleCharacters + document.VisibleCharacterCount,
			speakerVisibleCharacters,
			shiftedCommands);
	}

	private void ScheduleNextTick()
	{
		if (_activeLine is null)
			return;

		while (_activeCommandIndex < _activeLine.Commands.Count &&
			_activeLine.Commands[_activeCommandIndex].VisibleCharacterIndex <= _activeVisibleCharacters)
		{
			var command = _activeLine.Commands[_activeCommandIndex];
			_activeCommandIndex += 1;

			if (command.Kind == NarrativeTextCommandKind.Speed)
			{
				_activeCharactersPerSecond = Math.Max(1, (int)Math.Round(command.NumericValue));
				continue;
			}

			if (command.Kind != NarrativeTextCommandKind.Pause)
				continue;

			_timerAdvancesCharacter = false;
			_timer.WaitTime = Math.Max(0.01, command.NumericValue);
			_timer.Start();
			return;
		}

		if (_activeVisibleCharacters >= _activeLine.TotalVisibleCharacters)
		{
			CompleteActiveLine();
			return;
		}

		_timerAdvancesCharacter = true;
		_timer.WaitTime = GetCharacterIntervalSeconds();
		_timer.Start();
	}

	private void OnTimerTimeout()
	{
		if (_activeLine is null)
		{
			StopTimer();
			TryCompleteQueue();
			return;
		}

		if (!_timerAdvancesCharacter)
		{
			ScheduleNextTick();
			return;
		}

		_activeVisibleCharacters = Math.Min(
			_activeLine.TotalVisibleCharacters,
			_activeVisibleCharacters + 1);
		UpdateVisibleCharacters();

		if (_activeVisibleCharacters >= _activeLine.TotalVisibleCharacters)
		{
			CompleteActiveLine();
			return;
		}

		ScheduleNextTick();
	}

	private void CompleteActiveLine()
	{
		if (_activeLine is null)
			return;

		StopTimer();
		_history.Add(_activeLine);
		_activeLine = null;
		_activeVisibleCharacters = 0;
		_activeCommandIndex = 0;
		_activeCharactersPerSecond = Math.Max(1, DefaultCharactersPerSecond);
		UpdateDisplayText();
		_label.VisibleCharacters = -1;
		TryCompleteQueue();
	}

	private void TryCompleteQueue()
	{
		if (_activeLine is not null || _pendingLines.Count > 0)
			return;

		var completedAction = _queueCompletedAction;
		_queueCompletedAction = null;
		completedAction?.Invoke();
	}

	private void UpdateDisplayText()
	{
		var builder = new StringBuilder();
		var hasLine = false;
		foreach (var line in _history)
		{
			AppendLine(builder, line.BbCode, ref hasLine);
		}

		if (_activeLine is not null)
			AppendLine(builder, _activeLine.BbCode, ref hasLine);

		_label.Text = builder.ToString();
	}

	private void UpdateVisibleCharacters()
	{
		if (_activeLine is null)
		{
			_label.VisibleCharacters = -1;
			return;
		}

		var visibleCharacters = 0;
		var hasLine = false;
		foreach (var line in _history)
		{
			if (hasLine)
				visibleCharacters += SeparatorVisibleCharacters;
			visibleCharacters += line.TotalVisibleCharacters;
			hasLine = true;
		}

		if (hasLine)
			visibleCharacters += SeparatorVisibleCharacters;

		visibleCharacters += _activeVisibleCharacters;
		_label.VisibleCharacters = Math.Max(0, visibleCharacters);
	}

	private static void AppendLine(StringBuilder builder, string bbCode, ref bool hasLine)
	{
		if (hasLine)
			builder.Append("\n\n");

		builder.Append(bbCode);
		hasLine = true;
	}

	private void StopTimer()
	{
		_timer.Stop();
		_timerAdvancesCharacter = false;
	}

	private double GetCharacterIntervalSeconds()
	{
		return 1.0 / Math.Max(1, _activeCharactersPerSecond <= 0 ? DefaultCharactersPerSecond : _activeCharactersPerSecond);
	}

	private sealed class PresentedLine
	{
		public PresentedLine(
			string bbCode,
			int totalVisibleCharacters,
			int initialVisibleCharacters,
			IReadOnlyList<NarrativeTextCommand> commands)
		{
			BbCode = bbCode;
			TotalVisibleCharacters = Math.Max(0, totalVisibleCharacters);
			InitialVisibleCharacters = Math.Clamp(initialVisibleCharacters, 0, TotalVisibleCharacters);
			Commands = commands;
		}

		public string BbCode { get; }
		public int TotalVisibleCharacters { get; }
		public int InitialVisibleCharacters { get; }
		public IReadOnlyList<NarrativeTextCommand> Commands { get; }
	}
}
