using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Spoken story dialogue (#52): each line shown in the dialogue box is announced as "Name: line" on a
/// conversation's first line and when the speaker changes, and as the line alone while the same speaker continues or
/// when there's no speaker (#539), interrupting the line before. A voiced line follows the "Voiced lines" setting
/// (#57, #104); the history (#126) keeps every line in full whatever is said.</summary>
[FeatureSetting(Feature.Dialogue)]
public sealed class DialogueAnnouncer
{
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly DialogueHistory history;
    private readonly Func<VoicedLinesMode> voicedLines;
    private readonly Func<VoiceLanguage> voiceLanguage;
    private readonly Lock gate = new();
    private readonly TimeProvider time;
    private bool started;
    private string? lastSpeaker;
    // When the last line was announced, and the choice's options waiting for it to be heard (#744).
    private long? lastLineAt;
    private ITimer? pending;
    private int pendingId;

    /// <summary>How long a choice's options wait after a line that started within <see cref="RecentLine"/>.</summary>
    public static readonly TimeSpan OptionsDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>A line that started this recently is probably still being said.</summary>
    public static readonly TimeSpan RecentLine = TimeSpan.FromSeconds(1);

    /// <param name="announcer">Where lines are said.</param>
    /// <param name="cleaner">Turns the game's text into what is said.</param>
    /// <param name="history">Where every line is recorded in full, spoken or not.</param>
    /// <param name="voicedLines">The "Voiced lines" mode now, read on every line so a change applies at once; the full
    /// line when omitted.</param>
    /// <param name="voiceLanguage">The voice language now, for Auto; unknown when omitted.</param>
    /// <param name="timeProvider">Times <see cref="OptionsDelay"/> and <see cref="RecentLine"/>; the system clock when
    /// omitted.</param>
    public DialogueAnnouncer(
        IAnnouncer announcer,
        TextCleaner cleaner,
        DialogueHistory history,
        Func<VoicedLinesMode>? voicedLines = null,
        Func<VoiceLanguage>? voiceLanguage = null,
        TimeProvider? timeProvider = null)
    {
        time = timeProvider ?? TimeProvider.System;
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
        // The name is said on a conversation's first line and when the speaker changes (#539).
        bool changed;
        lock (gate)
        {
            changed = !started || !string.Equals(lastSpeaker, name, StringComparison.Ordinal);
            started = true;
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
                    NoteLine();
                    announcer.Announce(new Announcement(name, Interrupt: true));
                }
                return;
            default:
                NoteLine();
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
            CancelPending();
        }
    }

    /// <summary>Announces a choice's options as <see cref="Offer"/> does, but after <see cref="OptionsDelay"/> when a
    /// line started within <see cref="RecentLine"/>, so they don't come over the line being said (#744); at once
    /// otherwise. Options still waiting are replaced, and dropped if the conversation ends first.</summary>
    public void OfferAfterLine(string? options)
    {
        lock (gate)
        {
            CancelPending();
            if (lastLineAt is { } at && time.GetElapsedTime(at) < RecentLine)
            {
                var id = pendingId;
                pending = time.CreateTimer(_ => OfferIfCurrent(id, options), null, OptionsDelay, Timeout.InfiniteTimeSpan);
                return;
            }
        }
        Offer(options);
    }

    private void OfferIfCurrent(int id, string? options)
    {
        lock (gate)
        {
            if (id != pendingId)
            {
                return;
            }
            pending?.Dispose();
            pending = null;
        }
        // On a timer thread, where an exception nobody catches ends the process.
        try
        {
            Offer(options);
        }
        catch (Exception)
        {
            // Nothing more to do: the options are lost, as when any announcement fails.
        }
    }

    private void CancelPending()
    {
        pendingId++;
        pending?.Dispose();
        pending = null;
    }

    private void NoteLine()
    {
        lock (gate)
        {
            lastLineAt = time.GetTimestamp();
        }
    }

    /// <summary>Announces a choice's options (#82) from its message as the game stores it, one option per line, as
    /// "Choose: option; option", without cutting off the line before. A missing message (null), or one whose lines all
    /// clean to nothing, is announced as "Choice". An option of only dots or ellipses (which a screen reader reads as
    /// nothing) is spoken as "dot dot dot".</summary>
    public void Offer(string? options)
    {
        var lines = ChoiceOptions.From(options, cleaner);
        announcer.Announce(new Announcement(lines.Count == 0 ? "Choice" : $"Choose: {string.Join("; ", lines)}", Interrupt: false));
    }

    // Auto: the name for English voice; the full line for Japanese voice and while the language is unknown.
    private VoicedLinesMode Effective(VoicedLinesMode mode) =>
        mode != VoicedLinesMode.Auto ? mode
        : voiceLanguage() == VoiceLanguage.English ? VoicedLinesMode.NameOnly
        : VoicedLinesMode.FullLine;
}
