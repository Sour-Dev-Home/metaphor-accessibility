namespace Metaphor.Core.Cues;

/// <summary>Where the cue mixer opens its output streams (ADR 0019): Windows' waveOut in the game
/// (<c>WaveOutDevice</c> in the hook layer), a fake in tests.</summary>
public interface IWaveDevice
{
    /// <summary>Opens one stream of 16-bit stereo PCM at <see cref="CueWave.SampleRate"/>, with
    /// <paramref name="slots"/> buffers of up to <paramref name="framesPerSlot"/> frames each. Throws when it
    /// can't.</summary>
    IWaveOut Open(int slots, int framesPerSlot);
}

/// <summary>What <see cref="IWaveOut.Release"/> found.</summary>
public enum SlotState
{
    /// <summary>The device has finished with the slot, and its buffer may be written again.</summary>
    Released,

    /// <summary>The slot is still queued; its buffer belongs to the device.</summary>
    StillPlaying,
}

/// <summary>One open stream and the buffers it owns, one per slot. Only <see cref="WaveSlots"/> calls it, and it
/// keeps the order the waveOut documents require (<c>docs/raw-sources/winmm-waveout-2026-11-21.md</c>).</summary>
public interface IWaveOut
{
    /// <summary>Copies <paramref name="frames"/> (left and right samples, interleaved) into the slot's buffer and
    /// queues it after those already queued. Called only for a slot never written or released since. Throws when the
    /// device refuses, leaving the slot unqueued.</summary>
    void Write(int slot, ReadOnlySpan<short> frames);

    /// <summary>Tries to take the slot back from the device (waveOutUnprepareHeader).</summary>
    SlotState Release(int slot);

    /// <summary>Stops playback and marks every queued slot finished.</summary>
    void Reset();

    /// <summary>Closes the stream and frees its buffers; called only once every slot is released.</summary>
    void Close();
}

/// <summary>Keeps one <see cref="IWaveOut"/>'s slots in order: a slot is written only when free, taken back oldest
/// first, and the stream is closed only after a reset has handed every slot back. A slot the device won't release
/// keeps its buffer for good, so the device never plays freed memory.</summary>
public sealed class WaveSlots
{
    private readonly IWaveOut output;
    private readonly Queue<int> queued = new();
    private readonly Stack<int> free = new();

    public WaveSlots(IWaveOut output, int slots)
    {
        ArgumentNullException.ThrowIfNull(output);
        this.output = output;
        for (var slot = slots - 1; slot >= 0; slot--)
        {
            free.Push(slot);
        }
    }

    /// <summary>How many slots can be written now.</summary>
    public int FreeSlots => free.Count;

    /// <summary>Whether any slot is still queued.</summary>
    public bool Playing => queued.Count > 0;

    /// <summary>Takes back the slots that have finished. The device plays them in the order written, so this stops at
    /// the first one still playing.</summary>
    public void Reclaim()
    {
        while (queued.Count > 0 && output.Release(queued.Peek()) == SlotState.Released)
        {
            free.Push(queued.Dequeue());
        }
    }

    /// <summary>Queues <paramref name="frames"/> in a free slot.</summary>
    public void Write(ReadOnlySpan<short> frames)
    {
        if (free.Count == 0)
        {
            throw new InvalidOperationException("no free slot");
        }
        var slot = free.Peek();
        output.Write(slot, frames);
        queued.Enqueue(free.Pop());
    }

    /// <summary>Stops the stream and closes it. Returns false, leaving it open with its buffers, if a slot is still
    /// queued after the reset.</summary>
    public bool Close()
    {
        output.Reset();
        Reclaim();
        if (queued.Count > 0)
        {
            return false;
        }
        output.Close();
        return true;
    }
}
