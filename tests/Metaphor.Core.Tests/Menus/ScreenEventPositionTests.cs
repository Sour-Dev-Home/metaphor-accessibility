using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The position a screen event says (#664): "n of total" by default, the name alone with the "of total" turned
/// off, and the number alone when nothing else is known, so a focus is never silent. Made-up screens.</summary>
[Trait("Category", "Unit")]
public sealed class ScreenEventPositionTests
{
    private static ScreenEvent Focus(bool say = true, int? count = 8) =>
        new(ScreenChange.FocusMoved, null, "Test@ope@ui@app", Index: 2, Count: count, SayPositions: say);

    [Fact]
    public void ByDefaultAnItemIsSaidWithItsPositionOfTheTotal() =>
        Assert.Equal("Tonic, 3 of 8", Focus().Positioned("Tonic"));

    [Fact]
    public void WithPositionsOffTheItemIsSaidAlone() =>
        Assert.Equal("Tonic", Focus(say: false).Positioned("Tonic"));

    [Fact]
    public void WithPositionsOffSeveralNamesStillKeepTheirOrderAndSkipTheUnknown() =>
        Assert.Equal("Buy, Tonic", Focus(say: false).Positioned("Buy", null, "Tonic"));

    [Fact]
    public void AnItemWithNoNameIsSaidByItsNumberWhateverTheSetting()
    {
        string? none = null;
        Assert.Equal("3 of 8", Focus().Positioned(none));
        Assert.Equal("3", Focus(say: false).Positioned(none));
        Assert.Equal("3", Focus(say: false).Position());
    }

    [Fact]
    public void AListWhoseCountIsUnknownSaysTheNumberOnly() =>
        Assert.Equal("Tonic, 3", Focus(count: null).Positioned("Tonic"));

    [Fact]
    public void TheScreensTakeTheSettingAtEachEvent()
    {
        var say = true;
        var screens = new MenuScreens([], () => say);
        var heard = new List<bool>();
        screens.Observe(new Listener(heard));
        screens.MenusRead(open: true);
        var cursors = new CursorEvents();
        cursors.Observe(screens);

        void Move(int row) => cursors.Handled(
            new CursorInput(0xc0, 0x100, 0x1, Result: 1, Top: 0, Row: row, Flags: 0x46, "Item@ope@ui@app", 8));

        Move(0);
        say = false;
        Move(1);

        Assert.Equal([true, false], heard);
    }

    private sealed class Listener(List<bool> heard) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => heard.Add(change.SayPositions);
    }
}
