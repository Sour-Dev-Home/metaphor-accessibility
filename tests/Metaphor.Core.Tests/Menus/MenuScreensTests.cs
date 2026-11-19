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
}
