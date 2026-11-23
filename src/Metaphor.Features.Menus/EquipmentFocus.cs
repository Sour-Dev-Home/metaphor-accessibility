using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Equipment screen's focus (#217): on entering the character list, "Equipment" and the
/// position, then each move's position ("n of total", or "n" when the game's count can't be read), each cutting off the
/// last, on the characters, the slots, a slot's items and the swap's characters. Slot rows 1 to 4 add their name;
/// character names, item names, the "equipped" mark, the archetype row's name and the sort key are not spoken: no finding gives their source
/// <c>[NEEDS VERIFICATION]</c>. Safe to call from several threads.</summary>
/// <remarks>Classes on build 548ddc95 (docs/raw-sources/screen-class-session-2026-11-17.md): the characters, slots,
/// items and swap, in that order. The slot list had 5 rows in the issue's description; the count is the game's.</remarks>
[FeatureSetting(Feature.EquipmentMenu)]
public sealed class EquipmentFocus : IScreenObserver
{
    /// <summary>The character list, by its owner's class on build 548ddc95.</summary>
    public static Screen Characters { get; } = new("Equipment", "EquipCharaSelChoice@ope@ui@app");

    /// <summary>The slot list.</summary>
    public static Screen Slots { get; } = new("Equipment slots", "EquipPartsChoice@ope@ui@app");

    /// <summary>The list of items for a slot.</summary>
    public static Screen Items { get; } = new("Equipment items", "EquipMenuItemList@ope@ui@app");

    /// <summary>The characters a swap can go to.</summary>
    public static Screen Swap { get; } = new("Equipment swap", "EquipSwapCharaSelChoice@ope@ui@app");

    /// <summary>All the Equipment screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Characters, Slots, Items, Swap];

    // The slot rows 1 to 4 are System_General_30 to _33, serials 1511 to 1514 in System_Message.msg, which the game fetches
    // after each character's archetype name (docs/findings/item-equipment-menu-names-from-resolves.md). Row 0, the
    // archetype, has no label yet.
    private const int FirstSlotSerial = 1511;
    private const int SlotRows = 4;

    private readonly IAnnouncer announcer;
    private readonly MessageSerials? serials;
    private readonly TextCleaner? cleaner;

    public EquipmentFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>The Equipment screens with the slot rows named from System_General_30 to _33.</summary>
    public EquipmentFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
        : this(announcer)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when an Equipment screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is null || !Screens.Contains(change.Screen) || change.Change == ScreenChange.Left
            || change.Index < 0)
        {
            return;
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == Characters ? $"{Characters.Name}, " : "";
        var line = ListFocus.Position(change);
        if (change.Screen == Slots && change.Index is >= 1 and <= SlotRows
            && serials?.TextOf(FirstSlotSerial + change.Index - 1, cleaner!) is { } name)
        {
            line = $"{name}, {line}";
        }
        announcer.Announce(new Announcement(heading + line, Interrupt: true));
    }
}
