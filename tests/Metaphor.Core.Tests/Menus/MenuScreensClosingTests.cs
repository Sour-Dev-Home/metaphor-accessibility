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

    private const string DialogClass = "DialogChoice@test";

    private void Cursor(string screenClass, int row) => screens.Observe(Input(screenClass, manager: 0xa0, row));

    private static CursorInput Input(string screenClass, long manager, int row) =>
        new(manager, ClassRva: 0x100, Command: 0x1, Result: 1, Top: 0, row, Flags: 0, screenClass);

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
    public void AScreenShownOutsideTheMenusIsToldWhileTheyAreClosed()
    {
        // The second yes/no of #540's run: the title's handle had cleared, so the menus read closed, and the dialog was
        // silent (docs/findings/difficulty-select-shared-cursor.md).
        var outside = new MenuScreens([new Screen("Dialog", DialogClass, OutsideMenus: true)]);
        outside.Observe(new Recording(seen));

        outside.Observe(Input(DialogClass, manager: 0xa0, row: 1));
        outside.Observe(Input(DialogClass, manager: 0xa0, row: 0));

        Assert.Equal([$"Entered {DialogClass} 1", $"FocusMoved {DialogClass} 0"], seen);
    }

    [Fact]
    public void AScreenShownOutsideTheMenusIsEnteredAfreshWithEachNewManager()
    {
        // Nothing leaves a screen outside the menus, so a second dialog on the same item would otherwise be no change;
        // the game makes a manager per visit (docs/raw-sources/screen-class-session-2026-11-17.md).
        var outside = new MenuScreens([new Screen("Dialog", DialogClass, OutsideMenus: true)]);
        outside.Observe(new Recording(seen));

        outside.Observe(Input(DialogClass, manager: 0xa0, row: 1));
        outside.Observe(Input(DialogClass, manager: 0xb0, row: 1));

        Assert.Equal([$"Entered {DialogClass} 1", $"Left {DialogClass} 1", $"Entered {DialogClass} 1"], seen);
    }

    [Fact]
    public void AMenuScreenKeepsItsFocusAcrossManagers()
    {
        screens.MenusRead(open: true);
        screens.Observe(Input(ItemClass, manager: 0xa0, row: 1));

        screens.Observe(Input(ItemClass, manager: 0xb0, row: 1));

        Assert.Equal([$"Entered {ItemClass} 1"], seen);
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
