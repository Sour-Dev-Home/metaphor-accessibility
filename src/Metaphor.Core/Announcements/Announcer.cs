namespace Metaphor.Core.Announcements;

/// <summary>Text for the screen reader to speak (glossary: announcement).</summary>
/// <param name="Text">What is said.</param>
/// <param name="Interrupt">Whether it stops what is still being said. False queues it after.</param>
/// <param name="Private">Whether the text is personal data, such as the name the player types (#585): it is spoken but
/// never recorded, so the transcript holds its length only.</param>
public sealed record Announcement(string Text, bool Interrupt, bool Private = false)
{
    /// <summary>The announcement as a record would show it: for a private one, its length only.</summary>
    public override string ToString() => Private
        ? $"Announcement {{ Private, {Text.Length} characters, Interrupt = {Interrupt} }}"
        : $"Announcement {{ Text = {Text}, Interrupt = {Interrupt} }}";
}

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
