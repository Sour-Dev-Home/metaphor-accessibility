using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Metaphor.Core.Announcements;

/// <summary>Writes each announcement to the transcript (ADR 0004), one line each in order, then passes it on to the
/// announcer that speaks it. Dev builds only: the hook layer wraps the speech backend in this in Debug builds. Writing
/// is best effort and never stops the speech.</summary>
public sealed class TranscriptAnnouncer : IAnnouncer
{
    /// <summary>The transcript's file name in the mod's folder; the smoke test collects it by this name.</summary>
    public const string FileName = "transcript.txt";

    private readonly IAnnouncer speech;
    private readonly string path;
    private readonly Func<long> millisecondsSinceStart;

    /// <summary>The transcript's line for a private announcement of <paramref name="characters"/> characters.</summary>
    public static string PrivateLine(int characters) =>
        string.Create(CultureInfo.InvariantCulture, $"[private, {characters} characters]");

    /// <summary>The mark before an announcement's text: <c>interrupting: </c> for one that cuts off the speech before
    /// it, <c>queued: </c> for one that waits its turn (#824).</summary>
    public static string Mode(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        return announcement.Interrupt ? "interrupting: " : "queued: ";
    }

    /// <summary>Starts a fresh transcript in <paramref name="folder"/>: the glossary's transcript covers one run. Each
    /// line starts with <c>&lt;ms&gt; ms </c> from <paramref name="millisecondsSinceStart"/>, the report log's clock (#763).</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A transcript failure must never reach the game.")]
    public TranscriptAnnouncer(IAnnouncer speech, string folder, Func<long> millisecondsSinceStart)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(millisecondsSinceStart);
        this.speech = speech;
        this.millisecondsSinceStart = millisecondsSinceStart;
        path = Path.Combine(folder, FileName);
        try
        {
            File.WriteAllText(path, "");
        }
        catch (Exception)
        {
            // Later writes fail the same way, and are skipped the same way.
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A transcript failure must never reach the game.")]
    public void Announce(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        try
        {
            // One line per announcement, so line breaks inside one become spaces. A private one (#585) is spoken
            // but only its length is written. Each says whether it cut off the speech before it or waited (#824).
            var line = announcement.Private
                ? PrivateLine(announcement.Text.Length)
                : announcement.Text.ReplaceLineEndings(" ");
            var stamp = millisecondsSinceStart().ToString(CultureInfo.InvariantCulture);
            File.AppendAllText(path, $"{stamp} ms {Mode(announcement)}{line}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // The transcript is for checking; the player still hears the announcement.
        }
        speech.Announce(announcement);
    }
}
