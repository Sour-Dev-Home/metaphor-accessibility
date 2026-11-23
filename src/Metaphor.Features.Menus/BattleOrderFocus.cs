using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks battle's Order menu (#771), the one LB opens in a battle, and its Tactics submenu: on entering either
/// its heading, the focused row's name and its position, then each move's name and position ("Escape, 3 of 3"), each
/// cutting off the last. A row whose name isn't known is said by its position. Safe to call from several threads.
/// </summary>
/// <remarks>
/// <para>On build 548ddc95 (Steam build 18330018) the Order menu is <c>BTL_OrderTop</c> with 3 rows and the Tactics
/// submenu <c>BTL_OrderTactics</c> with 6, both shown while the menus read closed (the by-ear session of 2026-11-23, F8
/// mark 8; docs/raw-sources/battle-order-menu-log-2026-11-23.md).</para>
/// <para>The Order menu's rows are named by serials 2068 to 2070 (docs/findings/battle-order-menu-rows-static.md), but
/// the file holding them isn't one the mod knows: the log gives them no label. So its rows are the mod's own words, in
/// the cursor order the maintainer heard and saw (Party, Tactics, Escape); a game label found later replaces them
/// (#248). The Tactics rows are the game's own: the six <c>System_Battle_Tactics</c> names, in the order the submenu
/// fetched them as it scrolled, which is the cursor order the maintainer heard (<c>[NEEDS VERIFICATION]</c> on another
/// party or language).</para>
/// </remarks>
[FeatureSetting<MenusFeature>(MenusFeature.BattleOrderMenu)]
public sealed class BattleOrderFocus : IScreenObserver
{
    // System_Battle_Tactics_02, _01, _03 to _06, in cursor order.
    private static readonly int[] TacticsSerials = [1537, 1536, 1538, 1539, 1540, 1541];

    /// <summary>The mod's own English wording for the Order menu's rows (#771), in cursor order on build 548ddc95. Not
    /// game content; ready for translation.</summary>
    internal static IReadOnlyList<string> OrderLabels { get; } = ["Party", "Tactics", "Escape"];

    /// <summary>Battle's Order menu, by its owner's class on build 548ddc95.</summary>
    public static Screen Orders { get; } = new("Orders", "BTL_OrderTop@ope@ui@app", OutsideMenus: true);

    /// <summary>The Order menu's Tactics submenu, by its owner's class on build 548ddc95.</summary>
    public static Screen Tactics { get; } = new("Tactics", "BTL_OrderTactics@ope@ui@app", OutsideMenus: true);

    /// <summary>Both screens, for the known screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Orders, Tactics];

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public BattleOrderFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when either screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var said = change.Screen == Orders ? Line(change, Orders, OrderLabels.Count, index => OrderLabels[index])
            : change.Screen == Tactics ? Line(change, Tactics, TacticsSerials.Length,
                index => serials.TextOf(TacticsSerials[index], cleaner))
            : null;
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    // A list of another length than the rows known (say, a row the game left out) is said by position alone, never by
    // another row's name.
    private static Announcement? Line(ScreenEvent change, Screen screen, int rows, Func<int, string?> name)
    {
        var count = change.Total ?? rows;
        return ListFocus.Of(change, screen, screen.Name, count, index => count == rows ? name(index) : null);
    }
}
