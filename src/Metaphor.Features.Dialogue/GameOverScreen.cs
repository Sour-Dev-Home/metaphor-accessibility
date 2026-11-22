using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>The game-over screen (#623). Its voiced line is a subtitle (kind 2) the window shows with no request, from
/// the <c>MES_GAMEOVER_</c> labels; the window closes after it and the screen waits for confirm, then the game reloads
/// the last save by itself (docs/raw-sources/game-over-screen-session-2026-11-22.md). The line is spoken as the window
/// shows it, as #57 decides for its clip, and once the window closes the screen's words and how to go on.</summary>
public sealed class GameOverScreen : ILineShownObserver
{
    private const string LabelPrefix = "MES_GAMEOVER_";

    // The words are ours, English only: "Fantasy is Dead" is drawn, and no compiled message holds it (heard by the
    // maintainer on 2026-11-22, #623). Confirm is A on a pad or Space on the keyboard.
    private const string OwnWords = "Fantasy is Dead. Press A to continue.";

    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;
    private readonly IAnnouncer announcer;
    private readonly Lock gate = new();
    private bool shown;

    /// <param name="messages">The compiled messages, to find the shown line.</param>
    /// <param name="dialogue">Where the voiced line is said, as other dialogue is.</param>
    /// <param name="announcer">Where the screen's words are said.</param>
    public GameOverScreen(IMessageLookup messages, DialogueAnnouncer dialogue, IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(announcer);
        this.messages = messages;
        this.dialogue = dialogue;
        this.announcer = announcer;
    }

    /// <summary>Speaks a game-over line the window shows, and notes the screen is up; any other line is left to
    /// <see cref="DialogueLines"/>.</summary>
    public void Observe(LineShown line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Kind != 2 || messages.FindByLabelHash(line.LabelHash) is not { } message
            || !message.Label.StartsWith(LabelPrefix, StringComparison.Ordinal))
        {
            return;
        }
        lock (gate)
        {
            shown = true;
        }
        dialogue.Show(message.Speaker, message.Text);
    }

    /// <summary>The dialogue window closed: after a game-over line, says the screen's words once.</summary>
    public void WindowClosed()
    {
        lock (gate)
        {
            if (!shown)
            {
                return;
            }
            shown = false;
        }
        announcer.Announce(new Announcement(OwnWords, Interrupt: true));
    }
}
