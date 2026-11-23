using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Equipment screen's focus (#217): on entering the character list, "Equipment" and the
/// position, then each move's position ("n of total", or "n" when the game's count can't be read), each cutting off the
/// last, on the characters, the slots, a slot's items and the swap's characters. A character row adds the member's name
/// (none for row 0, the protagonist, whose name isn't a message) and archetype; slot row 0 says the archetype, rows 1 to
/// 4 their label and the item worn in it; entering the swap says its question first. Names come from the block the game
/// fetches for the focused character just before its cursor record. The item list's names, the "equipped" mark and the
/// sort key are not spoken: where the worn row sits in the list isn't in the fetches <c>[NEEDS VERIFICATION]</c>. Safe to
/// call from several threads.</summary>
/// <remarks>Classes on build 548ddc95 (docs/raw-sources/screen-class-session-2026-11-17.md): the characters, slots,
/// items and swap, in that order. The block (docs/raw-sources/item-equipment-menus-message-log-2026-11-22.md): the
/// member's <c>CharaName.msg</c> serial (flags 9), the archetype's <c>ATCategoryName.msg</c> serial (flags 8), then each
/// slot's label followed by its worn item's <c>Equip_Message_1.msg</c> name (flags 8)
/// (docs/findings/equipment-character-archetype-serials-static.md).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.EquipmentMenu)]
public sealed class EquipmentFocus : IScreenObserver, ISerialResolveObserver
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
    // after each character's archetype name (docs/findings/item-equipment-menu-names-from-resolves.md).
    private const int FirstSlotSerial = 1511;
    private const int SlotRows = 4;

    // The flags the block fetches the member's name, and the archetype's and worn items' names, with.
    private const int MemberFlags = 9;
    private const int NameFlags = 8;

    // The swap's question, MainMenu_Equip_25 (serial 1478, System_Message.msg), which the game fetches as the swap opens
    // (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md). The characters' names aren't known yet.
    private const int SwapQuestionSerial = 1478;

    private readonly IAnnouncer announcer;
    private readonly MessageSerials? serials;
    private readonly TextCleaner? cleaner;
    private readonly Lock gate = new();
    private readonly int?[] worn = new int?[SlotRows];
    private int? member;
    private int? fetchedMember;
    private int? archetype;
    private int? slotFetching;

    public EquipmentFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>The Equipment screens with the rows named from the names the game fetches.</summary>
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
        var heading = change.Change != ScreenChange.Entered ? ""
            : change.Screen == Characters ? $"{Characters.Name}, "
            : change.Screen == Swap && serials?.TextOf(SwapQuestionSerial, cleaner!) is { } question
                ? question + (question[^1] is '.' or '?' or '!' ? " " : ", ")
                : "";
        announcer.Announce(new Announcement(heading + Line(change), Interrupt: true));
    }

    /// <summary>Takes the focused character's block: the member, the archetype and each slot's worn item.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (serials is null)
        {
            return;
        }
        var serial = resolve.Serial;
        lock (gate)
        {
            if (resolve.Flags == MemberFlags && serials.InFile(serial, MessageSerials.CharacterNames))
            {
                fetchedMember = serial;
            }
            else if (resolve.Flags == NameFlags && serials.InFile(serial, MessageSerials.ArchetypeNames))
            {
                // A new block: its member is the one fetched just before, and its slots are fetched after.
                archetype = serial;
                member = fetchedMember;
                fetchedMember = null;
                slotFetching = null;
                Array.Clear(worn);
            }
            else if (serial is >= FirstSlotSerial and < FirstSlotSerial + SlotRows)
            {
                slotFetching = serial - FirstSlotSerial;
                worn[serial - FirstSlotSerial] = null;
            }
            else if (resolve.Flags == NameFlags && slotFetching is { } slot
                && serials.InFile(serial, MessageSerials.EquipmentNames))
            {
                worn[slot] = serial;
                slotFetching = null;
            }
        }
    }

    // "<names>, n of total", each name left out when it isn't known.
    private string Line(ScreenEvent change)
    {
        var position = ListFocus.Position(change);
        if (serials is null)
        {
            return position;
        }
        int?[] names;
        lock (gate)
        {
            names = change.Screen == Characters ? [change.Index == 0 ? null : member, archetype]
                : change.Screen != Slots ? []
                : change.Index == 0 ? [archetype]
                : change.Index <= SlotRows ? [FirstSlotSerial + change.Index - 1, worn[change.Index - 1]]
                : [];
        }
        var said = names.Select(serial => serial is { } known ? serials.TextOf(known, cleaner!) : null)
            .OfType<string>()
            .Append(position);
        return string.Join(", ", said);
    }
}
