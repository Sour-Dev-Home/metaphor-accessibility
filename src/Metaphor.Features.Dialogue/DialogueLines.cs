using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the line the game asks for by label (#56), and the line it puts in its message window by any other
/// route (#526), looked up in the messages the game has compiled (<see cref="MessageLibrary"/>). A line asked for by
/// label is shown right after, in the same call, so it is spoken once; so is a choice's prompt, which
/// <see cref="DialogueChoices"/> speaks as the choice opens and the window shows next (#541,
/// docs/findings/reply-choice-routes-static.md).</summary>
[FeatureSetting<DialogueFeature>(DialogueFeature.Narration)]
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

    // Kind 2 is read only while this says so (the narration setting, #908); off, it stays silent as before.
    private readonly Func<bool> narrationOn;

    /// <summary>The game-over lines are kind 2 too; <see cref="GameOverScreen"/> speaks them with the screen's words,
    /// so they are left to it.</summary>
    private const string GameOverPrefix = "MES_GAMEOVER_";

    public DialogueLines(
        IMessageLookup messages, DialogueAnnouncer dialogue, TimeProvider? time = null, Func<bool>? narrationOn = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(dialogue);
        this.messages = messages;
        this.dialogue = dialogue;
        this.time = time ?? TimeProvider.System;
        this.narrationOn = narrationOn ?? (() => false);
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
    /// was spoken then, or the game's immediate repeat of the line just shown (#740); says nothing when there is none.
    /// Kind 0, as for requests, and kind 1, the system messages: the save screen's "Save complete" pop-up is one, shown
    /// with no request (#622, docs/raw-sources/save-complete-popup-session-2026-11-22.md). Kind 2, the day count and
    /// the arrival narration (#908, docs/findings/chapter1-silent-text-audit-logged.md), is spoken with no speaker, queued
    /// behind what is being said, when the narration setting is on; the game-over lines in it are left to
    /// <see cref="GameOverScreen"/>.</summary>
    public void Observe(LineShown line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Kind is not (0 or 1 or 2) || (line.Kind == 2 && !narrationOn())
            || messages.FindByLabelHash(line.LabelHash) is not { } message
            || (line.Kind == 2 && message.Label.StartsWith(GameOverPrefix, StringComparison.Ordinal)))
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
        if (line.Kind == 2)
        {
            dialogue.Show(null, message.Text, interrupt: false);
            return;
        }
        dialogue.Show(message.Speaker, message.Text);
    }
}
