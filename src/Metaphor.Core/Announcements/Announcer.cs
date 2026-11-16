namespace Metaphor.Core.Announcements;

/// <summary>Text for the screen reader to speak (glossary: announcement).</summary>
/// <param name="Text">What is said.</param>
/// <param name="Interrupt">Whether it stops what is still being said. False queues it after.</param>
public sealed record Announcement(string Text, bool Interrupt);

/// <summary>The one interface every feature hands its announcements to (ADR 0004). Behind it sit the speech backend
/// and, in dev builds, the transcript. Implementations run inside the game, so a failure to speak or to record never
/// throws; only a null announcement, a caller's bug, does.</summary>
public interface IAnnouncer
{
    void Announce(Announcement announcement);
}

/// <summary>An announcer that says nothing: what features speak through when no speech backend could start, so they
/// never need to check.</summary>
public sealed class SilentAnnouncer : IAnnouncer
{
    public void Announce(Announcement announcement)
    {
    }
}

/// <summary>An announcer that only remembers what it was asked to say, in order: for tests.</summary>
public sealed class RecordingAnnouncer : IAnnouncer
{
    private readonly List<Announcement> said = [];

    /// <summary>Every announcement so far, in order.</summary>
    public IReadOnlyList<Announcement> Said => said;

    public void Announce(Announcement announcement) => said.Add(announcement);
}
