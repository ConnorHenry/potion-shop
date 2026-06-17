using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class CalendarEventOccurrence
{
	public CalendarEventDef Event { get; init; } = new();
	public GameCalendarDate Date { get; init; }
	public int GameDay { get; init; }
}

public static class CalendarEventService
{
	public static List<CalendarEventOccurrence> GetVisibleEventsOnDate(
		IEnumerable<CalendarEventDef> events,
		GameState state,
		GameCalendarDate date)
	{
		var occurrences = new List<CalendarEventOccurrence>();
		if (!GameCalendar.IsValidDate(date))
			return occurrences;

		foreach (var calendarEvent in events)
		{
			if (!IsVisible(calendarEvent, state) || !OccursOn(calendarEvent, date))
				continue;

			var gameDay = GameCalendar.ToGameDay(date);
			if (gameDay < 1)
				continue;

			occurrences.Add(new CalendarEventOccurrence
			{
				Event = calendarEvent,
				Date = date,
				GameDay = gameDay
			});
		}

		return occurrences
			.OrderBy(x => x.Event.Title, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.Event.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static List<CalendarEventOccurrence> GetVisibleEventsForMonth(
		IEnumerable<CalendarEventDef> events,
		GameState state,
		int month,
		int year)
	{
		var occurrences = new List<CalendarEventOccurrence>();
		if (month is < 1 or > GameCalendar.MonthsPerYear || year < 1)
			return occurrences;

		for (var day = 1; day <= GameCalendar.DaysPerMonth; day += 1)
		{
			var date = new GameCalendarDate(day, month, year);
			occurrences.AddRange(GetVisibleEventsOnDate(events, state, date));
		}

		return occurrences
			.OrderBy(x => x.GameDay)
			.ThenBy(x => x.Event.Title, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.Event.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static List<CalendarEventOccurrence> GetVisibleUpcomingEvents(
		IEnumerable<CalendarEventDef> events,
		GameState state,
		int currentGameDay,
		int horizonDays,
		int maxCount)
	{
		var occurrences = new List<CalendarEventOccurrence>();
		var safeCurrentGameDay = Math.Max(1, currentGameDay);
		var safeHorizonDays = Math.Max(1, horizonDays);

		foreach (var calendarEvent in events)
		{
			if (!IsVisible(calendarEvent, state))
				continue;

			if (TryResolveNextOccurrence(calendarEvent, safeCurrentGameDay, safeHorizonDays, out var occurrence))
				occurrences.Add(occurrence);
		}

		return occurrences
			.OrderBy(x => x.GameDay)
			.ThenBy(x => x.Event.Title, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.Event.Id, StringComparer.OrdinalIgnoreCase)
			.Take(Math.Max(0, maxCount))
			.ToList();
	}

	private static bool IsVisible(CalendarEventDef calendarEvent, GameState state)
	{
		return calendarEvent is not null && Requirements.Met(state, calendarEvent.VisibilityRequirements);
	}

	private static bool OccursOn(CalendarEventDef calendarEvent, GameCalendarDate date)
	{
		if (calendarEvent.Day != date.Day || calendarEvent.Month != date.Month)
			return false;

		if (calendarEvent.RepeatsYearly)
			return true;

		return calendarEvent.Year is int eventYear && eventYear == date.Year;
	}

	private static bool TryResolveNextOccurrence(
		CalendarEventDef calendarEvent,
		int currentGameDay,
		int horizonDays,
		out CalendarEventOccurrence occurrence)
	{
		occurrence = default!;
		if (!HasValidMonthAndDay(calendarEvent))
			return false;

		var currentDate = GameCalendar.ToDate(currentGameDay);
		if (calendarEvent.RepeatsYearly)
		{
			for (var year = currentDate.Year; year <= currentDate.Year + 1; year += 1)
			{
				var date = new GameCalendarDate(calendarEvent.Day, calendarEvent.Month, year);
				if (TryBuildFutureOccurrence(calendarEvent, date, currentGameDay, horizonDays, out occurrence))
					return true;
			}

			return false;
		}

		if (calendarEvent.Year is not int eventYear)
			return false;

		return TryBuildFutureOccurrence(
			calendarEvent,
			new GameCalendarDate(calendarEvent.Day, calendarEvent.Month, eventYear),
			currentGameDay,
			horizonDays,
			out occurrence);
	}

	private static bool TryBuildFutureOccurrence(
		CalendarEventDef calendarEvent,
		GameCalendarDate date,
		int currentGameDay,
		int horizonDays,
		out CalendarEventOccurrence occurrence)
	{
		occurrence = default!;
		var gameDay = GameCalendar.ToGameDay(date);
		if (gameDay <= currentGameDay || gameDay > currentGameDay + horizonDays)
			return false;

		occurrence = new CalendarEventOccurrence
		{
			Event = calendarEvent,
			Date = date,
			GameDay = gameDay
		};
		return true;
	}

	private static bool HasValidMonthAndDay(CalendarEventDef calendarEvent)
	{
		return calendarEvent.Month is >= 1 and <= GameCalendar.MonthsPerYear
			&& calendarEvent.Day is >= 1 and <= GameCalendar.DaysPerMonth;
	}
}
