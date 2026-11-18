namespace Metaphor.Core.Tests;

/// <summary>Finding hook signatures in the running game a chunk at a time, so the mod never copies the whole module
/// (#39). A byte array stands in for the game's memory; tiny chunks make the chunk edges easy to hit.</summary>
[Trait("Category", "Unit")]
public sealed class ChunkedScanTests
{
    private static HookSignature Signature(string name, string pattern) => new(name, pattern, [new string('0', 64)], "e", ".ecode");

    // Reads from `memory` at an RVA; `unreadable` RVAs fail, like a page that can't be copied.
    private static ChunkedScan.ReadAt From(byte[] memory, params long[] unreadable) => (rva, buffer) =>
    {
        var length = buffer.Length;
        if (unreadable.Any(bad => bad >= rva && bad < rva + length))
        {
            return false;
        }
        memory.AsSpan((int)rva, buffer.Length).CopyTo(buffer);
        return true;
    };

    private static IReadOnlyList<long> Find(byte[] memory, string pattern, int chunkBytes, params long[] unreadable) =>
        ChunkedScan.FindAll([(0, memory.Length)], From(memory, unreadable), [Signature("s", pattern)], chunkBytes).Matches["s"];

    [Fact]
    public void SaysWhenAChunkCouldNotBeRead()
    {
        byte[] memory = [0, 0, 0, 0, 0, 0, 0, 0];

        Assert.True(ChunkedScan.FindAll([(0, 8)], From(memory), [Signature("s", "AA")], 4).Complete);
        Assert.False(ChunkedScan.FindAll([(0, 8)], From(memory, unreadable: 5), [Signature("s", "AA")], 4).Complete);
    }

    [Fact]
    public void CountsTheBytesReadAndTheBytesThatFailedOncePerChunk()
    {
        // Three regions of 10 and 3 and 4 bytes in chunks of 4; the chunk at 4 fails. The overlap isn't counted twice.
        var memory = new byte[20];

        var result = ChunkedScan.FindAll([(0, 10), (12, 3), (16, 4)], From(memory, unreadable: 5), [Signature("s", "AA BB")], 4);

        Assert.Equal(13, result.ReadableBytes);
        Assert.Equal(4, result.UnreadableBytes);
    }

    [Fact]
    public void FindsAMatchInsideOneChunk() =>
        Assert.Equal([2L], Find([0, 0, 0xAA, 0xBB, 0, 0, 0, 0], "AA BB", chunkBytes: 4));

    [Fact]
    public void FindsAMatchThatCrossesAChunkEdgeOnce() =>
        Assert.Equal([3L], Find([0, 0, 0, 0xAA, 0xBB, 0xCC, 0, 0], "AA BB CC", chunkBytes: 4));

    [Fact]
    public void CountsEachMatchOnceDespiteTheOverlap() =>
        Assert.Equal([0L, 4L], Find([0xAA, 0xBB, 0, 0, 0xAA, 0xBB, 0, 0], "AA BB", chunkBytes: 4));

    [Fact]
    public void SkipsAChunkThatFailsToReadAndScansTheRest() =>
        Assert.Equal([6L], Find([0xAA, 0xBB, 0, 0, 0, 0, 0xAA, 0xBB], "AA BB", chunkBytes: 4, unreadable: 1));

    [Fact]
    public void ReportsMatchesAsRvasAcrossRegions()
    {
        byte[] memory = [0xAA, 0, 0, 0, 0, 0, 0, 0, 0, 0xAA];
        var found = ChunkedScan.FindAll([(0, 2), (8, 2)], From(memory), [Signature("s", "AA")], chunkBytes: 4).Matches;

        Assert.Equal([0L, 9L], found["s"]);
    }

    [Fact]
    public void ReportsEverySignatureEvenWithNoMatch()
    {
        var found = ChunkedScan.FindAll([(0, 4)], From([1, 2, 3, 4]), [Signature("a", "02"), Signature("b", "09")], 4).Matches;

        Assert.Equal([1L], found["a"]);
        Assert.Empty(found["b"]);
    }
}
