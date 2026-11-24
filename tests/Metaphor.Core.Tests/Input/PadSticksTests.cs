using Metaphor.Core.Input;

namespace Metaphor.Core.Tests.Input;

/// <summary>The pad's sticks as the game keeps them (#701, #184): 0 to 255 around 128, y growing down
/// (docs/findings/pad-sticks-in-pad-state-static.md).</summary>
[Trait("Category", "Unit")]
public sealed class PadSticksTests
{
    [Fact]
    public void UpOnAStickIsBelowTheCentre()
    {
        var sticks = PadSticks.Rest with { RightX = 255, RightY = 1, LeftX = 1, LeftY = 255 };

        Assert.Equal((1.0, 1.0), sticks.Right);
        Assert.Equal((-1.0, -1.0), sticks.Left);
    }

    [Theory]
    [InlineData(0, 1, 128, 1)]
    [InlineData(1, 0, 255, 128)]
    [InlineData(-2, -2, 1, 255)]
    public void TheLeftStickIsPushedWithinItsReach(double x, double y, int leftX, int leftY)
    {
        var pushed = PadSticks.Rest.WithLeft(x, y);

        Assert.Equal(PadSticks.Rest with { LeftX = leftX, LeftY = leftY }, pushed);
    }

    [Fact]
    public void AtRestNothingMoved()
    {
        Assert.False(PadSticks.Rest.Moved);
        Assert.True((PadSticks.Rest with { LeftX = 129 }).Moved);
        Assert.Equal(PadSticks.Rest, (PadSticks.Rest with { RightX = 0, RightY = 0 }).RightAtRest());
    }
}
