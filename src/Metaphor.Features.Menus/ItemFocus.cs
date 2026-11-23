using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Item screen's focus (#216): on entering the item list, "Item", the tab's name when known, the
/// focused row's name when known and its position; then each move's name and position ("n of total", or "n" when the
/// game's count can't be read), and on a tab switch the tab's name first, each cutting off the last; the same on the
/// party-member target list, without the heading. A row's name is the name the game fetched by serial for its place in
/// the view (<see cref="FetchedRows"/>). A row whose name has no known text (the equipment tabs', whose file isn't known)
/// is said by its position. The tab is told by its rows' item names, or, for the four equipment tabs, by its place next to
/// the last tab (<see cref="ListTabs"/>). Not spoken, <c>[NEEDS VERIFICATION]</c>: quantities (no record carries one) and
/// target names (no fetch names a member). Safe to call from several threads.</summary>
/// <remarks>Classes on build 548ddc95: <c>ItemMenuChoice</c> and <c>ItemCharaSelChoice</c>, the list and the target
/// (docs/raw-sources/screen-class-session-2026-11-17.md). Each opening, tab switch and move fetches the rows on view
/// (flags 9), a run of small serials (2 to 21), then the eight tab names (serials 1369 to 1379, flags 2) in tab order;
/// the item name groups per tab are in docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.ItemMenu)]
public sealed class ItemFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver
{
    /// <summary>The flags the list fetches a row's name with.</summary>
    public const int NameFlags = 9;

    // The tab names, Item_Tab_01 to _11, in System_Message.msg (item-equipment-menu-names-from-resolves.md), and the
    // eight the list shows in tab order: Use, Exchange, Key Items, Information, Weapons, Armour, Gear, Accessories.
    private const int FirstTabSerial = 1369;
    private const int LastTabSerial = 1379;
    private static readonly int[] TabSerials = [1369, 1371, 1372, 1379, 1374, 1375, 1376, 1377];
    private static readonly HashSet<int> NamedTabs = [0, 1, 2, 3];

    // Serials below System_Message.msg's first are the small run fetched after the rows, not rows.
    private const int FirstRowSerial = 1142;

    /// <summary>The Item list, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Item", "ItemMenuChoice@ope@ui@app");

    /// <summary>The party-member target list, by its owner's class on build 548ddc95.</summary>
    public static Screen Target { get; } = new("Item target", "ItemCharaSelChoice@ope@ui@app");

    /// <summary>Both Item screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, Target];

    private readonly IAnnouncer announcer;
    private readonly MessageSerials? serials;
    private readonly TextCleaner? cleaner;
    private readonly FetchedRows rows = new(List.ScreenClass);
    private readonly ListTabs tabs;
    private readonly Lock gate = new();
    private string? tabToSay;

    /// <summary>The Item screens spoken by position alone.</summary>
    public ItemFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
        tabs = new ListTabs(TabSerials.Length, NamedTabs, TabOfRow);
    }

    /// <summary>The Item screens with each list row named from the item names the game fetched.</summary>
    public ItemFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
        : this(announcer)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when an Item screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is null || !Screens.Contains(change.Screen) || change.Change == ScreenChange.Left
            || change.Index < 0)
        {
            return;
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == List ? $"{List.Name}, " : "";
        if (change.Screen != List)
        {
            announcer.Announce(new Announcement(heading + ListFocus.Position(change), Interrupt: true));
            return;
        }
        string? tab;
        lock (gate)
        {
            tab = tabToSay;
            tabToSay = null;
        }
        announcer.Announce(new Announcement(heading + RowLine(tab, change), Interrupt: true));
    }

    /// <summary>Takes each row name the game fetches, and notes the tab names that end the list's batch.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (serials is null)
        {
            return;
        }
        if (resolve.Serial is >= FirstTabSerial and <= LastTabSerial)
        {
            rows.Marked();
        }
        else if (resolve.Flags == NameFlags && (serials.InFile(resolve.Serial, MessageSerials.ItemNames)
            || (resolve.Serial >= FirstRowSerial && !InSystemFiles(resolve.Serial))))
        {
            rows.Fetched(resolve.Serial);
        }
    }

    /// <summary>Takes the list's view from each settled cursor input, before the menu screens tell it, and says a tab
    /// switch that leaves the focus on its row, which they don't tell.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (serials is null || rows.Cursor(input) is not { } view)
        {
            return;
        }
        if (view.NewVisit)
        {
            tabs.Reset();
        }
        if (!view.Switched)
        {
            return;
        }
        var tab = view.Tabbed && tabs.Showing(view.Rows) is { } shown ? serials.TextOf(TabSerials[shown], cleaner!) : null;
        if (view.SameRow)
        {
            var change = new ScreenEvent(ScreenChange.FocusMoved, List, List.ScreenClass, input.Index, input.Count);
            announcer.Announce(new Announcement(RowLine(tab, change), Interrupt: true));
            return;
        }
        lock (gate)
        {
            tabToSay = tab;
        }
    }

    // "<tab>, <row>, n of total", each name left out when it isn't known.
    private string RowLine(string? tab, ScreenEvent change)
    {
        var line = ListFocus.Position(change);
        if (rows.SerialAt(change.Index) is { } serial && serials?.TextOf(serial, cleaner!) is { } name)
        {
            line = $"{name}, {line}";
        }
        return tab is null ? line : $"{tab}, {line}";
    }

    // The tab an item name's group says: Exchange for CASH, Key Items for Scenario, Insects and Follower, Information for
    // Info, Use for any other item group; null for a name outside the item names file, such as an equipment row's.
    private int? TabOfRow(int serial)
    {
        if (serials is null || !serials.InFile(serial, MessageSerials.ItemNames)
            || serials.FindBySerial(serial)?.Label.Split('_') is not [_, var group, ..])
        {
            return null;
        }
        return group switch
        {
            "CASH" => 1,
            "Scenario" or "Insects" or "Follower" => 2,
            "Info" => 3,
            _ => 0,
        };
    }

    private bool InSystemFiles(int serial) =>
        serials!.InFile(serial, "message/system/System_Message.msg")
        || serials.InFile(serial, "message/system/System_Message_PC.msg");
}
