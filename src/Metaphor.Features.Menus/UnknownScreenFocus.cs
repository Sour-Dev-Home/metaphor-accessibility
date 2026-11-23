using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the focus on a screen the mod doesn't know yet (#97, spec #211), so the player is never in silence:
/// on entering it and on each focus move, "n of total", each cutting off the last (#289). The total is the list's item
/// count the game keeps (<see cref="ScreenEvent.Total"/>); when it can't be read, or can't hold the focus, the position
/// alone is said. Known screens are their features' to speak, and dialogue choices
/// (<see cref="DialogueChoice"/>, known too) are <c>ChoiceFocus</c>'s (#95). Safe to call from several threads.</summary>
[FeatureSetting(Feature.Menus)]
public sealed class UnknownScreenFocus : IScreenObserver
{
    /// <summary>A dialogue's reply choice: a known screen that no menu feature speaks, as the dialogue feature's
    /// <c>ChoiceFocus</c> does (#95). Its class on build 548ddc95: every choice requested in the run was followed by its
    /// cursor (docs/raw-sources/main-menu-session-2026-11-17.md).</summary>
    public static Screen DialogueChoice { get; } = new("Dialogue choice", "CommonTalkChoice@ope@ui@app");

    /// <summary>An opened quest (#194): a known screen that nothing speaks yet. Its cursor's count was 9 in session
    /// 2026-11-22 "c", not the quest's, so the position would be wrong; the quest's description is fetched by a serial
    /// whose text no known file holds <c>[NEEDS VERIFICATION]</c>. Its class on build 548ddc95.</summary>
    public static Screen QuestDetail { get; } = new("Quest detail", "CampQuestDetailChoice@ope@ui@app");

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
        if (change.Screen is not null || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var position = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        announcer.Announce(new Announcement(position, Interrupt: true));
    }
}
