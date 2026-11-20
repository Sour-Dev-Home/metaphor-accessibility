using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>What the menu-screens module takes, and the list total its events carry. Made-up screens.</summary>
[Trait("Category", "Unit")]
public sealed class MenuScreensTests
{
    private const string ItemClass = "ItemChoice@test";

    [Fact]
    public void MissingScreensOrObserversAreRefused()
    {
        var screens = new MenuScreens([]);

        Assert.Throws<ArgumentNullException>("knownScreens", () => new MenuScreens(null!));
        Assert.Throws<ArgumentNullException>("observer", () => screens.Observe((IScreenObserver)null!));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(4, 5, 5)]
    [InlineData(998, ScreenEvent.LongestList, ScreenEvent.LongestList)]
    public void AFocusInsideAListOfAPlausibleLengthHasATotal(int index, int count, int total)
    {
        Assert.Equal(total, new ScreenEvent(ScreenChange.FocusMoved, null, ItemClass, index, count).Total);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(0, ScreenEvent.LongestList + 1)]
    public void AFocusOutsideItsListOrAnImplausibleLengthHasNoTotal(int index, int count)
    {
        Assert.Null(new ScreenEvent(ScreenChange.FocusMoved, null, ItemClass, index, count).Total);
    }

    [Fact]
    public void AnEventWithoutACountHasNoTotal()
    {
        Assert.Null(new ScreenEvent(ScreenChange.Entered, null, ItemClass, 0).Total);
    }

    // #210: one choice can serve two lists, told apart by its own mode field.
    [Fact]
    public void EachEventCarriesTheModeOfTheInputBehindIt()
    {
        var (screens, seen) = Watched();

        Settle(screens, row: 2, mode: 1);
        Settle(screens, row: 0, mode: 2);
        screens.RepeatCurrent();
        Settle(screens, row: 0, mode: null, screenClass: "OtherChoice@test");

        Assert.Equal(
            [(ScreenChange.Entered, 2, 1), (ScreenChange.FocusMoved, 0, 2), (ScreenChange.Entered, 0, 2),
                (ScreenChange.Left, 0, 2), (ScreenChange.Entered, 0, null)],
            seen.Select(change => (change.Change, change.Index, change.Mode)));
    }

    [Fact]
    public void AModeChangeOnTheSameItemIsAFocusMove()
    {
        var (screens, seen) = Watched();
        Settle(screens, row: 0, mode: 1);

        Settle(screens, row: 0, mode: 2);
        Settle(screens, row: 0, mode: 2);

        Assert.Equal([(ScreenChange.Entered, 1), (ScreenChange.FocusMoved, 2)], seen.Select(change => (change.Change, change.Mode)));
    }

    [Theory]
    [InlineData("TitleChoice@ope@ui@app", 0x4a8)]
    [InlineData("RootMenuChoice@ope@ui@app", null)]
    [InlineData(null, null)]
    public void OnlyAChoiceKnownToServeTwoListsHasAModeField(string? screenClass, int? offset)
    {
        Assert.Equal(offset, ScreenModes.FieldOffset(screenClass));
    }

    private static (MenuScreens Screens, List<ScreenEvent> Seen) Watched()
    {
        var screens = new MenuScreens([]);
        var seen = new List<ScreenEvent>();
        screens.Observe(new Recording(seen));
        screens.MenusRead(open: true);
        return (screens, seen);
    }

    private static void Settle(MenuScreens screens, int row, int? mode, string screenClass = ItemClass) =>
        screens.Observe(new CursorInput(0xa0, 0x100, Command: 0x1, Result: 1, Top: 0, Row: row, Flags: 0x46, screenClass,
            Count: 3, Mode: mode));

    private sealed class Recording(List<ScreenEvent> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen.Add(change);
    }
}
