using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the shop's screens by name (#736, #197): the top menu on entering it "Shop", then each row's name and
/// position ("Buy, 1 of 4"); the Buy and Sell lists each row's name and position ("Tonic, 3 of 37"), with the Sell tab's
/// name on opening and on each tab switch; and the purchase's confirm each row's name ("Buy, 1 of 2"), each cutting off
/// the last. The top menu's rows are Buy, Sell, Appraise and Listen, by the labels the game fetches for them,
/// <c>System_General_12</c> to <c>_14</c> and <c>Shop_Menu_03</c> (serials 1491 to 1493 and 1362). A list row's name is
/// the name the view fetched by serial (flags 8) for its place in the view (<see cref="FetchedRows"/>), weapons and
/// armour from <see cref="MessageSerials.EquipmentNames"/>; a row whose name has no known text is said by its position.
/// A Sell tab is told by its rows' item or equipment names, or, when they say none, by its place next to the last tab
/// (<see cref="ListTabs"/>). Not
/// spoken, <c>[NEEDS VERIFICATION]</c>: price, owned count, quantity, ATK and HIT. Safe to call from several
/// threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018): <c>ItemShopMenuChoice</c> (4 rows), <c>ItemShopChoice</c> (the Buy
/// list, or a Sell tab) and <c>ItemShopConfirmChoice</c> (docs/findings/shop-rows-quantity-and-rank-up-notice-static.md;
/// docs/raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md). The Sell tabs, cycled with the shoulder buttons, are
/// <c>Item_Tab_03</c>, <c>_01</c>, <c>_06</c>, <c>_07</c>, <c>_08</c>, <c>_09</c> (Exchange, Use, then the four equipment
/// tabs), and the Exchange tab's first row is "Sell All" (<c>Shop_Other_41</c>, serial 1412)
/// (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md). The confirm's rows are the
/// <c>System_Window_</c> labels fetched as it opens, in fetch order (<c>System_Window_02</c>, then <c>_01</c>, Buy then
/// Cancel); that this is their row order is <c>[NEEDS VERIFICATION]</c>.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.ShopMenu)]
public sealed class ShopFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver
{
    /// <summary>The flags a view fetches its row names with.</summary>
    public const int NameFlags = 8;

    // System_General_12 to _14 and Shop_Menu_03: Buy, Sell, Appraise, Listen.
    private static readonly int[] TopSerials = [1491, 1492, 1493, 1362];
    private const int SellRow = 1;
    private const int SellAllSerial = 1412;

    // The Sell tabs' names in tab order (Item_Tab_03, _01, _06 to _09), and the tab names' serials.
    private static readonly int[] SellTabSerials = [1371, 1369, 1374, 1375, 1376, 1377];
    private static readonly HashSet<int> NamedTabs = [0, 1];
    private const int FirstEquipmentTab = 2;
    private const int FirstTabSerial = 1369;
    private const int LastTabSerial = 1379;
    private const string ConfirmRowLabel = "System_Window_";

    private const string SystemFile = "message/system/System_Message.msg";
    private const string SystemPcFile = "message/system/System_Message_PC.msg";

    /// <summary>The shop's top menu, by its owner's class on build 548ddc95.</summary>
    public static Screen Top { get; } = new("Shop", "ItemShopMenuChoice@ope@ui@app");

    /// <summary>The Buy list or a Sell tab, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Shop items", "ItemShopChoice@ope@ui@app");

    /// <summary>The two-row confirm after a purchase's quantity, by its owner's class on build 548ddc95.</summary>
    public static Screen Confirm { get; } = new("Shop confirm", "ItemShopConfirmChoice@ope@ui@app");

    /// <summary>The shop screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Top, List, Confirm];

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly FetchedRows rows = new(List.ScreenClass);
    private readonly FetchedRows confirmRows = new(Confirm.ScreenClass);
    private readonly ListTabs tabs;
    private readonly Lock gate = new();
    private int topIndex = -1;
    private bool selling;
    private string? tabToSay;

