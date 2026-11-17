using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Spoken story dialogue (#52): each line shown in the dialogue box is announced as "Name: line", or the line
/// alone when there's no speaker, interrupting the line before.</summary>
public sealed class DialogueAnnouncer
{
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    private readonly DialogueHistory history;

    public DialogueAnnouncer(IAnnouncer announcer, TextCleaner cleaner, DialogueHistory history)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(history);
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.history = history;
    }

    /// <summary>Announces a line as the game stores it, with its speaker as the game stores it (null when none). A line
    /// that cleans to nothing, such as one made only of tags, has nothing to say and isn't announced; a speaker that
    /// cleans to nothing is left out.</summary>
    public void Show(string? speaker, string line)
    {
        var text = cleaner.Clean(line);
        if (text.Length == 0)
        {
            return;
        }
        var name = cleaner.Clean(speaker ?? "");
        var spoken = name.Length == 0 ? text : $"{name}: {text}";
        // Recorded before speaking, so the history holds the line even if a mode leaves it unspoken (#126).
        history.Add(spoken);
        announcer.Announce(new Announcement(spoken, Interrupt: true));
    }

    /// <summary>Announces a choice's options (#82) from its message as the game stores it, one option per line, as
    /// "Choose: option; option", without cutting off the line before. A missing message (null), or one whose lines all
    /// clean to nothing, is announced as "Choice". An option of only dots or ellipses (which a screen reader reads as
    /// nothing) is spoken as "dot dot dot".</summary>
    public void Offer(string? options)
    {
        var lines = (options ?? "").Split('\n').Select(cleaner.Clean).Where(line => line.Length > 0)
            .Select(line => line.All(c => c is '.' or '…' or ' ') ? "dot dot dot" : line).ToList();
        announcer.Announce(new Announcement(lines.Count == 0 ? "Choice" : $"Choose: {string.Join("; ", lines)}", Interrupt: false));
    }
}
