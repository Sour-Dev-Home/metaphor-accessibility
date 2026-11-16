namespace Metaphor.Core.Tests;

/// <summary>Reading strings out of the game's memory through a read function that can fail, as the hook layer's
/// <c>ReadProcessMemory</c> does on a page it can't read. A byte array stands in for memory; addresses at or past
/// <c>readableUpTo</c> fail.</summary>
[Trait("Category", "Unit")]
public sealed class MemoryTextTests
{
    private static ChunkedScan.ReadAt From(byte[] memory, long readableUpTo = long.MaxValue) => (address, buffer) =>
    {
        if (address < 0 || address + buffer.Length > Math.Min(memory.Length, readableUpTo))
        {
            return false;
        }
        memory.AsSpan((int)address, buffer.Length).CopyTo(buffer);
        return true;
    };

    private static byte[] Text(string text, int padTo = 0)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes(text);
        return bytes.Length >= padTo ? bytes : [.. bytes, .. new byte[padTo - bytes.Length]];
    }

    [Fact]
    public void AnsiStopsAtTheTerminatorInTheFirstPiece() =>
        Assert.Equal("Will", MemoryText.ReadAnsi(From(Text("Will\0Strohl", padTo: 64)), 0));

    [Fact]
    public void AnsiReturnsWhatWasReadWhenTheStringRunsIntoUnreadableMemory() =>
        // Unreadable from byte 40: the second 32-byte piece fails, and the walk goes on byte by byte to the edge.
        Assert.Equal(new string('a', 40), MemoryText.ReadAnsi(From(Text(new string('a', 64)), readableUpTo: 40), 0));

    [Fact]
    public void AnsiIsNullWhenItsStartIsUnreadable() =>
        Assert.Null(MemoryText.ReadAnsi(From(Text("Will\0"), readableUpTo: 0), 0));

    [Fact]
    public void AnsiWithoutATerminatorReturnsTheLimitsWorth() =>
        Assert.Equal("abcdef", MemoryText.ReadAnsi(From(Text("abcdefghij")), 0, maxBytes: 6));

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Utf8IsNullForANegativeOrOverCapLength(int length) =>
        Assert.Null(MemoryText.ReadUtf8(From(Text("abcdefgh")), 0, length, maxBytes: 4));

    [Fact]
    public void Utf8IsNullWhenPartOfTheRangeIsUnreadable() =>
        Assert.Null(MemoryText.ReadUtf8(From(Text("abcdefgh"), readableUpTo: 6), 0, 8));

    [Fact]
    public void Utf8DecodesAReadableRange() =>
        Assert.Equal("café", MemoryText.ReadUtf8(From([0x00, .. System.Text.Encoding.UTF8.GetBytes("café"), 0x00]), 1, 5));
}
