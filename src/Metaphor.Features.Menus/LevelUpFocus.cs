using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the level-up point screen (#624): on entering, the game's heading ("Distribute Remaining Points"),
/// then each row's stat and position, each cutting off the last; the details key reads the stat's description. Off
/// (<paramref name="on"/>), "Level up" and positions only. Not said yet, <c>[NEEDS VERIFICATION]</c>: the member, the stats'
/// values and the points left, which are fields of the choice that need a live check
/// (docs/findings/level-up-point-distribution-choice-static.md); a Debug build logs them for the next session. Safe to
/// call from several threads.</summary>
/// <remarks>On build 548ddc95 the screen is <c>ResultDistributionPointChoice</c>, five rows; as it opens the game
/// fetches <c>System_LVUP_GUIDE</c> (serial 1747), the five stats' short names <c>System_General_05</c> to <c>_09</c> and
/// their descriptions <c>System_LVUP_POW</c> to <c>_LUK</c> (serials 1752 to 1756), in the stats' order (#624's by-ear
/// session of 2026-11-22). That the rows are in that order is the finding's lead, <c>[NEEDS VERIFICATION]</c> by ear. The
/// short names are abbreviations a screen reader says badly, so the rows use the stats' full names.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.LevelUpPoints)]
public sealed class LevelUpFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<bool> on)
    : IScreenObserver, IScreenDetails
{
    private const int HeadingSerial = 1747;
    private const int FirstDescriptionSerial = 1752;

    // The stats in the order of System_General_05 to _09: St, Ma, En, Ag, Lk.
    private static readonly string[] Stats = ["Strength", "Magic", "Endurance", "Agility", "Luck"];

    /// <summary>The point screen, by its owner's class on build 548ddc95. It follows a battle's results, where the menus
    /// may read closed, so it is told then too <c>[NEEDS VERIFICATION]</c>.</summary>
    public static Screen Screen { get; } = new("Level up", "ResultDistributionPointChoice@ope@ui@app", OutsideMenus: true);

    /// <summary>Speaks the row on entering the screen or moving its focus.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var named = on();
        var row = change.Positioned(named && change.Index < Stats.Length ? Stats[change.Index] : null);
        var heading = change.Change != ScreenChange.Entered ? null
            : (named ? serials.TextOf(HeadingSerial, cleaner) : null) ?? Screen.Name;
        announcer.Announce(new Announcement(heading is null ? row : $"{heading}, {row}", Interrupt: true));
    }

    /// <summary>The focused stat's description, or null on another screen.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        return focus.Screen == Screen && on() && focus.Index >= 0 && focus.Index < Stats.Length
            ? serials.TextOf(FirstDescriptionSerial + focus.Index, cleaner)
            : null;
    }
}
