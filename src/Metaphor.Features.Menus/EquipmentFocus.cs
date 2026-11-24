using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Equipment screen's focus (#217): on entering the character list, "Equipment" and the
/// position, then each move's position ("n of total", or "n" when the game's count can't be read), each cutting off the
/// last, on the characters, the slots, a slot's items and the swap's characters. A character row adds the member's name
/// and archetype, the specific one when the list fetched it (<see cref="ClassNames"/>, #857), row 0 the protagonist's typed name (<see cref="ProtagonistName"/>), since his isn't a message; slot row
/// 0 says the archetype, rows 1 to 4 their label and the item worn in it; entering the swap says its question first.
/// Names come from the block the game fetches for the focused character between the move's cursor records. The item
/// list's names, the "equipped" mark, the sort key and the swap's character are not spoken: where the worn row sits in
/// the list isn't in the fetches, and the swap fetches no name <c>[NEEDS VERIFICATION]</c>. Safe to call from several
/// threads.</summary>
/// <remarks>Classes on build 548ddc95 (docs/raw-sources/screen-class-session-2026-11-17.md): the characters, slots,
/// items and swap, in that order. The block (docs/raw-sources/item-equipment-menus-message-log-2026-11-22.md): the
/// member's <c>CharaName.msg</c> serial (flags 9), the archetype's <c>ATCategoryName.msg</c> serial (flags 8), then each
/// slot's label followed by its worn item's <c>Equip_Message_1.msg</c> name (flags 8)
/// (docs/findings/equipment-character-archetype-serials-static.md). The protagonist's block has no member, and
/// Gallica's no archetype and no slot labels: only her name and worn items (2026-11-23 session, build 0c122d3, Steam
/// 18330018: <c>CharaName_PC11_Gallica</c> then four equipment names, where the archetype-led block kept Basilio's, #216).
/// So any member or archetype fetched after a cursor record starts the next block, and a worn item fills the slot of its
/// kind (<see cref="EquipmentKinds"/>), the slots' order.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.EquipmentMenu)]
public sealed class EquipmentFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver, IScreenDetails
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

    /// <summary>The flags a worn item's description is fetched with, after its name (#907,
    /// docs/findings/item-equipment-menu-names-from-resolves.md).</summary>
    public const int DescriptionFlags = 7;

    /// <summary>Whether the details key says a worn item's description, from its setting; on when not given.</summary>
    public Func<bool> DescriptionsOn { get; init; } = () => true;

    // The swap's question, MainMenu_Equip_25 (serial 1478, System_Message.msg), which the game fetches as the swap opens
    // (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md). The characters' names aren't known yet.
    private const int SwapQuestionSerial = 1478;

    private readonly IAnnouncer announcer;
    private readonly MessageSerials? serials;
    private readonly TextCleaner? cleaner;
    private readonly Lock gate = new();
    private readonly int?[] worn = new int?[SlotRows];

    // The description fetched after each worn item's name (flags 7), and the slot whose name came last (#907).
    private readonly int?[] wornDescriptions = new int?[SlotRows];
    private int? lastWorn;
    private int? member;
    private int? archetype;

    // Whether a block is being fetched: from its first member or archetype until the next cursor record.
    private bool blockOpen;

    // Each row's current archetype, as the character list fetches them all in row order before a cursor record, each
    // from ClassName.msg with the member flags (#857, docs/findings/class-name-and-battle-item-tab-serials.md).
    private readonly List<int> classes = [];
    private bool classRunOpen;

    // The character row last focused, whose slots the slot list shows.
    private int character;

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

    /// <summary>The protagonist's name as the player typed it, or null when it can't be read; row 0 of the character
    /// list says it before his archetype.</summary>
    public Func<string?>? ProtagonistName { get; init; }

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
        var kind = resolve.Flags == NameFlags ? EquipmentKinds.Of(serials, serial) : null;
        lock (gate)
        {
            if (resolve.Flags == MemberFlags && serials.InFile(serial, MessageSerials.ClassNames))
            {
                if (!classRunOpen)
                {
                    classes.Clear();
                    classRunOpen = true;
                }
                classes.Add(serial);
            }
            else if (resolve.Flags == MemberFlags && serials.InFile(serial, MessageSerials.CharacterNames))
            {
                StartBlock(serial);
            }
            else if (resolve.Flags == NameFlags && serials.InFile(serial, MessageSerials.ArchetypeNames))
            {
                // The member's block goes on with their archetype; one that has its archetype, or none open, is the
                // protagonist's, which fetches no member.
                if (!blockOpen || archetype is not null)
                {
                    StartBlock(null);
                }
                archetype = serial;
            }
            else if (blockOpen && kind is { } slot && worn[slot] is null)
            {
                worn[slot] = serial;
                lastWorn = slot;
            }
            else if (blockOpen && resolve.Flags == DescriptionFlags && lastWorn is { } described
                && serials.InFile(serial, MessageSerials.EquipmentDescriptions))
            {
                // A worn item's description comes right after its name (#907).
                wornDescriptions[described] = serial;
                lastWorn = null;
            }
        }
    }

    /// <summary>The description of the item worn in the focused slot, for the details key (#907): the one the game fetched
    /// after its name. Null on another screen or row, for a slot with none, or when it is off. The slot's item list
    /// names no row, so it has no description to say <c>[NEEDS VERIFICATION]</c>.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (serials is null || focus.Screen != Slots || !DescriptionsOn() || focus.Index < 1 || focus.Index > SlotRows)
        {
            return null;
        }
        int? description;
        lock (gate)
        {
            description = wornDescriptions[focus.Index - 1];
        }
        return description is { } known ? serials.TextOf(known, cleaner!) : null;
    }

    /// <summary>Ends the block being fetched: the item list fetches equipment names too, which aren't worn.</summary>
    public void Observe(CursorInput input)
    {
        lock (gate)
        {
            blockOpen = false;
            classRunOpen = false;
        }
    }

    private void StartBlock(int? memberSerial)
    {
        member = memberSerial;
        archetype = null;
        Array.Clear(worn);
        Array.Clear(wornDescriptions);
        lastWorn = null;
        blockOpen = true;
    }

    // "<names>, n of total", each name left out when it isn't known.
    private string Line(ScreenEvent change)
    {
        if (serials is null)
        {
            return change.Positioned();
        }
        int?[] names;
        string? current = null;
        lock (gate)
        {
            if (change.Screen == Characters)
            {
                character = change.Index;
            }
            var archetypeRow = change.Screen == Characters || (change.Screen == Slots && change.Index == 0);
            if (archetypeRow)
            {
                // The row's own archetype when its line is the one fetched for it, else the line (#857).
                current = ClassNames.Current(
                    serials, character < classes.Count ? classes[character] : null, archetype, cleaner!);
            }
            names = change.Screen == Characters ? [change.Index == 0 ? null : member]
                : change.Screen != Slots || change.Index == 0 ? []
                : change.Index <= SlotRows ? [FirstSlotSerial + change.Index - 1, worn[change.Index - 1]]
                : [];
        }
        var protagonist = change.Screen == Characters && change.Index == 0 ? ProtagonistName?.Invoke() : null;
        string?[] said = [protagonist, .. names.Select(serial => serial is { } known ? serials.TextOf(known, cleaner!) : null)];
        return change.Positioned([.. said, current]);
    }
}
