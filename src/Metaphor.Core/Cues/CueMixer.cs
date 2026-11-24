namespace Metaphor.Core.Cues;

/// <summary>The cue channels (ADR 0019): a fixed set, each its own output stream, so their sounds overlap and Windows
/// mixes them. <see cref="CueChannels"/> play one-shot cues; each <see cref="ToneChannel"/> holds one tone. Not
/// thread-safe: <see cref="CuePlayer"/>'s thread owns it, and <see cref="Pump"/> is called while anything plays.
/// </summary>
/// <remarks>One output handle per channel, with stereo panning, follows the approach of BlindGuyNW's FF6ScreenReader
/// (CREDITS.md); no code was taken from it, and this is written from Microsoft's waveOut documentation.</remarks>
public sealed class CueMixer : IDisposable
{
    /// <summary>How many one-shot cues can sound at once; another one is dropped.</summary>
    public const int CueChannels = 2;

    /// <summary>Buffers per channel.</summary>
    public const int Slots = 4;

    /// <summary>Frames per buffer: 20 ms, so at most 80 ms is queued and a held tone's change is heard within that.
    /// </summary>
    public const int FramesPerSlot = CueWave.SampleRate / 50;

    private static readonly int ToneChannels = Enum.GetValues<ToneChannel>().Length;

    private readonly IWaveDevice device;
    private readonly Action<string> log;
    private readonly Channel[] channels;
    private readonly short[] scratch = new short[FramesPerSlot * 2];
    private bool failed;

    /// <param name="device">Where each channel's stream is opened, the first time it plays.</param>
    /// <param name="log">Where the first failure to play is reported.</param>
    public CueMixer(IWaveDevice device, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(log);
        this.device = device;
        this.log = log;
        channels = [.. Enumerable.Range(0, ChannelCount).Select(_ => new Channel())];
    }

    /// <summary>Every channel: the cue channels, then one per <see cref="ToneChannel"/>.</summary>
    public static int ChannelCount => CueChannels + ToneChannels;

    /// <summary>Starts <paramref name="frames"/> (interleaved stereo) on an idle cue channel. Returns false, dropping
    /// it, when every cue channel is busy.</summary>
    public bool Play(short[] frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        for (var i = 0; i < CueChannels; i++)
        {
            var channel = channels[i];
            if (channel.Source is null && channel.Stream is not { Playing: true })
            {
                channel.Source = new OneShot(frames);
                return true;
            }
        }
        return false;
    }

    /// <summary>Starts the tone on its channel, or moves the one already held there to it: the pitch changes without
    /// a break in the wave, and the loudness and position glide over the next buffer.</summary>
    /// <param name="channel">The tone's channel.</param>
    /// <param name="tone">Its pitch, place and volume; values out of range are clamped.</param>
    /// <param name="masterVolume">The sound cue volume setting, 0 to 100.</param>
    /// <param name="restart">Whether this is a new hold: a tone's <see cref="HeldTone.Sample"/> that doesn't loop plays
    /// from its start on a new hold, and only then; one that loops starts only when it isn't the sound held already.
    /// </param>
    public void Hold(ToneChannel channel, HeldTone tone, int masterVolume, bool restart = false)
    {
        var slot = channels[CueChannels + (int)channel];
        var (left, right) = CueWave.PanGains(tone.Pan);
        var peak = CueWave.Peak(Math.Clamp(masterVolume, 0, 100)) * Gain(tone.Volume);
        if (tone.Sample is { } sample)
        {
            var playing = slot.Source as Sampled;
            if (playing is null || playing.Sample != sample || (restart && !tone.Loops))
            {
                if (!restart && !tone.Loops)
                {
                    // A sound that has played through stays finished until it is held anew.
                    return;
                }
                playing = new Sampled(sample, tone.Loops);
                slot.Source = playing;
            }
            playing.Target(peak * left, peak * right);
            return;
        }
        var hertz = double.IsNaN(tone.Hertz)
            ? HeldTone.MinHertz
            : Math.Clamp(tone.Hertz, HeldTone.MinHertz, HeldTone.MaxHertz);
        if (slot.Source is not Held held)
        {
            held = new Held();
            slot.Source = held;
        }
        held.Target(hertz, peak * left, peak * right);
    }

    /// <summary>Fades the channel's tone out over one buffer, then leaves the channel idle.</summary>
    public void Silence(ToneChannel channel)
    {
        if (channels[CueChannels + (int)channel].Source is IHeld held)
        {
            held.FadeOut();
        }
    }

    /// <summary>Takes back the buffers that have played and refills them. Returns true while any channel has sound
    /// queued or still to come, so the caller knows to call again soon.</summary>
    public bool Pump()
    {
        var busy = false;
        foreach (var channel in channels)
        {
            try
            {
                Refill(channel);
            }
#pragma warning disable CA1031 // A device failure (no device, a driver error) must never end the thread or reach the game.
            catch (Exception e)
#pragma warning restore CA1031
            {
                Fail(e);
                channel.Source = null;
                Close(channel);
            }
            busy |= channel.Source is not null || channel.Stream is { Playing: true };
        }
        return busy;
    }

