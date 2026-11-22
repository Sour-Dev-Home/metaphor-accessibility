using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Calendar's day grid (#193): on entering it "Calendar, day n of total", then on each focus move
/// "Day n of total", each cutting off the last. The day is the grid cursor's index plus 1 and the total the cell count
/// the game keeps (<see cref="ScreenEvent.Total"/>), the position alone when it can't be read. Safe to call from
/// several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) the Calendar's grid has one cell per day, so the index is the day
/// of the month minus 1 (docs/findings/calendar-screen-and-quest-list-rows.md, heard 2026-11-22). Not spoken, because
/// the findings don't pin them: the weekday name (serial 9285 plus the column is in no message file the mod knows, and
/// 5 or 7 names is <c>[NEEDS VERIFICATION]</c>), the month, and the events and discounts of a day
/// (<c>[NEEDS VERIFICATION]</c>: how the day panel picks them).</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class CalendarFocus : IScreenObserver
{
    /// <summary>The Calendar: its class on build 548ddc95, as the mod's cursor hook logs it
    /// (tests/fixtures/sessions/title-file-during-play.txt).</summary>
    public static Screen Screen { get; } = new("Calendar", "CalendarChoice@ope@ui@app");

    private readonly IAnnouncer announcer;

    public CalendarFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focused day when the Calendar is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var day = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        var said = change.Change == ScreenChange.Entered ? $"Calendar, day {day}" : $"Day {day}";
        announcer.Announce(new Announcement(said, Interrupt: true));
    }
}
