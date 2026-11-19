namespace Metaphor.Core.Tests;

/// <summary>Deciding from <c>VirtualQuery</c>'s regions whether a read is safe (#393), against a made-up address space:
/// readable pages at 0x1000-0x3000 in two regions, a guard page at 0x3000, reserved pages at 0x4000.</summary>
[Trait("Category", "Unit")]
public sealed class PageRegionTests
{
    private const uint ReadWrite = 0x04;
    private const uint ExecuteRead = 0x20;
    private const uint NoAccess = 0x01;
    private const uint MemReserve = 0x2000;

    private static readonly PageRegion[] Space =
    [
        new(0x1000, 0x1000, PageRegion.MemCommit, ReadWrite),
        new(0x2000, 0x1000, PageRegion.MemCommit, ExecuteRead),
        new(0x3000, 0x1000, PageRegion.MemCommit, ReadWrite | PageRegion.PageGuard),
        new(0x4000, 0x1000, MemReserve, 0),
        new(0x5000, 0x1000, PageRegion.MemCommit, NoAccess),
    ];

    private readonly List<long> asked = [];

    private PageRegion? Query(long address)
    {
        asked.Add(address);
        return Space.Where(r => address >= r.Base && address < r.Base + r.Size).Select(r => (PageRegion?)r).FirstOrDefault();
    }

    [Fact]
    public void AReadWithinOneReadableRegionIsAllowed() =>
        Assert.True(PageRegion.AllReadable(0x1010, 16, Query));

    [Fact]
    public void AReadAcrossTwoReadableRegionsAsksForEach()
    {
        Assert.True(PageRegion.AllReadable(0x1ff0, 0x20, Query));
        Assert.Equal([0x1ff0, 0x2000], asked);
    }

    [Theory]
    [InlineData(0x3000, 1)] // a guard page
    [InlineData(0x2ff8, 16)] // running into one
    [InlineData(0x4000, 8)] // reserved, not committed
    [InlineData(0x5000, 8)] // committed, no access
    [InlineData(0x10, 8)] // nothing there: the query fails
    public void AReadTouchingAPageThatIsNotReadableIsRefused(long address, int count) =>
        Assert.False(PageRegion.AllReadable(address, count, Query));

    [Fact]
    public void AReadThatWouldWrapPastTheTopOfTheAddressSpaceIsRefused() =>
        Assert.False(PageRegion.AllReadable(long.MaxValue - 3, 8, Query));

    [Fact]
    public void AQueryThatDoesNotCoverTheAddressIsRefusedRatherThanLoopedOn() =>
        Assert.False(PageRegion.AllReadable(0x1000, 8, _ => new PageRegion(0x2000, 0x1000, PageRegion.MemCommit, ReadWrite)));

    [Fact]
    public void ProtectionIsIgnoredOutsideCommittedPages() =>
        Assert.False(new PageRegion(0, 0x1000, MemReserve, ReadWrite).IsReadable);
}
