using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Announcements;

/// <summary>One started speech backend: a Prism backend in the hook layer, a fake in tests. Statuses are
/// <c>PrismError</c> values, 0 for success (<c>docs/raw-sources/prism-output-braille-2026-11-16.md</c>).</summary>
public interface ISpeechBackend : IDisposable
{
    /// <summary>The backend's name for the log, such as NVDA or OneCore.</summary>
    string Name { get; }

    /// <summary>Speaks the text and sends it to braille where the backend can.</summary>
    int Output(string text, bool interrupt);

    /// <summary>Speaks the text.</summary>
    int Speak(string text, bool interrupt);
}

/// <summary>The announcer that moves to a screen reader started after the game, and back to the Windows voice when it
/// quits (#527). It looks for a screen reader at most once per <see cref="ProbeInterval"/>, off the speaking thread, and
/// only when there is a reason: while it isn't on one, or after a line failed. A line that failed on a reader that has
/// since quit is said again on the backend it moves to, the last such line only; a line that failed on a reader still
/// running isn't, since NVDA's <c>output</c> fails the same way when its braille failed after the speech
/// (<c>docs/raw-sources/prism-backend-redetection-2026-11-20.md</c>). Every change of backend is logged as
/// <c>speech: name</c>.</summary>
public sealed class SwitchingAnnouncer : IAnnouncer
{
    /// <summary>How often, at most, it looks for a screen reader.</summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(3);

    /// <summary>The log line when nothing can speak.</summary>
    public const string NoBackend = "speech unavailable: no speech backend is available";

    private readonly object gate = new();
    private readonly ISpeechBackend? fallback;
    private readonly Func<ISpeechBackend?> findScreenReader;
    private readonly Action<string> log;
    private readonly TimeProvider clock;
    private readonly Action<Action> inBackground;
    private ISpeechBackend? current;
    private OutputRoute route = new();
    private Announcement? unsaid;
    private bool lineFailed;
    private bool probing;
    private long lastProbe;

    /// <param name="screenReader">The screen reader started at load, or null.</param>
    /// <param name="fallback">The Windows voice started at load, used whenever no screen reader is running; or null.
    /// It is never disposed, so moving back to it costs nothing.</param>
    /// <param name="findScreenReader">Starts the first screen reader that is running, in order of preference, or
    /// returns null. Runs on <paramref name="inBackground"/>'s thread; must not throw, though a throw is caught.</param>
    /// <param name="log">Where a change of backend is reported.</param>
    /// <param name="clock">Times <see cref="ProbeInterval"/>; the last probe counts as now, since the caller just
    /// started its backends.</param>
    /// <param name="inBackground">Runs a probe off the speaking thread: the thread pool in the game.</param>
    public SwitchingAnnouncer(
        ISpeechBackend? screenReader,
        ISpeechBackend? fallback,
        Func<ISpeechBackend?> findScreenReader,
        Action<string> log,
        TimeProvider clock,
        Action<Action> inBackground)
    {
        this.fallback = fallback;
        this.findScreenReader = findScreenReader;
        this.log = log;
        this.clock = clock;
        this.inBackground = inBackground;
        current = screenReader ?? fallback;
        lastProbe = clock.GetTimestamp();
    }

    /// <summary>Which backend is speaking, or null while none is.</summary>
    public string? BackendName
    {
        get
        {
            lock (gate)
            {
                return current?.Name;
            }
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Speech failing must never reach the game.")]
    public void Announce(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        try
        {
            lock (gate)
            {
                if (Say(announcement))
                {
                    unsaid = null;
                }
                else
                {
                    unsaid = announcement;
                    lineFailed = true;
                }
                if (!ProbeDue())
                {
                    return;
                }
                probing = true;
                lineFailed = false;
                lastProbe = clock.GetTimestamp();
            }
        }
        catch (Exception)
        {
            // Nothing to tell: the transcript (Debug builds) still has the line.
            return;
        }
        try
        {
            inBackground(Probe);
        }
        catch (Exception)
        {
            lock (gate)
            {
                probing = false; // The next due line tries again.
            }
        }
    }

    /// <summary>Says the line on the current backend, under the lock; false if it failed or nothing can speak.</summary>
    private bool Say(Announcement announcement)
    {
        if (current is null)
        {
            return false;
        }
        if (route.TriesOutput)
        {
            var status = current.Output(announcement.Text, announcement.Interrupt);
            if (!route.SpeakInstead(status))
            {
                return status == 0;
            }
        }
        return current.Speak(announcement.Text, announcement.Interrupt) == 0;
    }

    private bool ProbeDue() =>
        !probing &&
        (current is null || current == fallback || lineFailed) &&
        clock.GetElapsedTime(lastProbe) >= ProbeInterval;

    [SuppressMessage("Design", "CA1031", Justification = "A probe runs on a thread-pool thread, where a throw ends the game.")]
    private void Probe()
    {
        ISpeechBackend? found = null;
        try
        {
            found = findScreenReader();
        }
        catch (Exception)
        {
            // As if none was running.
        }
        try
        {
            lock (gate)
            {
                probing = false;
                var previous = current;
                var onScreenReader = previous is not null && previous != fallback;
                if (found is null && !onScreenReader)
                {
                    return; // Still on the fallback, or still silent.
                }
                try
                {
                    SwitchTo(found ?? fallback, previous, onScreenReader);
                }
                catch (Exception)
                {
                    // A backend threw mid-switch: release both readers and settle on the fallback, so no handle is
                    // left behind (#532). Dispose is idempotent, so releasing one already released is harmless.
                    if (onScreenReader)
                    {
                        Release(previous!);
                    }
                    if (found is not null)
                    {
                        Release(found);
                    }
                    current = fallback;
                    route = new OutputRoute();
                    unsaid = null;
                }
            }
        }
        catch (Exception)
        {
            // Nothing to tell; the next line tries again.
        }
    }

    /// <summary>Moves from <paramref name="previous"/> to <paramref name="next"/>, under the lock. Throws only if
    /// <paramref name="next"/> or the log does.</summary>
    private void SwitchTo(ISpeechBackend? next, ISpeechBackend? previous, bool releasePrevious)
    {
        var renamed = next?.Name != previous?.Name;
        current = next;
        route = new OutputRoute();
        if (releasePrevious)
        {
            // A reader that failed a line is replaced even by a fresh copy of itself, whose connection is new.
            Release(previous!);
        }
        if (!renamed)
        {
            unsaid = null; // It may have been spoken already; saying it again could double it.
            return;
        }
        if (unsaid is not null && Say(unsaid))
        {
            unsaid = null;
        }
        log(next is null ? NoBackend : $"speech: {next.Name}");
    }

    /// <summary>Disposes a backend that is no longer current; a throw changes nothing, as it is dropped either way.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A backend failing to free must not undo the switch.")]
    private static void Release(ISpeechBackend backend)
    {
        try
        {
            backend.Dispose();
        }
        catch (Exception)
        {
            // Dropped either way.
        }
    }
}
