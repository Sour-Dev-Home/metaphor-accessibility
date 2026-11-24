namespace Metaphor.Core.Announcements;

/// <summary>The announcer in front of the speech backend that lets one source stop its own speech (#676), such as the
/// dialogue window's line when the window closes. A stop reaches the backend only while that source's line is still
/// the last thing said, so speech from anything else, queued or not, is never cut. A screen reader's stop cuts all
/// speech at once (<c>docs/raw-sources/prism-backend-stop-2026-11-21.md</c>), so this is the only way to keep the rest.
/// Safe to call from any of the game's threads; a stop and a line never cross.</summary>
public sealed class StoppableAnnouncer : IAnnouncer
{
    private readonly Lock gate = new();
    private readonly IAnnouncer speech;
    private readonly Action stop;
    private Announcement? last;

    /// <param name="speech">Where every announcement is said.</param>
    /// <param name="stop">Stops what <paramref name="speech"/> is saying; must not throw.</param>
    public StoppableAnnouncer(IAnnouncer speech, Action stop)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(stop);
        this.speech = speech;
        this.stop = stop;
    }

    public void Announce(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        // Said under the lock, so a stop can't land between the check and a newer line.
        lock (gate)
        {
            last = announcement;
            speech.Announce(announcement);
        }
    }

    /// <summary>Stops whatever is being said, from any source: the player asked for silence (#879's stop key).</summary>
    public void StopAll()
    {
        lock (gate)
        {
            last = null;
            stop();
        }
    }

    /// <summary>A new source whose speech can be stopped.</summary>
    public SpeechSource Source() => new(this);

    /// <summary>Stops speech if <paramref name="line"/>, by reference, is the last announcement said.</summary>
    internal void StopIfLast(Announcement line)
    {
        lock (gate)
        {
            if (!ReferenceEquals(last, line))
            {
                return;
            }
            last = null;
            stop();
        }
    }
}

/// <summary>One source of speech that <see cref="StoppableAnnouncer"/> can stop (#676). Its lines go through
/// <see cref="Through"/>; <see cref="Shown"/> marks the latest of them as on screen, and <see cref="StopSpeaking"/>
/// stops only that one, so a line said after the last mark, which may belong to what comes next, is left to play.
/// </summary>
public sealed class SpeechSource
{
    private readonly Lock gate = new();
    private readonly StoppableAnnouncer owner;
    private Announcement? latest;
    private Announcement? shown;

    internal SpeechSource(StoppableAnnouncer owner) => this.owner = owner;

    /// <summary>An announcer that marks what it's handed as this source's, then hands it to <paramref name="next"/>.
    /// </summary>
    public IAnnouncer Through(IAnnouncer next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return new Marking(this, next);
    }

    /// <summary>Marks the source's latest line as on screen.</summary>
    public void Shown()
    {
        lock (gate)
        {
            shown = latest;
        }
    }

    /// <summary>Stops the line last marked shown, if nothing has been said since it.</summary>
    public void StopSpeaking()
    {
        Announcement? line;
        lock (gate)
        {
            line = shown;
            shown = null;
        }
        if (line is not null)
        {
            owner.StopIfLast(line);
        }
    }

    private void Mark(Announcement announcement)
    {
        lock (gate)
        {
            latest = announcement;
        }
    }

    private sealed class Marking(SpeechSource source, IAnnouncer next) : IAnnouncer
    {
        public void Announce(Announcement announcement)
        {
            ArgumentNullException.ThrowIfNull(announcement);
            source.Mark(announcement);
            next.Announce(announcement);
        }
    }
}
