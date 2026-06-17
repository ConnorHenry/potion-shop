using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using OccultShop.Autoload;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class CalendarPanel : PanelContainer
{
	private const int UpcomingEventHorizonDays = GameCalendar.DaysPerYear;
	private const int UpcomingEventLimit = 8;
	private static readonly Color DefaultDayModulate = new(1.0f, 1.0f, 1.0f, 1.0f);
	private static readonly Color CurrentDayModulate = new(1.0f, 0.88f, 0.45f, 1.0f);
	private static readonly Color EventDayModulate = new(0.82f, 0.95f, 1.0f, 1.0f);

	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath DataDbPath = new(AutoloadNodePaths.DataDb);
	[Export] public NodePath TitleLabelPath = new("Margin/VBox/Header/Title");
	[Export] public NodePath CloseButtonPath = new("Margin/VBox/Header/Close");
	[Export] public NodePath DayGridPath = new("Margin/VBox/DayGrid");
	[Export] public NodePath SelectedDateLabelPath = new("Margin/VBox/SelectedDate");
	[Export] public NodePath EventDetailsPath = new("Margin/VBox/EventDetails");
	[Export] public NodePath UpcomingListPath = new("Margin/VBox/UpcomingList");

	private Label _title = default!;
	private Button _closeButton = default!;
	private GridContainer _dayGrid = default!;
	private Label _selectedDate = default!;
	private RichTextLabel _eventDetails = default!;
	private VBoxContainer _upcomingList = default!;
	private readonly Button[] _dayButtons = new Button[GameCalendar.DaysPerMonth];
	private readonly HashSet<int> _eventDaysInMonth = new();
	private GameState? _gameState;
	private DataDb? _dataDb;
	private GameCalendarDate _selectedCalendarDate;

	public override void _Ready()
	{
		_title = GetNode<Label>(TitleLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);
		_dayGrid = GetNode<GridContainer>(DayGridPath);
		_selectedDate = GetNode<Label>(SelectedDateLabelPath);
		_eventDetails = GetNode<RichTextLabel>(EventDetailsPath);
		_upcomingList = GetNode<VBoxContainer>(UpcomingListPath);
		_eventDetails.BbcodeEnabled = false;

		_gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (_gameState is null)
			GD.PushError($"CalendarPanel: GameState was not found at '{GameStatePath}'.");

		_dataDb = GetNodeOrNull<DataDb>(DataDbPath);
		if (_dataDb is null)
			GD.PushError($"CalendarPanel: DataDb was not found at '{DataDbPath}'.");

		BuildDayButtons();
		_closeButton.Pressed += HidePanel;
		if (_gameState is not null)
			_gameState.Changed += Refresh;

		Visible = false;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_closeButton is not null)
			_closeButton.Pressed -= HidePanel;
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
	}

	public void ShowPanel()
	{
		Visible = true;
		MoveToFront();
		SelectCurrentDay();
	}

	public void HidePanel()
	{
		Visible = false;
	}

	public void TogglePanel()
	{
		if (Visible)
			HidePanel();
		else
			ShowPanel();
	}

	public void Refresh()
	{
		if (_gameState is null || _dataDb is null || _dayButtons[0] is null)
			return;

		var currentDate = GameCalendar.ToDate(_gameState.Day);
		if (!GameCalendar.IsValidDate(_selectedCalendarDate) ||
			_selectedCalendarDate.Month != currentDate.Month ||
			_selectedCalendarDate.Year != currentDate.Year)
		{
			_selectedCalendarDate = currentDate;
		}

		_title.Text = $"Month {currentDate.Month:00} - Year {currentDate.Year}";
		RefreshEventDayCache(currentDate);
		RefreshDayButtons(currentDate);
		RefreshSelectedDateDetails();
		RefreshUpcomingList();
	}

	private void SelectCurrentDay()
	{
		if (_gameState is null)
			return;

		_selectedCalendarDate = GameCalendar.ToDate(_gameState.Day);
		Refresh();
	}

	private void BuildDayButtons()
	{
		_dayGrid.Columns = 7;

		for (var day = 1; day <= GameCalendar.DaysPerMonth; day += 1)
		{
			var capturedDay = day;
			var button = new Button
			{
				Name = $"Day{day:00}",
				Text = day.ToString(System.Globalization.CultureInfo.InvariantCulture),
				CustomMinimumSize = new Vector2(58, 42),
				FocusMode = FocusModeEnum.None
			};
			button.Pressed += () => SelectDay(capturedDay);
			_dayButtons[day - 1] = button;
			_dayGrid.AddChild(button);
		}
	}

	private void SelectDay(int day)
	{
		if (_gameState is null)
			return;

		var currentDate = GameCalendar.ToDate(_gameState.Day);
		_selectedCalendarDate = new GameCalendarDate(day, currentDate.Month, currentDate.Year);
		RefreshDayButtons(currentDate);
		RefreshSelectedDateDetails();
	}

	private void RefreshEventDayCache(GameCalendarDate currentDate)
	{
		_eventDaysInMonth.Clear();
		if (_gameState is null || _dataDb is null)
			return;

		var occurrences = CalendarEventService.GetVisibleEventsForMonth(
			_dataDb.CalendarEvents,
			_gameState,
			currentDate.Month,
			currentDate.Year);
		foreach (var occurrence in occurrences)
			_eventDaysInMonth.Add(occurrence.Date.Day);
	}

	private void RefreshDayButtons(GameCalendarDate currentDate)
	{
		for (var index = 0; index < _dayButtons.Length; index += 1)
		{
			var day = index + 1;
			var button = _dayButtons[index];
			var hasEvents = _eventDaysInMonth.Contains(day);
			button.Text = hasEvents
				? $"{day.ToString(System.Globalization.CultureInfo.InvariantCulture)}*"
				: day.ToString(System.Globalization.CultureInfo.InvariantCulture);
			button.TooltipText = hasEvents ? "Visible event" : "";

			if (day == currentDate.Day)
				button.Modulate = CurrentDayModulate;
			else if (hasEvents)
				button.Modulate = EventDayModulate;
			else
				button.Modulate = DefaultDayModulate;
		}
	}

	private void RefreshSelectedDateDetails()
	{
		if (_gameState is null || _dataDb is null)
			return;

		_selectedDate.Text = _selectedCalendarDate.ToDisplayText();
		var occurrences = CalendarEventService.GetVisibleEventsOnDate(
			_dataDb.CalendarEvents,
			_gameState,
			_selectedCalendarDate);

		if (occurrences.Count == 0)
		{
			_eventDetails.Text = "No visible events.";
			return;
		}

		var builder = new StringBuilder();
		foreach (var occurrence in occurrences)
		{
			if (builder.Length > 0)
				builder.AppendLine().AppendLine();

			builder.AppendLine(occurrence.Event.Title);
			if (!string.IsNullOrWhiteSpace(occurrence.Event.Text))
				builder.Append(occurrence.Event.Text);
		}

		_eventDetails.Text = builder.ToString();
	}

	private void RefreshUpcomingList()
	{
		ClearUpcomingList();
		if (_gameState is null || _dataDb is null)
			return;

		var upcomingEvents = CalendarEventService.GetVisibleUpcomingEvents(
			_dataDb.CalendarEvents,
			_gameState,
			_gameState.Day,
			UpcomingEventHorizonDays,
			UpcomingEventLimit);

		if (upcomingEvents.Count == 0)
		{
			AddUpcomingText("No known upcoming events.");
			return;
		}

		foreach (var occurrence in upcomingEvents)
			AddUpcomingText($"{occurrence.Date.ToDisplayText()}: {occurrence.Event.Title}");
	}

	private void ClearUpcomingList()
	{
		foreach (var child in _upcomingList.GetChildren())
			child.QueueFree();
	}

	private void AddUpcomingText(string text)
	{
		_upcomingList.AddChild(new Label
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 24)
		});
	}
}
