using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The camp Item list's tabs and scrolled rows (#216): the list fetches the rows on view and then the eight tab
/// names on every opening and move, the cursor's <c>row</c> field is the view's first row, and a tab is told by the item
/// names its rows fetch or, for the equipment tabs, by its place next to the last tab
/// (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md). Hook events in, announcements out; made-up file
/// text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class ItemTabsTests
{
    private const string ListClass = "ItemMenuChoice@ope@ui@app";
    private const string OtherClass = "RootMenuChoice@ope@ui@app";
    private const int ItemFirst = 300;
    private const int Exchange = ItemFirst + 10;
    private const int Information = ItemFirst + 11;
    private const int Weapon = 2500;
    private const int Armour = 2900;
    private const int Accessory = 3305;
    private const int EquipmentFirst = 4000;
    private const string TargetClass = "ItemCharaSelChoice@ope@ui@app";

    private static readonly int[] TabSerials = [1369, 1371, 1372, 1379, 1374, 1375, 1376, 1377];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private long now = 50_000;

    public ItemTabsTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", 1300, 300),
            new SerialRange(MessageSerials.ItemNames, ItemFirst, 12),
            new SerialRange(MessageSerials.EquipmentNames, EquipmentFirst, 2),
            new SerialRange(MessageSerials.CharacterNames, 0, 8),
        ]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new ItemFocus(serials, announcer, new TextCleaner(TextCleanerRules.None));
        // The focus hears each cursor input before the screens turn it into a screen event, as MenusModule wires it.
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 300).Select(at => Entry($"System_Filler_{at:000}", $"Text {1300 + at}")))));
        var items = Enumerable.Range(0, 10).Select(at => Entry($"ItemName_Consumable_{at:000}", $"Item {at}"))
            .Append(Entry("ItemName_CASH_001", "Nugget"))
            .Append(Entry("ItemName_Info_001", "Note"));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21, string.Join("\n\n", items)));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Equip_Message_1.msg", 12, string.Join("\n\n",
            Entry("WeaponName_SSWORD_01", "Sword"), Entry("ArmorName_Heavy_01", "Mail"))));
        messages.Compiled(new MessageFileCompiled("EN/message/CharaName.msg", 1, string.Join("\n\n",
            Enumerable.Range(0, 8).Select(at => Entry($"CharaName_PC{at:00}", $"Member {at}")))));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    // A batch as the list fetches it: the rows on view (flags 9), the party's small serials, then the tab names.
    private void Batch(params int[] rows)
    {
        foreach (var serial in rows)
        {
            messages.Resolved(new SerialResolved(serial, 9));
        }
        foreach (var member in new[] { 2, 4, 7 })
        {
            messages.Resolved(new SerialResolved(member, 9));
        }
        foreach (var serial in TabSerials)
        {
            messages.Resolved(new SerialResolved(serial, 2));
        }
    }

    private static int[] Items(int first, int count) => [.. Enumerable.Range(ItemFirst + first, count)];

    // A settled cursor: `line` is the focus's place in the view, `scroll` the view's first row.
    private void Settled(int line, int scroll, int count, string screenClass = ListClass)
    {
        now += 10;
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, 0x1, Result: 1, Top: line, Row: scroll, Flags: 0x66,
            screenClass, count));
    }

    // A move, which the game handles before it fetches the rows and settles.
    private void Move(int line, int scroll, int count)
    {
        now += 10;
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, 0x14, Result: 20, Top: line, Row: scroll, Flags: 0x66,
            ListClass, count));
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheListSaysTheTabByItsItemsAndTheFirstRow()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);

        Assert.Equal(["Item, Text 1369, Item 0, 1 of 10"], Said());
    }

    [Fact]
    public void NamesFetchedBeforeAnotherScreensCursorAreNotTheListsRows()
    {
        messages.Resolved(new SerialResolved(ItemFirst + 5, 9));
        Settled(1, 0, count: 9, screenClass: OtherClass);
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);

        Assert.Equal(["Item, Text 1369, Item 0, 1 of 10"], Said());
    }

    [Fact]
    public void WrappingToTheLastRowNamesItFromTheScrolledView()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Move(7, 2, count: 10);
        Batch(Items(2, 8));
        Settled(7, 2, count: 10);

        Assert.Equal("Item 9, 10 of 10", Said().Last());
    }

    [Fact]
    public void AStepThatScrollsOneRowFetchesOnlyTheNewRow()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Move(7, 1, count: 10);
        messages.Resolved(new SerialResolved(ItemFirst + 8, 9));
        Settled(7, 1, count: 10);
        Move(0, 0, count: 10);
        messages.Resolved(new SerialResolved(ItemFirst, 9));
        Settled(0, 0, count: 10);

        Assert.Equal(["Item, Text 1369, Item 0, 1 of 10", "Item 8, 9 of 10", "Item 0, 1 of 10"], Said());
    }

    [Fact]
    public void ATabSwitchOnTheSameRowSaysTheTabAndTheRow()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Batch(Exchange);
        Settled(0, 0, count: 1);

        Assert.Equal(["Item, Text 1369, Item 0, 1 of 10", "Text 1371, Nugget, 1 of 1"], Said());
    }

    [Fact]
    public void ATabSwitchFromAnotherRowSaysTheTabOnce()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Move(2, 0, count: 10);
        Batch(Items(0, 8));
        Settled(2, 0, count: 10);
        Batch(Exchange);
        Settled(0, 0, count: 1);

        Assert.Equal(["Item, Text 1369, Item 0, 1 of 10", "Item 2, 3 of 10", "Text 1371, Nugget, 1 of 1"], Said());
    }

    [Fact]
    public void AMoveOnTheSameTabDoesNotSayTheTab()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Move(1, 0, count: 10);
        Batch(Items(0, 8));
        Settled(1, 0, count: 10);

        Assert.Equal("Item 1, 2 of 10", Said().Last());
    }

    [Fact]
    public void TheEquipmentTabsAreToldByTheirPlaceAfterInformation()
    {
        Batch(Information);
        Settled(0, 0, count: 1);
        Batch(Weapon);
        Settled(0, 0, count: 5);
        Batch(Armour);
        Settled(0, 0, count: 3);
        Batch(Weapon);
        Settled(0, 0, count: 5);

        Assert.Equal(["Item, Text 1379, Note, 1 of 1", "Text 1374, 1 of 5", "Text 1375, 1 of 3", "Text 1374, 1 of 5"],
            Said());
    }

    [Fact]
    public void GoingLeftFromUseReachesAccessories()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Batch(Accessory);
        Settled(0, 0, count: 4);

        Assert.Equal("Text 1377, 1 of 4", Said().Last());
    }

    [Fact]
    public void AnEquipmentTabEnteredDirectlyIsNotNamed()
    {
        Batch(Weapon);
        Settled(0, 0, count: 5);

        Assert.Equal(["Item, 1 of 5"], Said());
    }

    // #216: the equipment tabs' rows said positions only, as their names' file wasn't known.
    [Fact]
    public void AnEquipmentTabsRowIsNamedAndTheTabToldByItsKind()
    {
        Batch(EquipmentFirst + 1);
        Settled(0, 0, count: 3);

        Assert.Equal(["Item, Text 1375, Mail, 1 of 3"], Said());
    }

    // #216: the target list's rows are the protagonist, then the party members the list fetched after its rows (2, 4, 7
    // here); the protagonist's name isn't a message.
    [Fact]
    public void TheTargetListNamesThePartyMembersTheListFetched()
    {
        Batch(Items(0, 8));
        Settled(0, 0, count: 10);
        Settled(0, 0, count: 8, screenClass: TargetClass);
        Settled(2, 0, count: 8, screenClass: TargetClass);
        Settled(4, 0, count: 8, screenClass: TargetClass);

        Assert.Equal(["1 of 8", "Member 4, 3 of 8", "5 of 8"], Said().Skip(1));
    }
}
