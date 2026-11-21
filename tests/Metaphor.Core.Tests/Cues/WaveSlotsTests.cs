using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>The buffer order waveOut requires (#637): a slot's memory is reused only after the device hands it back,
/// and the stream closes only after a reset has handed back every slot.</summary>
[Trait("Category", "Unit")]
public sealed class WaveSlotsTests
{
    private static readonly short[] Frames = [1, 2, 3, 4];

    [Fact]
    public void ASlotIsWrittenAgainOnlyAfterTheDeviceReleasesIt()
    {
        var output = Open(out var stream, slots: 2);
        stream.Write(Frames);
        stream.Write(Frames);

        stream.Reclaim();
        Assert.Equal(0, stream.FreeSlots);
        output.Finish(1);
        stream.Reclaim();
        Assert.Equal(1, stream.FreeSlots);
        stream.Write(Frames);

        Assert.Equal(["write 0", "write 1", "still 0", "release 0", "still 1", "write 0"], output.Events);
        Assert.Empty(output.Violations);
    }

    [Fact]
    public void WritingWithNoFreeSlotIsRefused()
    {
        var output = Open(out var stream, slots: 1);
        stream.Write(Frames);

        Assert.Throws<InvalidOperationException>(() => stream.Write(Frames));
        Assert.Single(output.Buffers);
    }

    [Fact]
    public void ClosingResetsThenReleasesEverySlotOldestFirstThenCloses()
    {
        var output = Open(out var stream, slots: 3);
        stream.Write(Frames);
        stream.Write(Frames);

        Assert.True(stream.Close());

        Assert.Equal(["write 0", "write 1", "reset", "release 0", "release 1", "close"], output.Events);
        Assert.Empty(output.Violations);
    }

    [Fact]
    public void AStreamWhoseSlotWontReleaseIsNeverClosedSoItsBuffersAreKept()
    {
        var output = Open(out var stream, slots: 2);
        stream.Write(Frames);
        output.Stuck = true;

        Assert.False(stream.Close());

        Assert.False(output.Closed);
        Assert.DoesNotContain("close", output.Events);
    }

    [Fact]
    public void AFailedWriteLeavesTheSlotFree()
    {
        var output = new ThrowingOut();
        var stream = new WaveSlots(output, 1);

        Assert.Throws<InvalidOperationException>(() => stream.Write(Frames));

        Assert.Equal(1, stream.FreeSlots);
        Assert.False(stream.Playing);
    }

    private static FakeWaveOut Open(out WaveSlots stream, int slots)
    {
        var output = (FakeWaveOut)new FakeWaveDevice().Open(slots, 4);
        stream = new WaveSlots(output, slots);
        return output;
    }

    private sealed class ThrowingOut : IWaveOut
    {
        public void Write(int slot, ReadOnlySpan<short> frames) => throw new InvalidOperationException("refused");

        public SlotState Release(int slot) => SlotState.Released;

        public void Reset()
        {
        }

        public void Close()
        {
        }
    }
}
