using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>A wave device that plays nothing. Each stream keeps what was written to it and checks the waveOut rules a
/// real device relies on: a slot is written only when free, and a stream is closed only with nothing queued.</summary>
internal sealed class FakeWaveDevice : IWaveDevice
{
    private readonly List<FakeWaveOut> opened = [];

    /// <summary>Whether slots finish as soon as they're written, as if played instantly; otherwise
    /// <see cref="FakeWaveOut.Finish"/> finishes them.</summary>
    public bool Instant { get; init; }

    /// <summary>How many opens fail before one succeeds.</summary>
    public int FailuresLeft { get; set; }

    /// <summary>Called on the writing thread before each write, so a test can hold the player there.</summary>
    public Action? BeforeWrite { get; init; }

    /// <summary>Set after each write.</summary>
    public SemaphoreSlim Writes { get; } = new(0);

    public IReadOnlyList<FakeWaveOut> Opened
    {
        get
        {
            lock (opened)
            {
                return [.. opened];
            }
        }
    }

    public IWaveOut Open(int slots, int framesPerSlot)
    {
        lock (opened)
        {
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new InvalidOperationException("no device");
            }
            var output = new FakeWaveOut(this, slots, framesPerSlot);
            opened.Add(output);
            return output;
        }
    }

    internal void Wrote() => Writes.Release();
}

internal sealed class FakeWaveOut(FakeWaveDevice device, int slots, int framesPerSlot) : IWaveOut
{
    private readonly object gate = new();
    private readonly List<int> queued = [];
    private readonly HashSet<int> finished = [];
    private readonly List<short> frames = [];
    private readonly List<string> events = [];
    private readonly List<string> violations = [];

    /// <summary>Slots that report still playing even after a reset, like a device that won't let go.</summary>
    public bool Stuck { get; set; }

    public bool Closed { get; private set; }

    /// <summary>Everything written, in order, as interleaved left and right samples.</summary>
    public short[] Frames
    {
        get
        {
            lock (gate)
            {
                return [.. frames];
            }
        }
    }

    /// <summary>Each write's interleaved samples.</summary>
    public List<short[]> Buffers { get; } = [];

    /// <summary>Calls in order: "write 0", "release 0", "still 0", "reset", "close".</summary>
    public IReadOnlyList<string> Events
    {
        get
        {
            lock (gate)
            {
                return [.. events];
            }
        }
    }

    public IReadOnlyList<string> Violations
    {
        get
        {
            lock (gate)
            {
                return [.. violations];
            }
        }
    }

    public int Queued
    {
        get
        {
            lock (gate)
            {
                return queued.Count;
            }
        }
    }

    /// <summary>Plays the oldest <paramref name="count"/> queued slots to the end.</summary>
    public void Finish(int count)
    {
        lock (gate)
        {
            foreach (var slot in queued.Where(slot => !finished.Contains(slot)).Take(count).ToList())
            {
                finished.Add(slot);
            }
        }
    }

    public void Write(int slot, ReadOnlySpan<short> samples)
    {
        device.BeforeWrite?.Invoke();
        lock (gate)
        {
            if (slot < 0 || slot >= slots || queued.Contains(slot))
            {
                violations.Add($"slot {slot} written while queued or out of range");
            }
            if (samples.Length > framesPerSlot * 2 || samples.Length % 2 != 0)
            {
                violations.Add($"slot {slot} written with {samples.Length} samples");
            }
            if (Closed)
            {
                violations.Add("written after close");
            }
            events.Add($"write {slot}");
            queued.Add(slot);
            if (device.Instant)
            {
                finished.Add(slot);
            }
            frames.AddRange(samples.ToArray());
            Buffers.Add(samples.ToArray());
        }
        device.Wrote();
    }

    public SlotState Release(int slot)
    {
        lock (gate)
        {
            if (queued.Count > 0 && queued[0] != slot)
            {
                violations.Add($"slot {slot} released before the older slot {queued[0]}");
            }
            if (!finished.Contains(slot) || Stuck)
            {
                events.Add($"still {slot}");
                return SlotState.StillPlaying;
            }
            events.Add($"release {slot}");
            finished.Remove(slot);
            queued.Remove(slot);
            return SlotState.Released;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            events.Add("reset");
            foreach (var slot in queued)
            {
                finished.Add(slot);
            }
        }
    }

    public void Close()
    {
        lock (gate)
        {
            if (queued.Count > 0)
            {
                violations.Add("closed with slots still queued");
            }
            events.Add("close");
            Closed = true;
        }
    }
}
