using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's Order menu and its Tactics submenu (#771): the Order menu's three rows by the mod's own words, the
/// Tactics rows by the System_Battle_Tactics names in cursor order (serials 1537, 1536, 1538 to 1541), both while the
/// menus read closed. Hook events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleOrderFocusTests
{
    private const string OrderClass = "BTL_OrderTop@ope@ui@app";
    private const string TacticsClass = "BTL_OrderTactics@ope@ui@app";
    private const int First = 1530;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public BattleOrderFocusTests()
    {
        var serials = new MessageSerials([new SerialRange("message/system/System_Message.msg", First, 12)]);
        var messages = new MessageEvents();
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 12).Select(at =>
                $"//--------\n@System_Battle_{at:00}\n{{\nText {First + at}\n<WAIT>\n}}"))));
        var screens = new MenuScreens(KnownScreens.All);
        // A battle: the menus read closed.
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        screens.Observe(new BattleOrderFocus(serials, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(string screenClass, int index, int count, long manager = 0xa0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, 0x1, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheOrderMenuSaysEachRowByName()
    {
        Cursor(OrderClass, 0, 3);
        Cursor(OrderClass, 1, 3);
        Cursor(OrderClass, 2, 3);

        Assert.Equal(["Orders, Party, 1 of 3", "Tactics, 2 of 3", "Escape, 3 of 3"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheTacticsSubmenuSaysEachRowByTheGamesName()
    {
        Cursor(OrderClass, 1, 3);
        for (var row = 0; row < 6; row++)
        {
            Cursor(TacticsClass, row, 6, manager: 0xb0);
        }

        Assert.Equal([
            "Orders, Tactics, 2 of 3", "Tactics, Text 1537, 1 of 6", "Text 1536, 2 of 6", "Text 1538, 3 of 6",
            "Text 1539, 4 of 6", "Text 1540, 5 of 6", "Text 1541, 6 of 6",
        ], Said());
    }

    // A count other than the rows known can't tell which row is which: its position alone.
    [Fact]
    public void AnOrderMenuOfAnotherLengthSaysThePositionAlone()
    {
        Cursor(OrderClass, 1, 2);

        Assert.Equal(["Orders, 2 of 2"], Said());
    }

    [Fact]
    public void BothScreensAreKnownAndShownOutsideTheMenus()
    {
        Assert.Contains(BattleOrderFocus.Orders, KnownScreens.All);
        Assert.Contains(BattleOrderFocus.Tactics, KnownScreens.All);
        Assert.True(BattleOrderFocus.Orders.OutsideMenus);
        Assert.True(BattleOrderFocus.Tactics.OutsideMenus);
    }
}
