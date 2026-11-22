using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Calendar's day grid (#193) is recognised by its owner's class and each cell the cursor settles on spoken
/// as its day of the month, the grid index plus 1, with the month's length when the game gives it
/// (docs/findings/calendar-screen-and-quest-list-rows.md). Weekday names, events and discounts are left out: their
/// source is not pinned. Hook events in, announcements out; made-up cursor data only.</summary>
[Trait("Category", "Unit")]
public sealed class CalendarFocusTests
{
    private const string CalendarClass = "CalendarChoice@ope@ui@app";
    private const long Grid = 0xc0;
    private const int Settle = 0x1;
    private const int Columns = 5;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public CalendarFocusTests()
    {
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new CalendarFocus(announcer));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    // A settled cursor on the grid at cell `index`, in rows of five, `count` cells in all.
    private void Cell(int index, int count = 30, string? screenClass = CalendarClass) =>
        cursors.Handled(new CursorInput(
            Grid, GridCursor.ClassRva, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46, screenClass, count,
            Grid: GridCursor.Of(Columns, index % Columns, index / Columns, scroll: 0, page: 0, pageStride: 0)));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheCalendarSaysItsNameAndTheFocusedDay()
    {
        Cell(11);

        Assert.Equal(["Calendar, day 12 of 30"], Said());
        Assert.True(announcer.Said[0].Interrupt);
    }

    [Fact]
    public void MovingAcrossAWeekSaysTheNextDay()
    {
        Cell(11);
        Cell(12);
        Cell(13);
        Cell(12);

        Assert.Equal(["Calendar, day 12 of 30", "Day 13 of 30", "Day 14 of 30", "Day 13 of 30"], Said());
    }

    [Fact]
    public void MovingDownARowSaysTheDayFiveLater()
    {
        Cell(11);
        Cell(16);

        Assert.Equal(["Calendar, day 12 of 30", "Day 17 of 30"], Said());
    }

    [Fact]
    public void TheMonthsLengthIsWhateverTheGameCounts()
    {
        Cell(30, count: 35);

        Assert.Equal(["Calendar, day 31 of 35"], Said());
    }

    [Fact]
    public void AnUnreadCountSaysTheDayAlone()
    {
        Cursor(count: null, index: 4);

        Assert.Equal(["Calendar, day 5"], Said());
    }

    [Fact]
    public void AnotherScreenIsLeftToItsOwnSpeech()
    {
        Cell(2, screenClass: "ShopChoice@ope@ui@app");

        Assert.Equal(["3 of 30"], Said());
    }

    private void Cursor(int? count, int index) =>
        cursors.Handled(new CursorInput(
            Grid, GridCursor.ClassRva, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46, CalendarClass, count,
            Grid: GridCursor.Of(Columns, index % Columns, index / Columns, scroll: 0, page: 0, pageStride: 0)));
}