    /// <summary>Stops every channel and closes its stream.</summary>
    public void Dispose()
    {
        foreach (var channel in channels)
        {
            channel.Source = null;
            Close(channel);
        }
    }

    private void Refill(Channel channel)
    {
        channel.Stream?.Reclaim();
        while (channel.Source is { } source)
        {
            channel.Stream ??= new WaveSlots(device.Open(Slots, FramesPerSlot), Slots);
            if (channel.Stream.FreeSlots == 0)
            {
                return;
            }
            var count = source.Render(scratch);
            if (count == 0)
            {
                channel.Source = null;
                return;
            }
            channel.Stream.Write(scratch.AsSpan(0, count * 2));
        }
    }

    private void Close(Channel channel)
    {
        if (channel.Stream is not { } stream)
        {
            return;
        }
        channel.Stream = null;
        try
        {
            if (!stream.Close())
            {
                Fail(new InvalidOperationException("a buffer was still queued after reset; its memory is kept"));
            }
        }
#pragma warning disable CA1031 // As in Pump: a stream that won't close is dropped, its buffers kept.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Fail(e);
        }
    }

    /// <summary>Reports a failure to play that happened outside the mixer, once, like the mixer's own.</summary>
    public void Report(Exception e) => Fail(e);

    private void Fail(Exception e)
    {
        if (!failed)
        {
            failed = true;
            log($"sound cue failed to play ({e.Message}); later failures are not logged");
        }
    }

    // A held tone's own volume, 0 to 100, on the same square curve as the setting.
    private static double Gain(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 100) / 100.0;
        return clamped * clamped;
    }

    private sealed class Channel
    {
        public ISource? Source { get; set; }

        public WaveSlots? Stream { get; set; }
    }

    private interface ISource
    {
        /// <summary>Fills <paramref name="frames"/> from the start and returns how many frames it wrote; 0 once
        /// finished.</summary>
        int Render(Span<short> frames);
    }

    private sealed class OneShot(short[] frames) : ISource
    {
        private int position;

        public int Render(Span<short> buffer)
        {
            var count = Math.Min(buffer.Length, frames.Length - position);
            frames.AsSpan(position, count).CopyTo(buffer);
            position += count;
            return count / 2;
        }
    }

    private interface IHeld : ISource
    {
        /// <summary>Glides to silence over the next buffer, then finishes.</summary>
        void FadeOut();
    }

    // A recorded sound (#841), its gains gliding as a held tone's do: once through and then finished, or looping.
    private sealed class Sampled(CueSample sample, bool loops) : IHeld
    {
        private int position;
        private double left;
        private double right;
        private double targetLeft;
        private double targetRight;
        private bool stopping;
        private bool silent = true;

        public CueSample Sample => sample;

        public void Target(double left, double right)
        {
            targetLeft = left;
            targetRight = right;
            stopping = false;
        }

        public void FadeOut()
        {
            targetLeft = 0;
            targetRight = 0;
            stopping = true;
        }

        public int Render(Span<short> buffer)
        {
            var samples = sample.Samples;
            if ((stopping && silent) || (!loops && position >= samples.Length))
            {
                return 0;
            }
            var count = buffer.Length / 2;
            if (!loops)
            {
                count = Math.Min(count, samples.Length - position);
            }
            for (var i = 0; i < count; i++)
            {
                var t = (i + 1) / (double)count;
                var wave = samples[position];
                buffer[2 * i] = (short)Math.Round(wave * (left + ((targetLeft - left) * t)));
                buffer[(2 * i) + 1] = (short)Math.Round(wave * (right + ((targetRight - right) * t)));
                position = loops && position + 1 == samples.Length ? 0 : position + 1;
            }
            left = targetLeft;
            right = targetRight;
            silent = left == 0 && right == 0;
            return count;
        }
    }

    private sealed class Held : IHeld
    {
        private double phase;
        private double hertz;
        private double left;
        private double right;
        private double targetLeft;
        private double targetRight;
        private bool stopping;
        private bool silent = true;

        public void Target(double hertz, double left, double right)
        {
            this.hertz = hertz;
            targetLeft = left;
            targetRight = right;
            stopping = false;
        }

        public void FadeOut()
        {
            targetLeft = 0;
            targetRight = 0;
            stopping = true;
        }

        public int Render(Span<short> buffer)
        {
            // Finished once a stop has faded to silence.
            if (stopping && silent)
            {
                return 0;
            }
            var count = buffer.Length / 2;
            var step = 2 * Math.PI * hertz / CueWave.SampleRate;
            for (var i = 0; i < count; i++)
            {
                // The gains move linearly across the buffer, from where the last one ended to the target.
                var t = (i + 1) / (double)count;
                var wave = Math.Sin(phase);
                buffer[2 * i] = (short)Math.Round(wave * (left + ((targetLeft - left) * t)));
                buffer[(2 * i) + 1] = (short)Math.Round(wave * (right + ((targetRight - right) * t)));
                phase += step;
            }
            phase %= 2 * Math.PI;
            left = targetLeft;
            right = targetRight;
            silent = left == 0 && right == 0;
            return count;
        }
    }
}
