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

    private static readonly string[] ItemTexts = ["Tonic", "Salve", "Elixir", "Ether"];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private long now = 50_000;

    public ShopFocusTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", 1300, 300),
            new SerialRange(MessageSerials.ItemNames, ItemFirst, 4),
        ]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        var focus = new ShopFocus(serials, announcer, new TextCleaner(TextCleanerRules.None), () => now);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 300).Select(at => Entry("System_Filler", at, $"Text {1300 + at}")))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21,
            string.Join("\n\n", ItemTexts.Select((text, at) =>
                Entry("ItemName", at, text)))));
    }

    private static string Entry(string prefix, int at, string text) =>
        $"//--------\n@{prefix}_{at:000}\n{{\n{text}\n<WAIT>\n}}";

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
}
