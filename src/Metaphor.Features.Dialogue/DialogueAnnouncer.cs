using Metaphor.Core.Announcements;

namespace Metaphor.Features.Dialogue;

/// <summary>Spoken story dialogue (#52): each line shown in the dialogue box is announced as "Name: line", or the line
/// alone when there's no speaker, interrupting the line before.</summary>
public sealed class DialogueAnnouncer
{
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public DialogueAnnouncer(IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.announcer = announcer;
        this.cleaner = cleaner;
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
        announcer.Announce(new Announcement(name.Length == 0 ? text : $"{name}: {text}", Interrupt: true));
    }
}
