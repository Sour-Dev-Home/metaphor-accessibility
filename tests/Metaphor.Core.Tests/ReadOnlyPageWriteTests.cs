namespace Metaphor.Core.Tests;

/// <summary>The target cursor's slot is on a read-only page, and a plain write is refused there (error 998 in game,
/// 2026-11-24, #253): the page is made writable for the write and its protection always put back. Made-up calls only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReadOnlyPageWriteTests
{
    private const long Slot = 0x1422502a0;
    private const long Value = 0x7ff0_1234_5000;
    private const uint ReadOnly = 0x02;

    private readonly List<string> calls = [];

    private uint? Protect(long address, uint protection)
    {
        calls.Add($"protect 0x{address:x} 0x{protection:x}");
        return ReadOnly;
    }

    [Fact]
    public void ThePageIsMadeWritableBeforeTheWriteAndPutBackAfter()
    {
        var written = ReadOnlyPageWrite.WritePointer(Slot, Value, Protect, (at, value) =>
        {
            calls.Add($"write 0x{at:x} 0x{value:x}");
            return true;
        });

        Assert.True(written);
        Assert.Equal(
            ["protect 0x1422502a0 0x4", "write 0x1422502a0 0x7ff012345000", "protect 0x1422502a0 0x2"], calls);
    }

    [Fact]
    public void AFailedWriteStillPutsTheProtectionBack()
    {
        var written = ReadOnlyPageWrite.WritePointer(Slot, Value, Protect, (_, _) => false);

        Assert.False(written);
        Assert.Equal("protect 0x1422502a0 0x2", calls[^1]);
    }

    [Fact]
    public void AWriteThatThrowsStillPutsTheProtectionBack()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReadOnlyPageWrite.WritePointer(Slot, Value, Protect, (_, _) => throw new InvalidOperationException()));

        Assert.Equal("protect 0x1422502a0 0x2", calls[^1]);
    }

    [Fact]
    public void NothingIsWrittenWhenThePageCantBeMadeWritable()
    {
        var wrote = false;

        var written = ReadOnlyPageWrite.WritePointer(Slot, Value, (_, _) => null, (_, _) => wrote = true);

        Assert.False(written);
        Assert.False(wrote);
    }

    [Fact]
    public void AProtectionThatCantBePutBackFailsTheWrite()
    {
        var protects = 0;

        var written = ReadOnlyPageWrite.WritePointer(
            Slot, Value, (_, _) => ++protects == 1 ? ReadOnly : null, (_, _) => true);

        Assert.False(written);
    }
}
