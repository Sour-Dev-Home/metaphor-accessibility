using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the in-game main menu (#97, spec #211): on entering the screen, "Menu" and the focused entry's
/// position, then on each focus move "n of total", each cutting off the last. The entries' labels aren't spoken yet: the
/// menu draws them without a message or a text pane, so where their text comes from is
/// <c>[NEEDS VERIFICATION]</c> (docs/raw-sources/main-menu-session-2026-11-17.md). Safe to call from several threads.
/// </summary>
public sealed class MainMenuFocus : IScreenObserver
{
    /// <summary>The main menu: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md).</summary>
    public static Screen Screen { get; } = new("Menu", "RootMenuChoice@ope@ui@app");

    // Skill, Item, Equipment, Party, Follower, Quest, Calendar, Memorandum, System, in cursor order, on build 548ddc95
    // (docs/raw-sources/main-menu-session-2026-11-17.md).
    private const int EntryCount = 9;

    private readonly IAnnouncer announcer;

    public MainMenuFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focus when the main menu is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index is < 0 or >= EntryCount)
        {
            return;
        }
        var position = $"{change.Index + 1} of {EntryCount}";
        announcer.Announce(new Announcement(
            change.Change == ScreenChange.Entered ? $"{Screen.Name}, {position}" : position, Interrupt: true));
    }
}
