using Metaphor.Core.Announcements;
using Metaphor.Core.Logging;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the focus on a screen the mod doesn't know yet (#97, spec #211), so the player is never in silence:
/// on entering it and on each focus move, "n of total", each cutting off the last (#289). The total is the list's item
/// count the game keeps (<see cref="ScreenEvent.Total"/>); when it can't be read, or can't hold the focus, the position
/// alone is said. Known screens are their features' to speak, and dialogue choices
/// (<see cref="DialogueChoice"/>, known too) are <c>ChoiceFocus</c>'s (#95). Safe to call from several threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.Menus)]
public sealed class UnknownScreenFocus : IScreenObserver
{
    /// <summary>A dialogue's reply choice: a known screen that no menu feature speaks, as the dialogue feature's
    /// <c>ChoiceFocus</c> does (#95). Its class on build 548ddc95: every choice requested in the run was followed by its
    /// cursor (docs/raw-sources/main-menu-session-2026-11-17.md).</summary>
    public static Screen DialogueChoice { get; } = new("Dialogue choice", "CommonTalkChoice@ope@ui@app");

    private readonly IAnnouncer announcer;
    private readonly UnknownValues unknowns;

    /// <param name="announcer">Where the position is said.</param>
    /// <param name="unknowns">Told each unknown screen's class, which it writes once a session (#824).</param>
    public UnknownScreenFocus(IAnnouncer announcer, UnknownValues? unknowns = null)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
        this.unknowns = unknowns ?? UnknownValues.None;
    }

    /// <summary>Speaks the focus when an unknown screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is not null || change.Change == ScreenChange.Left)
        {
            return;
        }
        unknowns.Report("screen class", change.ScreenClass ?? "unreadable");
        if (change.Index < 0)
        {
            return;
        }
        announcer.Announce(new Announcement(change.Position(), Interrupt: true));
    }
}
