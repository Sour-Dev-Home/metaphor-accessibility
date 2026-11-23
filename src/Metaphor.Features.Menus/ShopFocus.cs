using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the shop's two lists by name (#736, #197): the top menu on entering it "Shop", then each row's name
/// and position ("Buy, 1 of 4"), and the Buy and Sell lists each row's name and position ("Tonic, 3 of 37"), each
/// cutting off the last. The top menu's rows are Buy, Sell, Appraise and Listen, by the labels the game fetches for them,
/// <c>System_General_12</c> to <c>_14</c> and <c>Shop_Menu_03</c> (serials 1491 to 1493 and 1362). A list row's name is
/// the n-th name the view fetched by serial (flags 8), as the game fetches each view's eight rows in order just before its
/// cursor record; a row whose name wasn't fetched, or whose text isn't known (the weapon and armour names' file isn't),
/// is said by its position. Not spoken, <c>[NEEDS VERIFICATION]</c>: price, owned count, quantity, ATK and HIT, and the
/// Sell tab names. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018): <c>ItemShopMenuChoice</c> (4 rows) and <c>ItemShopChoice</c> (the
/// Buy list, or a Sell tab) (docs/findings/shop-rows-quantity-and-rank-up-notice-static.md; the per-view lookups in
/// docs/raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md). The Sell list's first tab had 6 rows, and "Sell All"
/// (<c>Shop_Other_41</c>, serial 1412, flags 8) was fetched just before its five item names, so it is taken as row 0
/// <c>[NEEDS VERIFICATION]</c>. A view's fetches start a new run when they come more than
/// <see cref="CursorEvents.QuietMilliseconds"/> after the list's last fetch or focus.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.ShopMenu)]
public sealed class ShopFocus : IScreenObserver, ISerialResolveObserver
{
    /// <summary>The flags a view fetches its row names with.</summary>
    public const int NameFlags = 8;

    // System_General_12 to _14 and Shop_Menu_03: Buy, Sell, Appraise, Listen.
    private static readonly int[] TopSerials = [1491, 1492, 1493, 1362];
    private const int SellAllSerial = 1412;

    /// <summary>The shop's top menu, by its owner's class on build 548ddc95.</summary>
    public static Screen Top { get; } = new("Shop", "ItemShopMenuChoice@ope@ui@app");

    /// <summary>The Buy list or a Sell tab, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Shop items", "ItemShopChoice@ope@ui@app");

    /// <summary>Both shop screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Top, List];

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<long> millisecondsNow;
    private readonly Lock gate = new();
    private readonly List<int> rows = [];
    private long lastActivity = long.MinValue / 2;

    public ShopFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<long> millisecondsNow)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(millisecondsNow);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.millisecondsNow = millisecondsNow;
    }

    /// <summary>Speaks the focus when a shop screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is null || !Screens.Contains(change.Screen) || change.Change == ScreenChange.Left
            || change.Index < 0)
        {
            return;
        }
        int? serial = null;
        lock (gate)
        {
            if (change.Screen == Top)
            {
                serial = change.Index < TopSerials.Length ? TopSerials[change.Index] : null;
            }
            else
            {
                lastActivity = millisecondsNow();
                serial = change.Index < rows.Count ? rows[change.Index] : null;
            }
        }
        var line = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        if (serial is { } known && serials.TextOf(known, cleaner) is { } name)
        {
            line = $"{name}, {line}";
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == Top ? $"{Top.Name}, " : "";
        announcer.Announce(new Announcement(heading + line, Interrupt: true));
    }

    /// <summary>Keeps each row name the game fetches, in order. A name fetched long after the last starts a new run.
    /// </summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags != NameFlags || !IsRowName(resolve.Serial))
        {
            return;
        }
        lock (gate)
        {
            var now = millisecondsNow();
            if (now - lastActivity > CursorEvents.QuietMilliseconds)
            {
                rows.Clear();
            }
            lastActivity = now;
            if (rows.Count < ScreenEvent.LongestList && !rows.Contains(resolve.Serial))
            {
                rows.Add(resolve.Serial);
            }
        }
    }

    // A name serial (flags 8) outside the system message files is a row's: the prompts and column labels fetched beside
    // them (Shop_Other_17 and the like) are in those files. "Sell All" is the one row from them.
    private bool IsRowName(int serial) =>
        serial == SellAllSerial
        || !(serials.InFile(serial, "message/system/System_Message.msg")
            || serials.InFile(serial, "message/system/System_Message_PC.msg"));
}