    public ShopFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        tabs = new ListTabs(SellTabSerials.Length, NamedTabs, TabOfRow);
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
        string? tab = null;
        int? serial;
        if (change.Screen == Top)
        {
            lock (gate)
            {
                topIndex = change.Index;
            }
            serial = change.Index < TopSerials.Length ? TopSerials[change.Index] : null;
        }
        else if (change.Screen == Confirm)
        {
            serial = confirmRows.SerialAt(change.Index);
        }
        else
        {
            lock (gate)
            {
                tab = tabToSay;
                tabToSay = null;
            }
            serial = rows.SerialAt(change.Index);
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == Top ? $"{Top.Name}, " : "";
        announcer.Announce(new Announcement(heading + Line(tab, serial, change), Interrupt: true));
    }

    /// <summary>Takes each row name the game fetches, the tab names that mark a Sell batch, and the confirm's rows.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Serial is >= FirstTabSerial and <= LastTabSerial)
        {
            rows.Marked();
            return;
        }
        if (resolve.Flags != NameFlags)
        {
            return;
        }
        if (IsRowName(resolve.Serial))
        {
            rows.Fetched(resolve.Serial);
        }
        else if (serials.FindBySerial(resolve.Serial)?.Label.StartsWith(ConfirmRowLabel, StringComparison.Ordinal) == true)
        {
            confirmRows.Fetched(resolve.Serial);
        }
    }

    /// <summary>Takes the list's and the confirm's views from each settled cursor input, before the menu screens tell
    /// it, and says a Sell tab switch that leaves the focus on its row, which they don't tell.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        confirmRows.Cursor(input);
        if (rows.Cursor(input) is not { } view)
        {
            return;
        }
        bool sell;
        lock (gate)
        {
            if (view.NewVisit)
            {
                selling = topIndex == SellRow;
                tabs.Reset();
            }
            sell = selling;
        }
        if (!view.Switched || !sell)
        {
            return;
        }
        var tab = tabs.Showing(view.Rows) is { } shown ? serials.TextOf(SellTabSerials[shown], cleaner) : null;
        if (view.SameRow)
        {
            var change = new ScreenEvent(ScreenChange.FocusMoved, List, List.ScreenClass, input.Index, input.Count);
            announcer.Announce(new Announcement(Line(tab, rows.SerialAt(input.Index), change), Interrupt: true));
            return;
        }
        lock (gate)
        {
            tabToSay = tab;
        }
    }

    // "<tab>, <row>, n of total", each name left out when it isn't known.
    private string Line(string? tab, int? serial, ScreenEvent change)
    {
        var line = ListFocus.Position(change);
        if (serial is { } known && serials.TextOf(known, cleaner) is { } name)
        {
            line = $"{name}, {line}";
        }
        return tab is null ? line : $"{tab}, {line}";
    }

    // The Sell tab a row name says: Exchange for "Sell All" and the CASH group, Use for any other item group, an
    // equipment tab for an equipment name's kind; null for any other name.
    private int? TabOfRow(int serial)
    {
        if (serial == SellAllSerial)
        {
            return 0;
        }
        if (EquipmentKinds.Of(serials, serial) is { } kind)
        {
            return FirstEquipmentTab + kind;
        }
        if (!serials.InFile(serial, MessageSerials.ItemNames)
            || serials.FindBySerial(serial)?.Label.Split('_') is not [_, var group, ..])
        {
            return null;
        }
        return group == "CASH" ? 0 : 1;
    }

    // A name serial (flags 8) outside the system message files is a row's: the prompts and column labels fetched beside
    // them (Shop_Other_17 and the like) are in those files. "Sell All" is the one row from them.
    private bool IsRowName(int serial) =>
        serial == SellAllSerial || !(serials.InFile(serial, SystemFile) || serials.InFile(serial, SystemPcFile));
}
