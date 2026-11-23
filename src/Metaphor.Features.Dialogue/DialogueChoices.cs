using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks a choice the game opens (#82): its prompt as a dialogue line, then its options, both looked up by
/// label in the messages the game has compiled (<see cref="MessageLibrary"/>). The prompt never goes through the line
/// request (docs/raw-sources/spike-session-1-2026-11-16.md), so it is spoken here.</summary>
public sealed class DialogueChoices : IChoiceRequestObserver, ISelectRequestObserver
{
    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;

    public DialogueChoices(IMessageLookup messages, DialogueAnnouncer dialogue)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        this.messages = messages;
        this.dialogue = dialogue;
    }

    /// <summary>Speaks the prompt, when its label has a message, then the options, or "Choice" when the choice's
    /// label has none.</summary>
    public void Observe(ChoiceRequested choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (Find(choice.PromptLabel) is { } prompt)
        {
            dialogue.Show(prompt.Speaker, prompt.Text);
        }
        // Held until the options are on screen, which is some seconds after the prompt, not over it (#744).
        dialogue.Hold(Find(choice.ChoiceLabel)?.Text);
    }

    /// <summary>The select window was called: held options with no first option to wait for are said
    /// (<see cref="DialogueAnnouncer.WindowOpened"/>, #814). Register after <see cref="SelectWindowChoices"/>, which
    /// holds the options of a choice only this window reports.</summary>
    public void Observe(SelectRequested window)
    {
        ArgumentNullException.ThrowIfNull(window);
        dialogue.WindowOpened();
    }

    private GameMessage? Find(string? label) => label is null ? null : messages.Find(label);
}
