using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Item and gear descriptions on the details key (#907, ADR 0023): the Item list's opening batch fetches each
/// row's name (flags 9) and then its description (flags 7), top row first, and the Equipment slot block fetches each
/// worn item's name (flags 8) and then its description (flags 7)
/// (docs/findings/item-equipment-menu-names-from-resolves.md). The details key says the one fetched for the focused
/// row, and no description is fetched by the mod. Hook events in, details out; made-up file text and serials.</summary>
[Trait("Category", "Unit")]
public sealed class GearDescriptionsTests
{
    private const string ListClass = "ItemMenuChoice@ope@ui@app";
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int ItemFirst = 300;
    private const int ItemDescriptionFirst = 400;
    private const int Sword = 2500;
    private const int Mail = 2501;
    private const int Seeker = 9441;
    private const int SlotLabel = 1511;
    private const int WornDescriptionOffset = 1206;

    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();
    private readonly ItemFocus items;
    private readonly EquipmentFocus equipment;
    private bool on = true;

    public GearDescriptionsTests()
    {
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", 1470, 50),
            new SerialRange(MessageSerials.EquipmentNames, Sword, 2),
            new SerialRange(MessageSerials.EquipmentDescriptions, Sword + WornDescriptionOffset, 2),
            new SerialRange(MessageSerials.ItemNames, ItemFirst, 3),
            new SerialRange(MessageSerials.ItemDescriptions, ItemDescriptionFirst, 3),
            new SerialRange(MessageSerials.ArchetypeNames, Seeker, 1),
        ]);
        messages.Compiled(new MessageFileCompiled("EN/message/ATCategoryName.msg", 30,
            Entry("ATCategoryName_01", "Seeker")));
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 21, string.Join("\n\n",
            Enumerable.Range(0, 50).Select(at => Entry($"System_General_{at:00}", $"Text {at}")))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Equip_Message_1.msg", 12,
            string.Join("\n\n", Entry("WeaponName_SSWORD_01", "Sword"), Entry("ArmorName_Heavy_01", "Mail"))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Equip_Message_2.msg", 13,
            string.Join("\n\n", Entry("WeaponText_SSWORD_01", "A plain blade"), Entry("ArmorText_Heavy_01", "Heavy armour"))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21,
            string.Join("\n\n", Entry("ItemName_000", "Tonic"), Entry("ItemName_001", "Salve"), Entry("ItemName_002", "Ether"))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_2.msg", 22,
            string.Join("\n\n", Entry("ItemText_000", "Heals a little"), Entry("ItemText_001", "Cures poison"),
                Entry("ItemText_002", "Restores MP"))));
        var cleaner = new TextCleaner(TextCleanerRules.None);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        items = new ItemFocus(serials, new RecordingAnnouncer(), cleaner) { DescriptionsOn = () => on };
        equipment = new EquipmentFocus(serials, new RecordingAnnouncer(), cleaner) { DescriptionsOn = () => on };
        // As MenusModule wires it: each hears the cursor record before the menu screens do.
        cursors.Observe(items);
        cursors.Observe(equipment);
        cursors.Observe(screens);
        screens.Observe(items);
        screens.Observe(equipment);
        messages.ObserveSerials(items);
        messages.ObserveSerials(equipment);
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void ListRow(int name, int description)
    {
        messages.Resolved(new SerialResolved(name, ItemFocus.NameFlags));
        messages.Resolved(new SerialResolved(description, ItemFocus.DescriptionFlags));
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private string? ItemDetails(int index) =>
        items.Details(new ScreenEvent(ScreenChange.FocusMoved, ItemFocus.List, ListClass, index, 3));

    private string? SlotDetails(int index) =>
        equipment.Details(new ScreenEvent(ScreenChange.FocusMoved, EquipmentFocus.Slots, SlotsClass, index, 5));

    [Fact]
    public void AnItemRowsDetailsAreTheDescriptionFetchedAfterItsName()
    {
        ListRow(ItemFirst + 1, ItemDescriptionFirst + 1);
        ListRow(ItemFirst, ItemDescriptionFirst);
        ListRow(ItemFirst + 2, ItemDescriptionFirst + 2);
        Cursor(ListClass, 0, count: 3);

        Assert.Equal("Cures poison", ItemDetails(0));
        Assert.Equal("Heals a little", ItemDetails(1));
        Assert.Equal("Restores MP", ItemDetails(2));
    }

    [Fact]
    public void AnEquipmentRowOnTheItemListSaysItsEquipmentDescription()
    {
        messages.Resolved(new SerialResolved(Sword, ItemFocus.NameFlags));
        messages.Resolved(new SerialResolved(Sword + WornDescriptionOffset, ItemFocus.DescriptionFlags));
        Cursor(ListClass, 0, count: 1);

        Assert.Equal("A plain blade", ItemDetails(0));
    }

    [Fact]
    public void ARowWhoseDescriptionWasNotFetchedSaysNone()
    {
        messages.Resolved(new SerialResolved(ItemFirst, ItemFocus.NameFlags));
        ListRow(ItemFirst + 1, ItemDescriptionFirst + 1);
        Cursor(ListClass, 0, count: 2);

        Assert.Null(ItemDetails(0));
        Assert.Equal("Cures poison", ItemDetails(1));
    }

    [Fact]
    public void ADescriptionWithNoNameBeforeItIsNoRowsAndNothingIsFetchedForIt()
    {
        messages.Resolved(new SerialResolved(ItemDescriptionFirst, ItemFocus.DescriptionFlags));
        ListRow(ItemFirst, ItemDescriptionFirst + 1);
        Cursor(ListClass, 0, count: 1);

        Assert.Equal("Cures poison", ItemDetails(0));
    }

    [Fact]
    public void TheItemDetailsAreSilentWhileTheSettingIsOff()
    {
        ListRow(ItemFirst, ItemDescriptionFirst);
        Cursor(ListClass, 0, count: 1);
        on = false;

        Assert.Null(ItemDetails(0));
    }

    // The slot block: the archetype, then each slot label followed by the worn item's name and description.
    private void SlotBlock(params (int Item, bool Described)[] worn)
    {
        messages.Resolved(new SerialResolved(Seeker, 8));
        for (var at = 0; at < worn.Length; at++)
        {
            messages.Resolved(new SerialResolved(SlotLabel + at, 21));
            messages.Resolved(new SerialResolved(worn[at].Item, 8));
            if (worn[at].Described)
            {
                messages.Resolved(new SerialResolved(worn[at].Item + WornDescriptionOffset, ItemFocus.DescriptionFlags));
            }
        }
    }

    [Fact]
    public void AWornItemsDetailsAreItsDescriptionFetchedAfterItsName()
    {
        // A weapon fills slot 0 (row 1), an armour slot 1 (row 2).
        SlotBlock((Sword, true), (Mail, true));
        Cursor(SlotsClass, 1, count: 5);

        Assert.Equal("A plain blade", SlotDetails(1));
        Assert.Equal("Heavy armour", SlotDetails(2));
        Assert.Null(SlotDetails(0));
        Assert.Null(SlotDetails(3));
    }

    [Fact]
    public void ASlotWhoseDescriptionWasNotFetchedSaysNone()
    {
        SlotBlock((Sword, false), (Mail, true));
        Cursor(SlotsClass, 1, count: 5);

        Assert.Null(SlotDetails(1));
        Assert.Equal("Heavy armour", SlotDetails(2));
    }

    [Fact]
    public void TheSlotDetailsAreSilentWhileTheSettingIsOff()
    {
        SlotBlock((Sword, true));
        Cursor(SlotsClass, 1, count: 5);
        on = false;

        Assert.Null(SlotDetails(1));
    }

    [Fact]
    public void ANewBlockForgetsTheLastCharactersDescriptions()
    {
        SlotBlock((Sword, true), (Mail, true));
        Cursor(SlotsClass, 1, count: 5);
        SlotBlock((Mail, true));
        Cursor(SlotsClass, 1, count: 5);

        Assert.Null(SlotDetails(1));
        Assert.Equal("Heavy armour", SlotDetails(2));
    }
}
