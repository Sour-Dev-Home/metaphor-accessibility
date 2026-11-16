using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the line the game asks for by label (#56), looked up in the messages the game has compiled
/// (<see cref="MessageLibrary"/>).</summary>
public sealed class DialogueLines
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

    /// <summary>Speaks the message with this label; says nothing when there is none.</summary>
    public void Requested(string label)
    {
        if (messages.Find(label) is { } message)
        {
            dialogue.Show(message.Speaker, message.Text);
        }
    }
}
