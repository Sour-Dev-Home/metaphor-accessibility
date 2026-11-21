using System.Collections.Concurrent;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Cues;

/// <summary>Where features play a cue (#636, ADR 0019). <see cref="Play"/> returns at once from any thread, the game's
/// included, and never throws for a sound that can't be played.</summary>
public interface ICuePlayer
{
    void Play(Cue cue);
}

/// <summary>Plays nothing: for features with no player, and for tests that don't listen.</summary>
public sealed class SilentCuePlayer : ICuePlayer
{
    public void Play(Cue cue)
    {
    }
}

/// <summary>What sounds a WAV file. <see cref="CuePlayer"/> calls it from its own thread, one file at a time, and
/// expects it to return once the sound has finished; it may throw when the sound can't be played.</summary>
public interface ICueSink
{
    void Play(byte[] wav);
}

/// <summary>Plays cues through a sink on a thread of its own, so the caller never waits. The settings are read at
/// each cue: off or volume 0 plays nothing. A cue that arrives while <see cref="Backlog"/> are already waiting is
/// dropped, since a cue heard late would describe the wrong moment.</summary>
public sealed class CuePlayer : ICuePlayer, IDisposable
{
    /// <summary>How many cues may wait behind the one playing.</summary>
    public const int Backlog = 2;

    private readonly ICueSink sink;
    private readonly Func<ModSettings> settings;
    private readonly Action<string> log;
    private readonly BlockingCollection<(Cue Cue, int Volume)> waiting = new(Backlog);
    private readonly ConcurrentDictionary<(Cue Cue, int Volume), byte[]> rendered = new();
    private readonly Thread worker;
    private bool failed;

    /// <param name="sink">What sounds each cue.</param>
    /// <param name="settings">The settings in force, read at each cue.</param>
    /// <param name="log">Where the first failure to play is reported.</param>
    public CuePlayer(ICueSink sink, Func<ModSettings> settings, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);
        this.sink = sink;
        this.settings = settings;
        this.log = log;
        worker = new Thread(Run) { IsBackground = true, Name = "Metaphor sound cues" };
        worker.Start();
    }

    public void Play(Cue cue)
    {
        var now = settings();
        if (!now.SoundCues || now.SoundCueVolume == 0)
        {
            return;
        }
        try
        {
            // Never blocks: a full queue drops the cue.
            waiting.TryAdd((cue, now.SoundCueVolume));
        }
        catch (InvalidOperationException)
        {
            // Disposed.
        }
    }

    /// <summary>Plays what is still waiting, then stops the thread.</summary>
    public void Dispose()
    {
        waiting.CompleteAdding();
        worker.Join();
        waiting.Dispose();
    }

    private void Run()
    {
        foreach (var next in waiting.GetConsumingEnumerable())
        {
            try
            {
                sink.Play(rendered.GetOrAdd(next, key => CueWave.Wav(CueCatalogue.For(key.Cue), key.Volume)));
            }
#pragma warning disable CA1031 // A sink failure (no device, a driver error) must never end the thread or reach the game.
            catch (Exception e)
#pragma warning restore CA1031
            {
                if (!failed)
                {
                    failed = true;
                    log($"sound cue failed to play ({e.Message}); later failures are not logged");
                }
            }
        }
    }
}
