using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The shop's screens (#736, #197): the top menu's four rows are named from the labels the game fetches for them
/// (System_General_12 to _14 and Shop_Menu_03), and a Buy or Sell row from the n-th name its view fetched by serial
/// (docs/raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md). Hook events in, announcements out; made-up file
/// text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class ShopFocusTests
{
    private const string TopClass = "ItemShopMenuChoice@ope@ui@app";
    private const string ListClass = "ItemShopChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int Flags = ShopFocus.NameFlags;
    private const int ItemFirst = 300;
    private const int EquipmentSerial = 2453;
    private const long Later = CursorEvents.QuietMilliseconds + 1;

    private const string ConfirmClass = "ItemShopConfirmChoice@ope@ui@app";
    private const int Nugget = ItemFirst + 4;
    private const int CancelSerial = 1430;
    private const int ConfirmBuySerial = 1431;

    private static readonly string[] ItemTexts = ["Tonic", "Salve", "Elixir", "Ether"];
    private static readonly int[] TabSerials = [1371, 1369, 1374, 1375, 1376, 1377];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private long now = 50_000;

    public ShopFocusTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", 1300, 300),
            new SerialRange(MessageSerials.ItemNames, ItemFirst, 5),
        ]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new ShopFocus(serials, announcer, new TextCleaner(TextCleanerRules.None));
        // The focus hears each cursor input before the screens turn it into a screen event, as MenusModule wires it.
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 300).Select(at => (1300 + at) switch
            {
                CancelSerial => Entry("System_Window_01", "Cancel"),
                ConfirmBuySerial => Entry("System_Window_02", "Buy"),
                var serial => Entry($"System_Filler_{at:000}", $"Text {serial}"),
            }))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21,
            string.Join("\n\n", ItemTexts.Select((text, at) => Entry($"ItemName_Consumable_{at:000}", text))
                .Append(Entry("ItemName_CASH_001", "Nugget")))));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    // A Sell tab's batch: its rows, then the tab names (flags 2).
    private void SellBatch(params int[] fetched)
    {
        Fetched(fetched);
        foreach (var serial in TabSerials)
        {
            messages.Resolved(new SerialResolved(serial, 2));
        }
    }

    private void Fetched(params int[] fetched)
    {
        foreach (var serial in fetched)
        {
            messages.Resolved(new SerialResolved(serial, Flags));
        }
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheTopMenuRowsAreNamedFromTheirFetchedLabels()
    {
        Cursor(TopClass, 0, count: 4);
        Cursor(TopClass, 1, count: 4);
        Cursor(TopClass, 3, count: 4);

        Assert.Equal(["Shop, Text 1491, 1 of 4", "Text 1492, 2 of 4", "Text 1362, 4 of 4"], Said());
    }

    [Fact]
    public void AListRowIsTheNthItemNameTheViewFetched()
    {
        Fetched(301, 300, 303);
        Cursor(ListClass, 0, count: 37);
        now += 10;
        Cursor(ListClass, 2, count: 37);

        Assert.Equal(["Salve, 1 of 37", "Ether, 3 of 37"], Said());
    }

    [Fact]
    public void PromptsAndColumnLabelsFetchedBesideTheRowsAreNotRows()
    {
        Fetched(1383, 1384);
        Fetched(300, 301);
        Cursor(ListClass, 1, count: 37);

        Assert.Equal(["Salve, 2 of 37"], Said());
    }

    [Fact]
    public void ARowWhoseNameHasNoKnownTextIsSaidByPosition()
    {
        Fetched(EquipmentSerial, 301);
        Cursor(ListClass, 0, count: 37);
        now += 10;
        Cursor(ListClass, 1, count: 37);

        Assert.Equal(["1 of 37", "Salve, 2 of 37"], Said());
    }

    [Fact]
    public void ANewViewsFetchesStartTheRowsAfresh()
    {
        Fetched(300, 301);
        Cursor(ListClass, 1, count: 37);
        now += Later;
        Fetched(302);
        Cursor(ListClass, 0, count: 37);

        Assert.Equal(["Salve, 2 of 37", "Elixir, 1 of 37"], Said());
    }

    [Fact]
    public void SellAllIsTheFirstRowOfTheSellList()
    {
        Fetched(1412, 300);
        Cursor(ListClass, 0, count: 2);
        now += 10;
        Cursor(ListClass, 1, count: 2);

        Assert.Equal(["Text 1412, 1 of 2", "Tonic, 2 of 2"], Said());
    }

    [Fact]
    public void BuyOpensOnItsOwnFirstRowNotTheSellRowsFetchedBehindTheMenu()
    {
        Cursor(TopClass, 1, count: 4);
        Fetched(1412, Nugget);
        Cursor(TopClass, 0, count: 4);
        Fetched(301, 302);
        Cursor(ListClass, 0, count: 37);

        Assert.Equal("Salve, 1 of 37", Said().Last());
    }

    [Fact]
    public void EachSellTabIsNamedOnOpeningAndOnEachSwitch()
    {
        Cursor(TopClass, 1, count: 4);
        SellBatch(1412, Nugget);
        Cursor(ListClass, 0, count: 2);
        now += 10;
        SellBatch(ItemFirst, ItemFirst + 1);
        Cursor(ListClass, 0, count: 5);
        now += 10;
        SellBatch(EquipmentSerial);
        Cursor(ListClass, 0, count: 3);

        Assert.Equal(
            ["Shop, Text 1492, 2 of 4", "Text 1371, Text 1412, 1 of 2", "Text 1369, Tonic, 1 of 5", "Text 1374, 1 of 3"],
            Said());
    }

    [Fact]
    public void TheBuyListSaysNoTab()
    {
        Cursor(TopClass, 0, count: 4);
        SellBatch(ItemFirst);
        Cursor(ListClass, 0, count: 5);

        Assert.Equal("Tonic, 1 of 5", Said().Last());
    }

    [Fact]
    public void TheConfirmRowsAreNamedFromTheWindowLabelsFetchedAsItOpens()
    {
        Fetched(ConfirmBuySerial, CancelSerial);
        Cursor(ConfirmClass, 0, count: 2);
        Fetched(ItemFirst);
        Cursor(ConfirmClass, 1, count: 2);

        Assert.Equal(["Buy, 1 of 2", "Cancel, 2 of 2"], Said());
    }
}
