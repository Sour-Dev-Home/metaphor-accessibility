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

    // #560: a settings page's OK button settles at index 0 with the cursor's 0x4 bit clear (flags 0x43, not 0x47).
    [Fact]
    public void ACursorWithItsItemBitClearIsOffTheList()
    {
        var (screens, seen) = Watched();

        Settle(screens, row: 2, mode: null, flags: 0x47);
        Settle(screens, row: 0, mode: null, flags: 0x43);

        Assert.Equal([(2, false), (0, true)], seen.Select(change => (change.Index, change.OffList)));
    }

    [Fact]
    public void UnreadFlagsCountAsOnTheList()
    {
        var (screens, seen) = Watched();

        Settle(screens, row: 2, mode: null, flags: 0);

        Assert.Equal([(2, false)], seen.Select(change => (change.Index, change.OffList)));
    }

    [Fact]
    public void LeavingTheListAtItsFirstItemIndexIsAFocusMove()
    {
        var (screens, seen) = Watched();
        Settle(screens, row: 0, mode: null, flags: 0x43);

        Settle(screens, row: 0, mode: null, flags: 0x47);
        Settle(screens, row: 0, mode: null, flags: 0x46);

        Assert.Equal(
            [(ScreenChange.Entered, true), (ScreenChange.FocusMoved, false)],
            seen.Select(change => (change.Change, change.OffList)));
    }

    // #589: Key Configuration's OK button is its owner's +0x914, not the flags' item bit, which stays clear on its cells.
    [Theory]
    [InlineData(true, 0x46, true)]
    [InlineData(false, 0x42, false)]
    public void AScreenThatSaysWhetherItsButtonIsFocusedOverridesTheItemBit(bool onButton, int flags, bool offList)
    {
        var input = new CursorInput(0xa0, GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 0, Row: 0, flags, ItemClass,
            OnButton: onButton);

        Assert.Equal(offList, input.OffList);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public void KeyConfigurationsModeOneIsItsOkButtonAndAnUnreadModeACell(int? mode, bool onButton)
    {
        Assert.Equal(onButton, SettingsPages.KeyConfigOnButton(mode));
    }

    // #589: a Key Configuration sub-tab switch can keep the grid's index (Event's first cell to Field's), so only the
    // count changes; on a grid that is a focus move.
    [Fact]
    public void AGridWhoseCountChangesOnTheSameCellIsAFocusMove()
    {
        var (screens, seen) = Watched();
        var cell = GridCursor.Of(columns: 2, column: 0, line: 0, scroll: 0, page: 0, pageStride: 0);

        screens.Observe(new CursorInput(0xa0, GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 0, Row: 0, Flags: 0x46,
            ItemClass, Count: 3, Grid: cell));
        screens.Observe(new CursorInput(0xa0, GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 0, Row: 0, Flags: 0x46,
            ItemClass, Count: 14, Grid: cell));

        Assert.Equal(
            [(ScreenChange.Entered, 3), (ScreenChange.FocusMoved, 14)],
            seen.Select(change => (change.Change, change.Count ?? -1)));
    }

    [Fact]
    public void AListWhoseCountChangesOnTheSameItemIsNotAFocusMove()
    {
        var (screens, seen) = Watched();

        screens.Observe(new CursorInput(0xa0, 0x100, Command: 0x1, Result: 1, Top: 0, Row: 0, Flags: 0x46, ItemClass,
            Count: 3));
        screens.Observe(new CursorInput(0xa0, 0x100, Command: 0x1, Result: 1, Top: 0, Row: 0, Flags: 0x46, ItemClass,
            Count: 14));

        Assert.Equal([ScreenChange.Entered], seen.Select(change => change.Change));
    }

    // #563: a grid cursor's events carry its cells, so a feature can say the column; repeating keeps it.
    [Fact]
    public void EachEventCarriesTheGridOfTheInputBehindIt()
    {
        var (screens, seen) = Watched();
        var first = GridCursor.Of(columns: 2, column: 0, line: 1, scroll: 0, page: 0, pageStride: 0);
        var second = GridCursor.Of(columns: 2, column: 1, line: 1, scroll: 0, page: 0, pageStride: 0);

        screens.Observe(new CursorInput(0xa0, GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 2, Row: 0, Flags: 0x46,
            ItemClass, Count: 23, Grid: first));
        screens.Observe(new CursorInput(0xa0, GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 3, Row: 0, Flags: 0x46,
            ItemClass, Count: 23, Grid: second));
        screens.RepeatCurrent();

        Assert.Equal(
            [(ScreenChange.Entered, 2, first), (ScreenChange.FocusMoved, 3, second), (ScreenChange.Entered, 3, second)],
            seen.Select(change => (change.Change, change.Index, change.Grid)));
    }

    // #96: a settings page's value changing on the same row is its own change, carrying the new values.
    [Fact]
    public void AValueChangingOnTheSameItemIsAValueChange()
    {
        var (screens, seen) = Watched();
        var before = Values(17, 0);
        var after = Values(17, 1);

        SettleWith(screens, row: 1, before);
        SettleWith(screens, row: 1, Values(17, 0));
        SettleWith(screens, row: 1, after);
        SettleWith(screens, row: 2, after);
        screens.RepeatCurrent();

        Assert.Equal(
            [(ScreenChange.Entered, 1, before), (ScreenChange.ValueChanged, 1, after), (ScreenChange.FocusMoved, 2, after),
                (ScreenChange.Entered, 2, after)],
            seen.Select(change => (change.Change, change.Index, change.Values)));
    }

    [Fact]
    public void ValuesThatStopBeingReadAreNoValueChange()
    {
        var (screens, seen) = Watched();

        SettleWith(screens, row: 1, Values(17, 0));
        SettleWith(screens, row: 1, null);

        Assert.Equal([ScreenChange.Entered], seen.Select(change => change.Change));
    }

    private static SettingValues Values(int id, int value)
    {
        var bytes = new byte[SettingsPages.WorkingCopyLength];
        BitConverter.GetBytes(SettingValues.Count).CopyTo(bytes, 0x8);
        BitConverter.GetBytes(value).CopyTo(bytes, 0x10 + (4 * id));
        return SettingValues.Of(bytes)!;
    }

    private static void SettleWith(MenuScreens screens, int row, SettingValues? values) =>
        screens.Observe(new CursorInput(0xa0, 0x100, Command: 0x1, Result: 1, Top: 0, Row: row, Flags: 0x47, ItemClass,
            Count: 3, Values: values));

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

    private static void Settle(
        MenuScreens screens, int row, int? mode, string screenClass = ItemClass, int flags = 0x46) =>
        screens.Observe(new CursorInput(0xa0, 0x100, Command: 0x1, Result: 1, Top: 0, Row: row, Flags: flags, screenClass,
            Count: 3, Mode: mode));

    private sealed class Recording(List<ScreenEvent> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen.Add(change);
    }
}
