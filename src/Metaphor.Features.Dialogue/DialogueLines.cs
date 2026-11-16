using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the line the game asks for by label (#56), looked up in the messages the game has compiled
/// (<see cref="MessageLibrary"/>).</summary>
public sealed class DialogueLines : ILineRequestObserver
{
    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;

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
            dialogue.Show(message.Speaker, message.Text);
        }
    }
}
