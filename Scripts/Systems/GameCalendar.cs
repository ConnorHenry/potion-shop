using System;
using System.Globalization;

namespace OccultShop.Systems;

public readonly record struct GameCalendarDate(int Day, int Month, int Year)
{
	public string ToHudText()
	{
		return $"{Day:00}/{Month:00} Y{Year.ToString(CultureInfo.InvariantCulture)}";
	}

	public string ToDisplayText()
	{
		return $"{Day:00}/{Month:00}, Year {Year.ToString(CultureInfo.InvariantCulture)}";
	}
}

public static class GameCalendar
{
	public const int DaysPerMonth = 28;
	public const int MonthsPerYear = 12;
	public const int DaysPerYear = DaysPerMonth * MonthsPerYear;
	public const int StartDay = 26;
	public const int StartMonth = 3;
	public const int StartYear = 1;

	private const int StartAbsoluteDayIndex =
		(StartYear - 1) * DaysPerYear +
		(StartMonth - 1) * DaysPerMonth +
		(StartDay - 1);

	public static GameCalendarDate ToDate(int gameDay)
	{
		var safeGameDay = Math.Max(1, gameDay);
		var absoluteDayIndex = StartAbsoluteDayIndex + safeGameDay - 1;
		var year = absoluteDayIndex / DaysPerYear + 1;
		var dayOfYear = absoluteDayIndex % DaysPerYear;
		var month = dayOfYear / DaysPerMonth + 1;
		var day = dayOfYear % DaysPerMonth + 1;

		return new GameCalendarDate(day, month, year);
	}

	public static int ToGameDay(GameCalendarDate date)
	{
		if (!IsValidDate(date))
			return 0;

		var absoluteDayIndex =
			(date.Year - 1) * DaysPerYear +
			(date.Month - 1) * DaysPerMonth +
			(date.Day - 1);

		return absoluteDayIndex - StartAbsoluteDayIndex + 1;
	}

	public static bool IsValidDate(GameCalendarDate date)
	{
		return date.Year >= 1
			&& date.Month is >= 1 and <= MonthsPerYear
			&& date.Day is >= 1 and <= DaysPerMonth;
	}
}
