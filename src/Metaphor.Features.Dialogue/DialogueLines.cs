using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the line the game asks for by label (#56), and the line it puts in its message window by any other
/// route (#526), looked up in the messages the game has compiled (<see cref="MessageLibrary"/>). A line asked for by
/// label is shown right after, in the same call, so it is spoken once; so is a choice's prompt, which
/// <see cref="DialogueChoices"/> speaks as the choice opens and the window shows next (#541,
/// docs/findings/reply-choice-routes-static.md).</summary>
public sealed class DialogueLines : ILineRequestObserver, ILineShownObserver, IChoiceRequestObserver
{
    private readonly IMessageLookup messages;
    private readonly DialogueAnnouncer dialogue;
    private readonly Lock gate = new();
    // The label just spoken as requested or as a choice's prompt, until the window shows it.
    private string? requested;

    private readonly TimeProvider time;
    // The label of the line last shown and when, to drop the game's immediate repeat of it (#740).
    private string? lastShown;
    private long lastShownAt;

    /// <summary>A shown line identical to the previous one within this window is the game's own repeat (#740: seen
    /// 37 to 100 ms apart), not a second reading.</summary>
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromMilliseconds(300);

    public DialogueLines(IMessageLookup messages, DialogueAnnouncer dialogue, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        this.messages = messages;
        this.dialogue = dialogue;
        this.time = time ?? TimeProvider.System;
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

    /// <summary>Notes the prompt of a choice that opened, when it has a message, as spoken.</summary>
    public void Observe(ChoiceRequested choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (choice.PromptLabel is { } label && messages.Find(label) is { } message)
        {
            lock (gate)
            {
                requested = message.Label;
            }
        }
    }

    /// <summary>Speaks the message whose label hashes to the shown line's, unless it is the line just requested, which
    /// was spoken then, or the game's immediate repeat of the line just shown (#740); says nothing when there is none. Kind 0 only, as for requests.</summary>
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
            var now = time.GetTimestamp();
            var repeat = lastShown == message.Label && time.GetElapsedTime(lastShownAt, now) < RepeatWindow;
            lastShown = message.Label;
            lastShownAt = now;
            if (already || repeat)
            {
                return;
            }
        }
        dialogue.Show(message.Speaker, message.Text);
    }
}
