using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's item list says its tab's name (#851, ADR 0023): on opening and on each LB or RB switch, before the
/// row, from the list's tab field (<see cref="CursorInput.Mode"/>) and the tab names the list fetches
/// (<see cref="BattleListFocus.FirstItemTabSerial"/>). A switch fetches no rows on build 548ddc95, so the row is said by
/// its position. Hook events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleItemTabsTests
{
    private const string ItemClass = "BTL_ItemSelect@ope@ui@app";
    private const int ItemFirst = 5143;
    private const int CommandFirst = 1932;
    private const int CommandCount = 159;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private readonly MenuScreens screens = new(KnownScreens.All);
    private long now = 50_000;
    private bool tabsOn = true;

    public BattleItemTabsTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange(MessageSerials.ItemNames, ItemFirst, 3),
            new SerialRange(MessageSerials.BattleCommandMessages, CommandFirst, CommandCount),
        ]);
        messages.ObserveFiles(serials);
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        var items = BattleListFocus.Items(serials, announcer, new TextCleaner(TextCleanerRules.None), () => now,
            viewsOn: () => true, tabsOn: () => tabsOn);
        screens.Observe(items);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveSerials(items);
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 15,
            Source("Item", "Tonic", "Salve", "Ether")));
        // The five tab names sit at 2085 to 2089, 153 to 157 places into the file.
        messages.Compiled(new MessageFileCompiled("EN/message/system/CommandBattle_Message.msg", 7,
            Source("Command", [.. Enumerable.Range(0, CommandCount)
                .Select(at => at is >= 153 and <= 157 ? $"Tab {at - 152}" : $"Text {at}")])));
    }

    private static string Source(string prefix, params string[] texts) => string.Join("\n\n",
        texts.Select((text, at) => $"//--------\n@{prefix}_{at:000}\n{{\n{text}\n<WAIT>\n}}"));

    private void Settle(int index, int count, int? tab) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x46,
            ItemClass, count, Mode: tab));

    private void Open(int tab, int count)
    {
        foreach (var serial in new[] { ItemFirst, ItemFirst + 1, ItemFirst + 2 })
        {
            messages.Resolved(new SerialResolved(serial, BattleListFocus.NameFlags));
        }
        Settle(0, count, tab);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // #851: opening said "Items, Tonic, 1 of 11" with no tab.
    [Fact]
    public void OpeningSaysTheListThenItsTabThenTheRow()
    {
        Open(tab: 1, count: 11);

        Assert.Equal(["Items, Tab 2, Tonic, 1 of 11"], Said());
    }

    // #851: a switch with LB or RB said nothing.
    [Fact]
    public void ASwitchSaysTheNewTabThenTheRowsPosition()
    {
        Open(tab: 0, count: 11);
        now += 500;
        Settle(0, 5, tab: 1);

        Assert.Equal("Tab 2, 1 of 5", Said().Last());
        Assert.True(announcer.Said[^1].Interrupt);
    }

    [Fact]
    public void ASwitchToATabOfTheSameLengthSaysItToo()
    {
        Open(tab: 0, count: 11);
        now += 500;
        Settle(0, 11, tab: 4);

        Assert.Equal("Tab 5, 1 of 11", Said().Last());
    }

    [Fact]
    public void AMoveOnTheSameTabSaysNoTab()
    {
        Open(tab: 0, count: 11);
        Settle(1, 11, tab: 0);

        Assert.Equal("Salve, 2 of 11", Said().Last());
    }

    [Fact]
    public void OffASwitchSaysNothingAsBefore()
    {
        tabsOn = false;
        Open(tab: 0, count: 11);
        now += 500;
        Settle(0, 5, tab: 1);

        Assert.Equal(["Items, Tonic, 1 of 11"], Said());
    }

    [Fact]
    public void AnUnreadableTabFieldSaysNoTab()
    {
        Open(tab: 0, count: 11);
        now += 500;
        Settle(1, 11, tab: null);

        Assert.Equal("Salve, 2 of 11", Said().Last());
    }
}
