using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Equipment item list's row names (#923): the equipment names the game fetches (flags 8) as rows come into
/// a view of <see cref="EquipmentFocus.ViewRows"/> lines name them in order, leaving out the worn row the list opens on,
/// which is named by the item worn in the slot. The 18-row sequence is the 2026-11-24 session's (#923). A batch that
/// doesn't fit the view says positions only. Hook events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class EquipmentItemNamesTests
{
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const string ItemsClass = "EquipMenuItemList@ope@ui@app";
    private const int Settle = 0x1;
    private const int MoveDown = 0x15;
    private const int First = 1470;
    private const int Count = 50;
    private const int Seeker = 9441;
    private const int ArmourSlotLabel = 1512;
    private const int FirstArmour = 2600;
    private const int Armours = 12;
    private const int Worn = FirstArmour;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly MessageEvents messages = new();

    public EquipmentItemNamesTests()
    {
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", First, Count),
            new SerialRange(MessageSerials.EquipmentNames, FirstArmour, Armours),
            new SerialRange(MessageSerials.ArchetypeNames, Seeker, 1),
        ]);
        messages.ObserveFiles(serials);
        var source = string.Join("\n\n", Enumerable.Range(0, Count).Select(at => Entry($"System_General_{at:00}", $"Text {at + First}")));
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 21, source));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Equip_Message_1.msg", 12, string.Join("\n\n",
            Enumerable.Range(0, Armours).Select(at => Entry($"ArmorName_{at:00}", at == 0 ? "Worn mail" : $"Armour {at}")))));
        messages.Compiled(new MessageFileCompiled("EN/message/ATCategoryName.msg", 30, Entry("ATCategoryName_01", "Seeker")));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new EquipmentFocus(serials, announcer, new TextCleaner(TextCleanerRules.None));
        // As MenusModule wires it: the focus hears each cursor record before the menu screens do.
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        messages.ObserveSerials(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Cursor(string screenClass, int top, int row, int? count, int command = Settle) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, command, Result: 1, top, row, Flags: 0x66, screenClass, count));

    // The armour slot's list opened from the slot list: the character's block with the worn armour, the slot row, then
    // the names fetched for the view before the list's first cursor record.
    private void OpenArmourList(int? worn, int top, int count, params int[] fetched)
    {
        messages.Resolved(new SerialResolved(Seeker, 8));
        if (worn is { } item)
        {
            messages.Resolved(new SerialResolved(ArmourSlotLabel, 21));
            messages.Resolved(new SerialResolved(item, 8));
        }
        Cursor(SlotsClass, 2, 0, count: 5);
        Fetch(fetched);
        Cursor(ItemsClass, top, 0, count);
    }

    private void Fetch(params int[] names)
    {
        foreach (var name in names)
        {
            messages.Resolved(new SerialResolved(name, 8));
            messages.Resolved(new SerialResolved(name + 1206, 7));
        }
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // #923: "6 of 18", "5 of 18" and "1 of 18", positions only.
    [Fact]
    public void TheEighteenRowListNamesTheWornRowItOpensOnAndTheFiveFetchedAroundIt()
    {
        OpenArmourList(Worn, top: 5, count: 18, 2601, 2602, 2603, 2604, 2605);
        Cursor(ItemsClass, 4, 0, count: 18);
        Cursor(ItemsClass, 0, 0, count: 18);

        Assert.Equal(["Worn mail, 6 of 18", "Armour 5, 5 of 18", "Armour 1, 1 of 18"], Said().Skip(1));
    }

    [Fact]
    public void AStepThatScrollsTheViewNamesTheRowItFetched()
    {
        OpenArmourList(Worn, top: 5, count: 18, 2601, 2602, 2603, 2604, 2605);
        Cursor(ItemsClass, 5, 0, count: 18, command: MoveDown);
        Fetch(2606);
        Cursor(ItemsClass, 5, 1, count: 18);

        Assert.Equal("Armour 6, 7 of 18", Said().Last());
    }

    [Fact]
    public void AnEmptySlotsListNamesEveryRowOnView()
    {
        OpenArmourList(worn: null, top: 0, count: 7, 2601, 2602, 2603, 2604, 2605, 2606);
        Cursor(ItemsClass, 5, 0, count: 7);

        Assert.Equal(["Armour 1, 1 of 7", "Armour 6, 6 of 7"], Said().Skip(1));
    }

    [Fact]
    public void ABatchThatDoesNotFitTheViewSaysPositionsOnly()
    {
        OpenArmourList(Worn, top: 5, count: 18, 2601, 2602, 2603, 2604);
        Cursor(ItemsClass, 4, 0, count: 18);

        Assert.Equal(["6 of 18", "5 of 18"], Said().Skip(1));
    }
}
