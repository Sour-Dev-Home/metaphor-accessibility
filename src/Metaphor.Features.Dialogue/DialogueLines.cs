using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the line the game asks for by label (#56), and the line it puts in its message window by any other
/// route (#526), looked up in the messages the game has compiled (<see cref="MessageLibrary"/>). A line asked for by
/// label is shown right after, in the same call, so it is spoken once.</summary>
public sealed class DialogueLines : ILineRequestObserver, ILineShownObserver
{
    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;
    private readonly Lock gate = new();
    // The label just spoken as requested, until the window shows it.
    private string? requested;

    public DialogueLines(IMessageLookup messages, DialogueAnnouncer dialogue)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        this.messages = messages;
        this.dialogue = dialogue;
    }

    /// <summary>Speaks the message with the line's label; says nothing when there is none.</summary>
    public void Observe(LineRequested line)
    {
        ArgumentNullException.ThrowIfNull(line);
        // Only kind 0, the kind a spoken event line was seen with. Kinds 1 and 2 come from the system message,
        // subtitle and narration wrappers (docs/raw-sources/msg-request-functions-2026-11-16.md); what they show is
        // unverified.
        if (line.Kind == 0 && messages.Find(line.Label) is { } message)
        {
            lock (gate)
            {
                requested = message.Label;
            }
            dialogue.Show(message.Speaker, message.Text);
        }
    }

    /// <summary>Speaks the message whose label hashes to the shown line's, unless it is the line just requested, which
    /// was spoken then; says nothing when there is none. Kind 0 only, as for requests.</summary>
    public void Observe(LineShown line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Kind != 0 || messages.FindByLabelHash(line.LabelHash) is not { } message)
        {
            return;
        }
        lock (gate)
        {
            var already = requested == message.Label;
            requested = null;
            if (already)
            {
                return;
            }
        }
        dialogue.Show(message.Speaker, message.Text);
    }
}
