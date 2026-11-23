using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Equipment slot list's names (#217): rows 1 to 4 are System_General_30 to _33, serials 1511 to 1514
/// (docs/findings/item-equipment-menu-names-from-resolves.md), each with the item worn in it; row 0 is the character's
/// archetype, and a character row names the member and their archetype, from the block the game fetches before the
/// cursor record. The item list and the swap stay by position. Hook events in, announcements out; made-up file text
/// only.</summary>
[Trait("Category", "Unit")]
public sealed class EquipmentSlotNamesTests
{
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const string CharactersClass = "EquipCharaSelChoice@ope@ui@app";
    private const string SwapClass = "EquipSwapCharaSelChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int First = 1470;
    private const int Count = 50;

    private const int Strohl = 2;
    private const int Seeker = 9441;
    private const int Gunner = 9447;
    private const int Sword = 2500;
    private const int Mail = 2501;
    private const int SlotLabel = 1511;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly MessageEvents messages = new();

    public EquipmentSlotNamesTests()
    {
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", First, Count),
            new SerialRange(MessageSerials.CharacterNames, 0, 3),
            new SerialRange(MessageSerials.EquipmentNames, Sword, 2),
            new SerialRange(MessageSerials.ArchetypeNames, Seeker, 7),
        ]);
        messages.ObserveFiles(serials);
        var source = string.Join("\n\n", Enumerable.Range(0, Count).Select(at => Entry($"System_General_{at:00}", $"Text {at + First}")));
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 21, source));
        messages.Compiled(new MessageFileCompiled("EN/message/CharaName.msg", 1,
            string.Join("\n\n", Entry("CharaName_PC01_Hero", "Protagonist"), Entry("CharaName_PC01_Hero_01", "Hero"),
                Entry("CharaName_PC02_Stroll", "Strohl"))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Equip_Message_1.msg", 12,
            string.Join("\n\n", Entry("WeaponName_SSWORD_01", "Sword"), Entry("ArmorName_Heavy_01", "Mail"))));
        messages.Compiled(new MessageFileCompiled("EN/message/ATCategoryName.msg", 30, string.Join("\n\n",
            Enumerable.Range(0, 7).Select(at => Entry($"ATCategoryName_{at + 1:00}", at == 0 ? "Seeker" : at == 6 ? "Gunner" : $"Lineage {at}")))));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        var focus = new EquipmentFocus(serials, announcer, new TextCleaner(TextCleanerRules.None));
        screens.Observe(focus);
        messages.ObserveSerials(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    // A character's block as the game fetches it before the cursor record: the member (none for the protagonist), the
    // archetype, then each slot label followed by the worn item's name and description.
    private void Block(int? member, int archetype, params (int Label, int Item)[] slots)
    {
        if (member is { } serial)
        {
            messages.Resolved(new SerialResolved(serial, 9));
        }
        messages.Resolved(new SerialResolved(archetype, 8));
        foreach (var (label, item) in slots)
        {
            messages.Resolved(new SerialResolved(label, 21));
            messages.Resolved(new SerialResolved(item, 8));
            messages.Resolved(new SerialResolved(item + 1206, 7));
        }
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void SlotRowsOneToFourAreNamedFromSystemGeneral30To33()
    {
        Cursor(SlotsClass, 1, count: 5);
        Cursor(SlotsClass, 4, count: 5);

        Assert.Equal(["Text 1511, 2 of 5", "Text 1514, 5 of 5"], Said());
    }

    [Fact]
    public void TheFirstSlotRowIsSaidByPositionBeforeAnArchetypeIsFetched()
    {
        Cursor(SlotsClass, 0, count: 5);

        Assert.Equal(["1 of 5"], Said());
    }

    // #217: the archetype row said its position only.
    [Fact]
    public void TheFirstSlotRowIsTheCharactersArchetype()
    {
        Block(Strohl, Gunner, (SlotLabel, Sword));
        Cursor(SlotsClass, 0, count: 5);

        Assert.Equal(["Gunner, 1 of 5"], Said());
    }

    // #217: the slots said their labels but not the worn items.
    [Fact]
    public void ASlotRowSaysTheItemWornInIt()
    {
        Block(Strohl, Gunner, (SlotLabel, Sword), (SlotLabel + 1, Mail));
        Cursor(SlotsClass, 1, count: 5);
        Cursor(SlotsClass, 2, count: 5);

        Assert.Equal(["Text 1511, Sword, 2 of 5", "Text 1512, Mail, 3 of 5"], Said());
    }

    [Fact]
    public void AnEmptySlotSaysItsLabelOnly()
    {
        Block(Strohl, Gunner, (SlotLabel, Sword));
        messages.Resolved(new SerialResolved(SlotLabel + 1, 21));
        Cursor(SlotsClass, 2, count: 5);

        Assert.Equal(["Text 1512, 3 of 5"], Said());
    }

    [Fact]
    public void EnteringTheSwapSaysItsQuestionThenThePosition()
    {
        Cursor(SlotsClass, 1, count: 5);
        Cursor(SwapClass, 0, count: 1);

        Assert.Equal("Text 1478, 1 of 1", Said().Last());
    }

    [Fact]
    public void ACharacterIsSaidByPositionBeforeTheirBlockIsFetched()
    {
        Cursor(CharactersClass, 1, count: 8);

        Assert.Equal(["Equipment, 2 of 8"], Said());
    }

    // #217: the character rows said positions only. Row 0 is the protagonist, whose name isn't a message, so a member
    // fetched before its block (the entry's run) isn't theirs.
    [Fact]
    public void ACharacterRowSaysTheMemberAndTheirArchetype()
    {
        messages.Resolved(new SerialResolved(Strohl, 9));
        Block(null, Seeker);
        Cursor(CharactersClass, 0, count: 8);
        Block(Strohl, Gunner);
        Cursor(CharactersClass, 1, count: 8);

        Assert.Equal(["Equipment, Seeker, 1 of 8", "Strohl, Gunner, 2 of 8"], Said());
    }
}
