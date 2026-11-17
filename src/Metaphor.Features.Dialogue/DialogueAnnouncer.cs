using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Spoken story dialogue (#52): each line shown in the dialogue box is announced as "Name: line", or the line
/// alone when there's no speaker, interrupting the line before. A voiced line follows the "Voiced lines" setting
/// (#57, #104); the history (#126) keeps every line in full whatever is said.</summary>
public sealed class DialogueAnnouncer
{
    // A message is voiced when it carries a VOICE tag (docs/raw-sources/spike-session-1-2026-11-16.md).
    private const string VoiceTag = "<VOICE";

    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly DialogueHistory history;
    private readonly Func<VoicedLinesMode> voicedLines;
    private readonly Func<VoiceLanguage> voiceLanguage;

    /// <param name="announcer">Where lines are said.</param>
    /// <param name="cleaner">Turns the game's text into what is said.</param>
    /// <param name="history">Where every line is recorded in full, spoken or not.</param>
    /// <param name="voicedLines">The "Voiced lines" mode now, read on every line so a change applies at once; the full
    /// line when omitted.</param>
    /// <param name="voiceLanguage">The voice language now, for Auto; unknown when omitted.</param>
    public DialogueAnnouncer(
        IAnnouncer announcer,
        TextCleaner cleaner,
        DialogueHistory history,
        Func<VoicedLinesMode>? voicedLines = null,
        Func<VoiceLanguage>? voiceLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(history);
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.history = history;
        this.voicedLines = voicedLines ?? (() => VoicedLinesMode.FullLine);
        this.voiceLanguage = voiceLanguage ?? (() => VoiceLanguage.Unknown);
    }

    /// <summary>Announces a line as the game stores it, with its speaker as the game stores it (null when none). A line
    /// that cleans to nothing, such as one made only of tags, has nothing to say and isn't announced; a speaker that
    /// cleans to nothing is left out. An unvoiced line is always said in full.</summary>
    public void Show(string? speaker, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = cleaner.Clean(line);
        if (text.Length == 0)
        {
            return;
        }
        var name = cleaner.Clean(speaker ?? "");
        var full = name.Length == 0 ? text : $"{name}: {text}";
        // Recorded before the mode is applied, so the history holds the full line even if it goes unspoken (#126).
        history.Add(full);
        var mode = line.Contains(VoiceTag, StringComparison.Ordinal) ? Effective(voicedLines()) : VoicedLinesMode.FullLine;
        switch (mode)
        {
            case VoicedLinesMode.Nothing:
                return;
            case VoicedLinesMode.NameOnly:
                if (name.Length > 0)
                {
                    announcer.Announce(new Announcement(name, Interrupt: true));
                }
                return;
            default:
                announcer.Announce(new Announcement(full, Interrupt: true));
                return;
        }
    }

    /// <summary>Announces a choice's options (#82) from its message as the game stores it, one option per line, as
    /// "Choose: option; option", without cutting off the line before. A missing message (null), or one whose lines all
    /// clean to nothing, is announced as "Choice".</summary>
    public void Offer(string? options)
    {
        var lines = (options ?? "").Split('\n').Select(cleaner.Clean).Where(line => line.Length > 0).ToList();
        announcer.Announce(new Announcement(lines.Count == 0 ? "Choice" : $"Choose: {string.Join("; ", lines)}", Interrupt: false));
    }

    // Auto: the name for English voice; the full line for Japanese voice and while the language is unknown.
    private VoicedLinesMode Effective(VoicedLinesMode mode) =>
        mode != VoicedLinesMode.Auto ? mode
        : voiceLanguage() == VoiceLanguage.English ? VoicedLinesMode.NameOnly
        : VoicedLinesMode.FullLine;
}
