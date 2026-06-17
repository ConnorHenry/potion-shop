using OccultShop.Systems;
using static ProjectFileTestHelper;
using static TestAssert;

internal static class CalendarTests
{
    public static void Register(TestRunner runner)
    {
        runner.Run("Game calendar maps canonical game days", TestGameCalendarMapsCanonicalGameDays);
        runner.Run("Calendar authored data contract stays wired", TestCalendarAuthoredDataContract);
    }

    private static void TestGameCalendarMapsCanonicalGameDays()
    {
        var dayOne = GameCalendar.ToDate(1);
        AssertEqual("Day 1 day", 26, dayOne.Day);
        AssertEqual("Day 1 month", 3, dayOne.Month);
        AssertEqual("Day 1 year", 1, dayOne.Year);
        AssertEqual("Day 1 HUD text", "26/03 Y1", dayOne.ToHudText());

        var aprilSecond = GameCalendar.ToDate(5);
        AssertEqual("Day 5 day", 2, aprilSecond.Day);
        AssertEqual("Day 5 month", 4, aprilSecond.Month);
        AssertEqual("Day 5 year", 1, aprilSecond.Year);
        AssertEqual("02/04 Y1 game day", 5, GameCalendar.ToGameDay(aprilSecond));

        var nextYearStart = GameCalendar.ToDate(GameCalendar.DaysPerYear + 1);
        AssertEqual("Next year day", 26, nextYearStart.Day);
        AssertEqual("Next year month", 3, nextYearStart.Month);
        AssertEqual("Next year", 2, nextYearStart.Year);
    }

    private static void TestCalendarAuthoredDataContract()
    {
        var model = ReadProjectFile("Scripts/Models/CalendarEventDef.cs");
        var service = ReadProjectFile("Scripts/Systems/CalendarEventService.cs");
        var dataDb = ReadProjectFile("Scripts/Autoload/DataDb.cs");
        var data = ReadProjectFile("Data/calendar_events_data.tres");

        AssertTrue("Calendar events store scheduled date fields",
            model.Contains("public int Day") &&
            model.Contains("public int Month") &&
            model.Contains("public int? Year"));
        AssertTrue("Calendar events support yearly repeats and visibility requirements",
            model.Contains("public bool RepeatsYearly") &&
            model.Contains("VisibilityRequirements"));
        AssertTrue("Calendar service filters visible events and supports yearly repeats",
            service.Contains("Requirements.Met(state, calendarEvent.VisibilityRequirements)") &&
            service.Contains("calendarEvent.RepeatsYearly") &&
            service.Contains("GetVisibleUpcomingEvents"));
        AssertTrue("DataDb parses calendar event fields",
            dataDb.Contains("Day = ReadInt(entry, \"day\", 0)") &&
            dataDb.Contains("Month = ReadInt(entry, \"month\", 0)") &&
            dataDb.Contains("Year = ReadNullableInt(entry, \"year\")") &&
            dataDb.Contains("RepeatsYearly = ReadBool(entry, \"repeatsYearly\")"));
        AssertTrue("Example calendar event is scheduled on 02/04 Year 1",
            data.Contains("\"day\": 2") &&
            data.Contains("\"month\": 4") &&
            data.Contains("\"year\": 1"));
    }
}
