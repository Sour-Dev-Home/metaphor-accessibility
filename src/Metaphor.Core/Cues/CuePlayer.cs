using System.Collections.Concurrent;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Cues;

/// <summary>A channel that holds one continuous tone, which a feature starts, changes and stops (ADR 0019).</summary>
public enum ToneChannel
{
    /// <summary>Guidance toward a target (#183).</summary>
    Guidance,

    /// <summary>Walls and obstacles nearby (#185).</summary>
    Wall,

    /// <summary>The field sonar's pings for nearby people, objects and exits (#827).</summary>
    Sonar,
}

/// <summary>A held tone: a sine at <paramref name="Hertz"/>, placed at <paramref name="Pan"/> from -1 (left) to +1
/// (right), at <paramref name="Volume"/> from 0 to 100 within the sound cue volume. Values out of range are clamped.
/// </summary>
public readonly record struct HeldTone(double Hertz, double Pan, int Volume)
{
    /// <summary>The lowest pitch a held tone plays.</summary>
    public const double MinHertz = 40;

    /// <summary>The highest pitch a held tone plays.</summary>
    public const double MaxHertz = 4000;
}

/// <summary>Where features play sound cues (#636, #637, ADR 0019). Every call returns at once from any thread, the
/// game's included, and never throws for a sound that can't be played.</summary>
public interface ICuePlayer
{
    /// <summary>Plays the cue in the centre.</summary>
    void Play(Cue cue);

    /// <summary>Plays the cue at <paramref name="pan"/>, from -1 (left) to +1 (right).</summary>
    void Play(Cue cue, double pan);

    /// <summary>Starts the tone on <paramref name="channel"/>, or changes the one held there.</summary>
    void Hold(ToneChannel channel, HeldTone tone);

    /// <summary>Fades out the tone held on <paramref name="channel"/>.</summary>
    void Silence(ToneChannel channel);
}

/// <summary>Plays nothing: for features with no player, and for tests that don't listen.</summary>
public sealed class SilentCuePlayer : ICuePlayer
{
    public void Play(Cue cue)
    {
    }

    public void Play(Cue cue, double pan)
    {
    }

    public void Hold(ToneChannel channel, HeldTone tone)
    {
    }

    public void Silence(ToneChannel channel)
    {
    }
}

/// <summary>Plays cues and held tones through a <see cref="CueMixer"/> on a thread of its own, so the caller never
/// waits. The settings are read at each cue and while tones are held: off or volume 0 plays nothing and silences held
/// tones until they're back on. A cue that arrives while <see cref="Backlog"/> are already waiting, or while every cue
/// channel is busy, is dropped, since a cue heard late would describe the wrong moment.</summary>
public sealed class CuePlayer : ICuePlayer, IDisposable
{
    /// <summary>How many cues may wait for the player's thread.</summary>
    public const int Backlog = 4;

    // How often the thread refills buffers while anything plays: well inside the 80 ms the mixer queues.
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan SettingsPoll = TimeSpan.FromMilliseconds(250);

    private readonly CueMixer mixer;
    private readonly Func<ModSettings> settings;
    private readonly ConcurrentQueue<(Cue Cue, double Pan, int Volume)> waiting = new();
    private readonly ToneRequest?[] requests = new ToneRequest?[Enum.GetValues<ToneChannel>().Length];
    private readonly HeldTone?[] held = new HeldTone?[Enum.GetValues<ToneChannel>().Length];
    private readonly Dictionary<(Cue Cue, int Volume), short[]> rendered = [];
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread worker;
    private int waitingCount;
    private volatile bool disposing;

    /// <param name="device">Where the mixer opens its channels.</param>
    /// <param name="settings">The settings in force.</param>
    /// <param name="log">Where the first failure to play is reported.</param>
    public CuePlayer(IWaveDevice device, Func<ModSettings> settings, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        mixer = new CueMixer(device, log);
        this.settings = settings;
        worker = new Thread(Run) { IsBackground = true, Name = "Metaphor sound cues" };
        worker.Start();
    }

    public void Play(Cue cue) => Play(cue, 0);

    public void Play(Cue cue, double pan)
    {
        var now = settings();
        // Each cue's own setting is a share of the sound cue volume (#661).
        var own = now.Sound(cue);
        var volume = now.SoundCueVolume * own.Volume / 100;
        if (disposing || !now.SoundCues || !own.On || volume == 0)
        {
            return;
        }
        // Never blocks: a full backlog drops the cue.
        if (Interlocked.Increment(ref waitingCount) > Backlog)
        {
            Interlocked.Decrement(ref waitingCount);
            return;
        }
        waiting.Enqueue((cue, pan, volume));
        Wake();
    }

    public void Hold(ToneChannel channel, HeldTone tone) => Send(channel, new ToneRequest(tone));

    public void Silence(ToneChannel channel) => Send(channel, new ToneRequest(null));

    /// <summary>Plays the cues still waiting, fades out held tones, then stops the thread and closes the channels.
    /// </summary>
    public void Dispose()
    {
        disposing = true;
        Wake();
        worker.Join();
        mixer.Dispose();
        wake.Dispose();
    }

    private void Send(ToneChannel channel, ToneRequest request)
    {
        if (disposing)
        {
            return;
        }
        // The latest request wins, so a feature that updates a tone every frame never builds a queue.
        Interlocked.Exchange(ref requests[(int)channel], request);
        Wake();
    }

    private void Wake()
    {
        try
        {
            wake.Set();
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and the call.
        }
    }

    private void Run()
    {
        while (true)
        {
            var stopping = disposing;
            while (waiting.TryDequeue(out var next))
            {
                Interlocked.Decrement(ref waitingCount);
                try
                {
                    var samples = Rendered(next.Cue, next.Volume);
                    mixer.Play(CueWave.Stereo(samples, next.Pan));
                }
#pragma warning disable CA1031 // An exception ending this thread would end the game: a bad cue is logged and skipped.
                catch (Exception e)
#pragma warning restore CA1031
                {
                    mixer.Report(e);
                }
            }
            ApplyTones(stopping);
            var busy = mixer.Pump();
            if (stopping && !busy && waiting.IsEmpty)
            {
                return;
            }
            // A tone held while the settings silence it is checked again now and then, to come back when they allow.
            wake.WaitOne(busy ? Tick : held.Any(tone => tone is not null) ? SettingsPoll : Timeout.InfiniteTimeSpan);
        }
    }

    private void ApplyTones(bool stopping)
    {
        var now = settings();
        var audible = !stopping && now.SoundCues && now.SoundCueVolume > 0;
        for (var i = 0; i < held.Length; i++)
        {
            if (Interlocked.Exchange(ref requests[i], null) is { } request)
            {
                held[i] = request.Tone;
            }
            if (audible && held[i] is { } tone)
            {
                mixer.Hold((ToneChannel)i, tone, now.SoundCueVolume);
            }
            else
            {
                mixer.Silence((ToneChannel)i);
            }
        }
    }

    private short[] Rendered(Cue cue, int volume)
    {
        if (!rendered.TryGetValue((cue, volume), out var samples))
        {
            samples = CueWave.Samples(CueCatalogue.For(cue), volume);
            rendered[(cue, volume)] = samples;
        }
        return samples;
    }
}

/// <summary>A held tone's latest request: the tone, or null to stop.</summary>
internal sealed record ToneRequest(HeldTone? Tone);
