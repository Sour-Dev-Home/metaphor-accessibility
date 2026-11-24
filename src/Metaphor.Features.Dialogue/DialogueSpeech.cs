using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>The dialogue's lines and choices, built on one lookup that names each speaker as the screen shows it
/// (<see cref="ShownSpeakers"/>, #81), so a choice's prompt and a line never name the same speaker differently.</summary>
public sealed record DialogueSpeech(DialogueLines Lines, DialogueChoices Choices)
{
    public static DialogueSpeech Shown(
        IMessageLookup messages, GameNames names, DialogueAnnouncer dialogue, Func<bool>? narrationOn = null)
    {
        var shown = new ShownSpeakers(messages, names);
        return new(new DialogueLines(shown, dialogue, narrationOn: narrationOn), new DialogueChoices(shown, dialogue));
    }
}
