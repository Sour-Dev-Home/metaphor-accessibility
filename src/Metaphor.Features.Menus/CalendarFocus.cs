using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Calendar's day grid (#193): on entering it "Calendar, day n of total", then on each focus move
/// "Day n of total", each cutting off the last. The day is the grid cursor's index plus 1 and the total the cell count
/// the game keeps (<see cref="ScreenEvent.Total"/>), the position alone when it can't be read. Safe to call from
/// several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) the Calendar's grid has one cell per day, so the index is the day
/// of the month minus 1 (docs/findings/calendar-screen-and-quest-list-rows.md, heard 2026-11-22). The weekday is the
/// column, five names as heard in session "c" (the text of serials 9290 to 9294 is not logged). Not spoken, because no
/// log carries them: the month, and the events and discounts of a day, whose message lookups (the
/// <c>Calendar_Message</c> labels) are not logged
/// (<c>[NEEDS VERIFICATION]</c>: how the day panel picks them).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.Calendar)]
public sealed class CalendarFocus : IScreenObserver
{
    /// <summary>The Calendar: its class on build 548ddc95, as the mod's cursor hook logs it
    /// (tests/fixtures/sessions/title-file-during-play.txt).</summary>
    public static Screen Screen { get; } = new("Calendar", "CalendarChoice@ope@ui@app");

    // The five weekdays, by grid column (index mod 5): the columns' headings are serials 9290 to 9294, whose text the log
    // never records, so the names are as heard in session 2026-11-22 "c" (issue #193), exact spelling
    // [NEEDS VERIFICATION].
    private static readonly string[] Weekdays = ["Flames Day", "Waters Day", "Arbors Day", "Metals Day", "Idols Day"];

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
        var said = $"{Weekdays[change.Index % Weekdays.Length]}, day {day}";
        if (change.Change == ScreenChange.Entered)
        {
            said = $"Calendar, {said}";
        }
        announcer.Announce(new Announcement(said, Interrupt: true));
    }
}
