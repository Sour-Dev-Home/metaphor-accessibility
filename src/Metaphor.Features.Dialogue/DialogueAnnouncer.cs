using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Spoken story dialogue (#52): each line shown in the dialogue box is announced as "Name: line" on a
/// conversation's first line, when the speaker changes and on the first line after a choice, and as the line alone
/// while the same speaker continues or when there's no speaker (#539, #740), interrupting the line before. A voiced line follows the "Voiced lines" setting
/// (#57, #104); the history (#126) keeps every line in full whatever is said.</summary>
[FeatureSetting<DialogueFeature>(DialogueFeature.Dialogue, DialogueFeature.ChoiceCount, DialogueFeature.ChoiceDelay)]
public sealed class DialogueAnnouncer
{
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<bool> choiceCountOn;
    private readonly Func<bool> choiceDelayOn;
    private readonly DialogueHistory history;
    private readonly Func<VoicedLinesMode> voicedLines;
    private readonly Func<VoiceLanguage> voiceLanguage;
    private readonly ShownLine? shown;
    private readonly Lock gate = new();
    private readonly TimeProvider time;
    private bool started;
    private string? lastSpeaker;
    // A choice opened since the last line, so the next line says its speaker even if unchanged (#740).
    private bool afterChoice;
    // A choice's options waiting for its window to open, and the timer that says them if it never does (#744).
    private string? heldOptions;
    private bool holding;
    private ITimer? fallback;
    private int holdId;

    /// <summary>How long held options wait for their window to open before they are said anyway.</summary>
    public static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(8);

    /// <param name="announcer">Where lines are said.</param>
    /// <param name="cleaner">Turns the game's text into what is said.</param>
    /// <param name="history">Where every line is recorded in full, spoken or not.</param>
    /// <param name="voicedLines">The "Voiced lines" mode now, read on every line so a change applies at once; the full
    /// line when omitted.</param>
    /// <param name="voiceLanguage">The voice language now, for Auto; unknown when omitted.</param>
    /// <param name="choiceCountOn">Whether a choice says its count (<see cref="DialogueFeature.ChoiceCount"/>) rather than
    /// every option; on when omitted.</param>
    /// <param name="timeProvider">Times <see cref="HoldLimit"/>; the system clock when
    /// omitted.</param>
    /// <param name="choiceDelayOn">Whether a choice's options wait for the select window
    /// (<see cref="DialogueFeature.ChoiceDelay"/>) rather than follow the prompt at once; on when omitted.</param>
    /// <param name="shown">Where each line is recorded as the one on screen, spoken or not (#879); none when
    /// omitted.</param>
    public DialogueAnnouncer(
        IAnnouncer announcer,
        TextCleaner cleaner,
        DialogueHistory history,
        Func<VoicedLinesMode>? voicedLines = null,
        Func<VoiceLanguage>? voiceLanguage = null,
        Func<bool>? choiceCountOn = null,
        TimeProvider? timeProvider = null,
        Func<bool>? choiceDelayOn = null,
        ShownLine? shown = null)
    {
        this.shown = shown;
        this.choiceDelayOn = choiceDelayOn ?? (() => true);
        time = timeProvider ?? TimeProvider.System;
        this.choiceCountOn = choiceCountOn ?? (() => true);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(history);
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.history = history;
        this.voicedLines = voicedLines ?? (() => VoicedLinesMode.FullLine);
        this.voiceLanguage = voiceLanguage ?? (() => VoiceLanguage.Unknown);
    }

    /// <summary>Announces a line as the game stores it, with its speaker as the game stores it (null when none). A line
    /// that cleans to nothing, such as one made only of tags, has nothing to say and isn't announced; a speaker that
    /// cleans to nothing is left out. An unvoiced line is always said in full.</summary>
    public void Show(string? speaker, string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = cleaner.Clean(line);
        if (text.Length == 0)
        {
            return;
        }
        var name = cleaner.Clean(speaker ?? "");
        var full = name.Length == 0 ? text : $"{name}: {text}";
        // Recorded before the mode is applied, so the history holds the full line even if it goes unspoken (#126).
        // The history keeps the name on every line, so the replay key always says name and line (#539).
        history.Add(full);
        shown?.Record(name, text);
        // The name is said on a conversation's first line and when the speaker changes (#539), not before every window
        // a speaker goes on across (#873). Idea only, from Arcana (https://github.com/ChipsAhoiMcCoy/arcana,
        // ChipsAhoiMcCoy, Zlib; v0.6.0 release notes, tag commit 73db802); no code copied (CREDITS.md).
        bool changed;
        lock (gate)
        {
            changed = !started || afterChoice || !string.Equals(lastSpeaker, name, StringComparison.Ordinal);
            started = true;
            afterChoice = false;
            lastSpeaker = name;
        }
        var said = changed ? full : text;
        var mode = VoiceClips.ReadsTheWholeLine(line) ? Effective(voicedLines()) : VoicedLinesMode.FullLine;
        switch (mode)
        {
            case VoicedLinesMode.Nothing:
                return;
            case VoicedLinesMode.NameOnly:
                // A voiced line whose speaker hasn't changed says nothing.
                if (changed && name.Length > 0)
                {
                    announcer.Announce(new Announcement(name, Interrupt: true));
                }
                return;
            default:
                announcer.Announce(new Announcement(said, Interrupt: true));
                return;
        }
    }

    /// <summary>Ends the conversation, so the next line counts as a first and says its speaker (#539), and drops options
    /// still waiting to be said (#744). Called when the dialogue window closes.</summary>
    public void EndConversation()
    {
        lock (gate)
        {
            started = false;
            lastSpeaker = null;
            afterChoice = false;
            CancelHeld();
        }
    }

    /// <summary>Holds a choice's options until they are on screen (<see cref="Release()"/>), because they appear some
    /// seconds after the line before them (about 4 s in session c, #744), so saying them at once, or after a fixed
    /// delay, is over the line. If the select window is never called they are said after <see cref="HoldLimit"/>.
    /// Options already held are replaced; <see cref="EndConversation"/> drops them.</summary>
    public void Hold(string? options)
    {
        // The hold is its own setting while it is tested (#783); off, the options are said at once as before.
        if (!choiceDelayOn())
        {
            Offer(options);
            return;
        }
        lock (gate)
        {
            CancelHeld();
            heldOptions = options;
            holding = true;
            var id = holdId;
            fallback = time.CreateTimer(_ => Release(id), null, HoldLimit, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Says the held options, if any: they are on screen, and the focus is about to say the first of them.
    /// </summary>
    public void Release() => Release(null);

    /// <summary>The choice's select window was called. On a voiced choice that comes with the prompt, and the options
    /// show only when the voice ends, 8.7 s later at new game's first choice
    /// (docs/raw-sources/dialogue-by-ear-session-2026-11-23.md, #814). So held options that have a first option for the
    /// focus to say keep waiting for it (<see cref="Release()"/>), now with no time limit; held options with none
    /// ("Choice") are said now.</summary>
    public void WindowOpened()
    {
        string? options;
        int id;
        lock (gate)
        {
            if (!holding)
            {
                return;
            }
            options = heldOptions;
            id = holdId;
        }
        // Cleaned outside the lock: the cleaner may read names from the game.
        var focusFollows = ChoiceOptions.From(options, cleaner).Count > 0;
        lock (gate)
        {
            if (!holding || id != holdId)
            {
                return;
            }
            if (focusFollows)
            {
                fallback?.Dispose();
                fallback = null;
                return;
            }
        }
        Release(id);
    }

    private void Release(int? id)
    {
        string? options;
        lock (gate)
        {
            if (!holding || (id is { } expected && expected != holdId))
            {
                return;
            }
            options = heldOptions;
            CancelHeld();
        }
        // May run on a timer thread, where an exception nobody catches ends the process; with an id, from the timer or
        // the window call, the options are dropped as a timer's would be.
        try
        {
            Offer(options);
        }
        catch (Exception) when (id is not null)
        {
            // Nothing more to do: the options are lost, as when any announcement fails.
        }
    }

    // Called with the gate held.
    private void CancelHeld()
    {
        holdId++;
        holding = false;
        heldOptions = null;
        fallback?.Dispose();
        fallback = null;
    }

    /// <summary>Announces a choice's options (#82) from its message as the game stores it, one option per line, as
    /// "Choice, N options" (the options themselves are spoken as focus moves, ChoiceFocus), without cutting off the line
    /// before. A missing message (null), or one whose lines all
    /// clean to nothing, is announced as "Choice". An option of only dots or ellipses (which a screen reader reads as
    /// nothing) is spoken as "dot dot dot".</summary>
    public void Offer(string? options)
    {
        lock (gate)
        {
            afterChoice = true;
        }
        var lines = ChoiceOptions.From(options, cleaner);
        // The count is its own setting while it is tested (#783); off, every option is read in the one line as before.
        var said = lines.Count switch
        {
            0 => "Choice",
            _ when !choiceCountOn() => $"Choose: {string.Join("; ", lines)}",
            1 => "Choice, 1 option",
            _ => $"Choice, {lines.Count} options",
        };
        announcer.Announce(new Announcement(said, Interrupt: false));
    }

    // Auto: the name for English voice; the full line for Japanese voice and while the language is unknown.
    private VoicedLinesMode Effective(VoicedLinesMode mode) =>
        mode != VoicedLinesMode.Auto ? mode
        : voiceLanguage() == VoiceLanguage.English ? VoicedLinesMode.NameOnly
        : VoicedLinesMode.FullLine;
}
