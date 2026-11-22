using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Item screen's focus (#216): on entering the item list, "Item", the focused row's name when
/// known and its position, then each move's name and position ("n of total", or "n" when the game's count can't be
/// read, as on the scrolling list), each cutting off the last; the same on the party-member target list, without the
/// heading. A row's name is the n-th item name the game fetched by serial since the list opened: the list fetches each
/// visible row's name top first, and one more just before the cursor record of each step past them (docs/findings/
/// item-equipment-menu-names-from-resolves.md). A row with no fetched item name (the equipment tabs', whose names are in
/// a file not known yet) is said by its position. Not spoken, <c>[NEEDS VERIFICATION]</c>: the active tab's name (the
/// eight tab names are fetched in a fixed order but no record says which is active), quantities (no record carries
/// one) and target names (no fetch names a member). Safe to call from several threads.</summary>
/// <remarks>Classes on build 548ddc95: <c>ItemMenuChoice</c> and <c>ItemCharaSelChoice</c>, the list and the target
/// (docs/raw-sources/screen-class-session-2026-11-17.md; <c>ItemMenuChoice</c> also in the 2026-11-22 report log). Each
/// list opening fetches its names, then the tab names (serials 1369 to 1379, <c>flags=2</c>); the first name fetched
/// after the tab names starts the next opening's rows.</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class ItemFocus : IScreenObserver, ISerialResolveObserver
{
    /// <summary>The flags the list fetches a row's name with.</summary>
    public const int NameFlags = 9;

    // The tab names, Item_Tab_01 to _11, in System_Message.msg (item-equipment-menu-names-from-resolves.md).
    private const int FirstTabSerial = 1369;
    private const int LastTabSerial = 1379;

    /// <summary>The Item list, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Item", "ItemMenuChoice@ope@ui@app");

    /// <summary>The party-member target list, by its owner's class on build 548ddc95.</summary>
    public static Screen Target { get; } = new("Item target", "ItemCharaSelChoice@ope@ui@app");

    /// <summary>Both Item screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, Target];

    private readonly IAnnouncer announcer;
    private readonly MessageSerials? serials;
    private readonly TextCleaner? cleaner;
    private readonly Lock gate = new();
    private readonly List<int> rows = [];
    private bool namesSinceTabs;
    private bool afterTabs;

    /// <summary>The Item screens spoken by position alone.</summary>
    public ItemFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
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
        var line = ListFocus.Position(change);
        if (change.Screen == List && NameOf(change.Index) is { } name)
        {
            line = $"{name}, {line}";
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == List ? $"{List.Name}, " : "";
        announcer.Announce(new Announcement(heading + line, Interrupt: true));
    }

    /// <summary>Keeps each item name the game fetches, in order. The tab names, fetched after a list's names, close the
    /// opening: a list with no names of its own (an equipment tab) forgets the last opening's rows, and the next name
    /// fetched starts afresh.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (serials is null)
        {
            return;
        }
        if (resolve.Serial is >= FirstTabSerial and <= LastTabSerial)
        {
            lock (gate)
            {
                // The first tab name (Item_Tab_01) starts each opening's batch of tab names.
                if (resolve.Serial == FirstTabSerial)
                {
                    if (!namesSinceTabs)
                    {
                        rows.Clear();
                    }
                    namesSinceTabs = false;
                }
                afterTabs = true;
            }
            return;
        }
        if (resolve.Flags != NameFlags || !serials.InFile(resolve.Serial, MessageSerials.ItemNames))
        {
            return;
        }
        lock (gate)
        {
            if (afterTabs)
            {
                rows.Clear();
                afterTabs = false;
            }
            namesSinceTabs = true;
            if (rows.Count < ScreenEvent.LongestList && !rows.Contains(resolve.Serial))
            {
                rows.Add(resolve.Serial);
            }
        }
    }

    // The name of row `index`, or null when its name wasn't fetched or has no known text.
    private string? NameOf(int index)
    {
        int serial;
        lock (gate)
        {
            if (serials is null || index >= rows.Count)
            {
                return null;
            }
            serial = rows[index];
        }
        return serials.TextOf(serial, cleaner!);
    }
}
