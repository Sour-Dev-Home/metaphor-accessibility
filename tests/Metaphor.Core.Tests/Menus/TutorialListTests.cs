using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The System tutorial list's row numbers (#806): a row's number is read only when the list's count holds it,
/// so the previous tab's numbers past the count are never read.</summary>
[Trait("Category", "Unit")]
public sealed class TutorialListTests
{
    [Theory]
    [InlineData(0, 33, 0x1488)]
    [InlineData(11, 33, 0x14b4)]
    [InlineData(32, 33, 0x1508)]
    public void ARowInsideTheCountIsReadFourBytesPerRow(int row, int count, int offset) =>
        Assert.Equal(offset, TutorialList.NumberOffset(row, count));

    [Theory]
    [InlineData(33, 33)]
    [InlineData(-1, 33)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(0, TutorialList.MaxRows + 1)]
    public void ARowPastTheCountOrAnUnlikelyCountReadsNothing(int row, int count) =>
        Assert.Null(TutorialList.NumberOffset(row, count));

    [Fact]
    public void AnUnreadCountReadsNothing() => Assert.Null(TutorialList.NumberOffset(0, null));
}
