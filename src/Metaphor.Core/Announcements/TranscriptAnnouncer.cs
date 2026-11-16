using System.Diagnostics.CodeAnalysis;

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

    /// <summary>Starts a fresh transcript in <paramref name="folder"/>: the glossary's transcript covers one run.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A transcript failure must never reach the game.")]
    public TranscriptAnnouncer(IAnnouncer speech, string folder)
    {
        ArgumentNullException.ThrowIfNull(speech);
        this.speech = speech;
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
            // One line per announcement, so line breaks inside one become spaces.
            File.AppendAllText(path, announcement.Text.ReplaceLineEndings(" ") + Environment.NewLine);
        }
        catch (Exception)
        {
            // The transcript is for checking; the player still hears the announcement.
        }
        speech.Announce(announcement);
    }
}
