using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
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
    // maintainer on 2026-11-22, #623). Confirm is A on a pad or Space on the keyboard, and the words name the key of
    // the device last used (maintainer's decision, 2026-11-22, Q29 on #623).

    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;
    private readonly IAnnouncer announcer;
    private readonly LastInputDevice device;
    private readonly Lock gate = new();
    private bool shown;

    /// <param name="messages">The compiled messages, to find the shown line.</param>
    /// <param name="dialogue">Where the voiced line is said, as other dialogue is.</param>
    /// <param name="announcer">Where the screen's words are said.</param>
    /// <param name="device">The last-used input device, to name the confirm key.</param>
    public GameOverScreen(
        IMessageLookup messages, DialogueAnnouncer dialogue, IAnnouncer announcer, LastInputDevice device)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(device);
        this.device = device;
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
        var key = device.Current == InputDevice.Keyboard ? "Space" : "A";
        announcer.Announce(new Announcement($"Fantasy is Dead. Press {key} to continue.", Interrupt: true));
    }
}
