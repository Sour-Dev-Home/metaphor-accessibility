using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The menu-screens module knows when the menus close (#268): the hooks hand it each read of the game's own
/// menus-open state, and when the menus close it leaves the current screen and forgets it. Made-up screens.</summary>
[Trait("Category", "Unit")]
public sealed class MenuScreensClosingTests
{
    private const string ItemClass = "ItemChoice@test";
    private const string PlainClass = "PlainChoice@test";

    private readonly MenuScreens screens = new([new Screen("Items", ItemClass)]);
    private readonly List<string> seen = [];

    public MenuScreensClosingTests() => screens.Observe(new Recording(seen));

    private void Cursor(string screenClass, int row) =>
        screens.Observe(new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: 0, row, Flags: 0, screenClass));

    private sealed class Recording(List<string> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen.Add($"{change.Change} {change.ScreenClass} {change.Index}");
    }

    [Fact]
    public void TheMenusCountAsClosedUntilARead()
    {
        Assert.False(screens.MenusOpen);
    }

    [Fact]
    public void ClosingTheMenusLeavesTheCurrentScreenAtItsLastFocus()
    {
        screens.MenusRead(open: true);
        Cursor(ItemClass, row: 2);

        screens.MenusRead(open: false);

        Assert.False(screens.MenusOpen);
        Assert.Equal([$"Entered {ItemClass} 2", $"Left {ItemClass} 2"], seen);
    }

    [Fact]
    public void ReadsThatDoNotChangeTheStateTellNothing()
    {
        screens.MenusRead(open: true);
        Cursor(ItemClass, row: 0);
        screens.MenusRead(open: true);
        screens.MenusRead(open: false);
        screens.MenusRead(open: false);

        Assert.Equal([$"Entered {ItemClass} 0", $"Left {ItemClass} 0"], seen);
    }

    [Fact]
    public void ClosingWithNoScreenEnteredTellsNothing()
    {
        screens.MenusRead(open: true);
        screens.MenusRead(open: false);

        Assert.Empty(seen);
    }

    [Fact]
    public void AfterTheMenusCloseNothingIsRepeated()
    {
        screens.MenusRead(open: true);
        Cursor(ItemClass, row: 1);
        screens.MenusRead(open: false);
        seen.Clear();

        screens.RepeatCurrent();

        Assert.Empty(seen);
    }

    [Fact]
    public void AfterTheMenusReopenTheSameScreenIsEnteredAfresh()
    {
        screens.MenusRead(open: true);
        Cursor(ItemClass, row: 1);
        screens.MenusRead(open: false);
        screens.MenusRead(open: true);
        seen.Clear();

        Cursor(ItemClass, row: 1);

        Assert.Equal([$"Entered {ItemClass} 1"], seen);
    }

    [Fact]
    public void ACursorThatSettlesWhileTheMenusAreClosedTellsNothing()
    {
        // A cursor outside the menus, such as a dialogue's choice or a battle's command list, is no menu screen: its
        // own features speak it (#268).
        Cursor(PlainClass, row: 0);
        Cursor(PlainClass, row: 1);

        Assert.Empty(seen);
        Assert.False(screens.MenusOpen);
    }

    [Fact]
    public void AfterTheMenusOpenTheFirstCursorToSettleEntersItsScreen()
    {
        Cursor(PlainClass, row: 3);
        screens.MenusRead(open: true);

        Cursor(PlainClass, row: 3);

        Assert.Equal([$"Entered {PlainClass} 3"], seen);
    }
}
