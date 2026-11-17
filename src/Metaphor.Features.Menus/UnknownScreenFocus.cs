using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the focus on a screen the mod doesn't know yet (#97, spec #211), so the player is never in silence:
/// on entering it and on each focus move, the focus's position from 1, each cutting off the last. Spec #211 asks for
/// "n of total", but no hook reads a cursor's item count yet (<c>[NEEDS VERIFICATION]</c>: where the manager keeps it),
/// so the total is left out. Known screens are their features' to speak, and dialogue choices are
/// <c>ChoiceFocus</c>'s (#95). Safe to call from several threads.</summary>
public sealed class UnknownScreenFocus : IScreenObserver
{
    // The screen class of a dialogue's reply choice on build 548ddc95: every choice requested in the run was followed
    // by its cursor (docs/raw-sources/main-menu-session-2026-11-17.md).
    private const string DialogueChoiceClass = "CommonTalkChoice@ope@ui@app";

    private readonly IAnnouncer announcer;

    public UnknownScreenFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focus when an unknown screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is not null || change.ScreenClass == DialogueChoiceClass
            || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        announcer.Announce(new Announcement($"{change.Index + 1}", Interrupt: true));
    }
}
