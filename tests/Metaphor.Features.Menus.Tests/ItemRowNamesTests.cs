using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The camp Item list's row names (#216): the list fetches each visible row's name by serial in row order just
/// before its cursor record, so with the view at the top row n is the n-th item name of that batch
/// (docs/findings/item-equipment-menu-names-from-resolves.md). A row with no fetched item name is said by position.
/// Hook events in, announcements out; made-up file text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class ItemRowNamesTests
{
    private const string ListClass = "ItemMenuChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int ItemFirst = 300;
    private const int EquipmentSerial = 2688;

    private static readonly string[] ItemTexts = ["Tonic", "Salve", "Elixir", "Ether"];
    private static readonly int[] TabSerials = [1369, 1371, 1372, 1379, 1374, 1375, 1376, 1377];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private long now = 50_000;

    public ItemRowNamesTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([new SerialRange(MessageSerials.ItemNames, ItemFirst, 4)]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new ItemFocus(serials, announcer, new TextCleaner(TextCleanerRules.None));
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        var source = string.Join("\n\n", ItemTexts.Select((text, at) =>
            $"//--------\n@ItemName_{at:000}\n{{\n{text}\n<WAIT>\n}}"));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21, source));
    }

    private void Fetched(params int[] serials)
    {
        foreach (var serial in serials)
        {
            messages.Resolved(new SerialResolved(serial, 9));
        }
    }

    // The eight tab names a list opening fetches after its rows, in their fixed order (flags 2).
    private void Tabs()
    {
        foreach (var serial in TabSerials)
        {
            messages.Resolved(new SerialResolved(serial, 2));
        }
    }

    private void Cursor(int index, int? count = 4) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            ListClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void RowsAreNamedInTheOrderTheirNamesWereFetched()
    {
        Fetched(301, 300, 302);
        Cursor(0);
        now += 10;
        Cursor(1);
        now += 10;
        Cursor(2);

        Assert.Equal(["Item, Salve, 1 of 4", "Tonic, 2 of 4", "Elixir, 3 of 4"], Said());
    }

    [Fact]
    public void ARowWithNoFetchedItemNameIsSaidByPosition()
    {
        Fetched(EquipmentSerial);
        Cursor(0);

        Assert.Equal(["Item, 1 of 4"], Said());
    }

    [Fact]
    public void ANewListOpeningStartsTheRowsAfresh()
    {
        Fetched(300, 301);
        Tabs();
        Cursor(1);
        now += 10;
        Fetched(302);
        Tabs();
        Cursor(0);

        Assert.Equal(["Item, Salve, 2 of 4", "Elixir, 1 of 4"], Said());
    }

    [Fact]
    public void AListOpeningWithNoItemNamesForgetsTheLastOpeningsRows()
    {
        Fetched(300, 301);
        Tabs();
        Cursor(1);
        now += 10;
        Fetched(EquipmentSerial);
        Tabs();
        Cursor(0);

        Assert.Equal(["Item, Salve, 2 of 4", "1 of 4"], Said());
    }

    [Fact]
    public void ANameFetchedWithOtherFlagsIsNotARow()
    {
        messages.Resolved(new SerialResolved(300, 7));
        Cursor(0);

        Assert.Equal(["Item, 1 of 4"], Said());
    }
}
