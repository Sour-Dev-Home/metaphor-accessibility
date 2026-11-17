using System.Globalization;

namespace Metaphor.Core.Messages;

/// <summary>A sound cue set within the window after a line: its number, the player handle it was set on, that player's
/// CRI category when one was seen (null otherwise), and how long after the line's request it came.</summary>
public sealed record VoiceCueHeard(int CueId, long Player, uint? Category, long AfterMilliseconds)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{CueId}@{AfterMilliseconds}ms/player=0x{Player:x}/cat={(Category is { } c ? c.ToString(CultureInfo.InvariantCulture) : "?")}");
}

/// <summary>One line's check (#103): the request's kind and label, whether its message carries a <c>&lt;VOICE</c> tag
/// (null when the message wasn't found), and the cues set within the window after it. Labels and numbers only.
/// </summary>
public sealed record VoiceCueChecked(int Kind, string Label, bool? Tagged, IReadOnlyList<VoiceCueHeard> Cues)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"kind={Kind} label=\"{LogText.Escaped(Label)}\" tagged={Tagged switch { true => "yes", false => "no", null => "unknown" }} cues={(Cues.Count == 0 ? "none" : string.Join(",", Cues))}");
}

/// <summary>Issue #103's check of whether each line the game requests starts a voice cue: it notes whether the line's
/// message is <c>&lt;VOICE&gt;</c>-tagged and collects the cues set within <c>windowMilliseconds</c> after the
/// request, with each cue's player category, so a run tells which category voices use and whether any tagged line
/// starts none. Session 1 saw each voiced line's cue within 1 ms after its request
/// (docs/raw-sources/spike-session-1-2026-11-16.md). A line's check is reported when the next line is requested, when
/// a cue comes after its window, or on <see cref="Flush"/>. Development only. Safe to use from several threads.</summary>
public sealed class VoiceCueCheck : ILineRequestObserver
{
    /// <summary>The most cues one line's check keeps.</summary>
    public const int MaxCues = 8;

    // The same tag the dialogue feature treats as voiced (docs/raw-sources/spike-session-1-2026-11-16.md).
    private const string VoiceTag = "<VOICE";

    private readonly IMessageLookup messages;
    private readonly Func<long> clock;
    private readonly Action<VoiceCueChecked> report;
    private readonly long window;
    private readonly Lock gate = new();
    private readonly Dictionary<long, uint> categories = [];
    private Pending? pending;

    public VoiceCueCheck(IMessageLookup messages, Func<long> clock, Action<VoiceCueChecked> report,
        long windowMilliseconds = 1000)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(report);
        this.messages = messages;
        this.clock = clock;
        this.report = report;
        window = windowMilliseconds;
    }

    /// <summary>Starts a check for <paramref name="line"/>, reporting the one before.</summary>
    public void Observe(LineRequested line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var tagged = messages.Find(line.Label) is { } message ? message.Text.Contains(VoiceTag, StringComparison.Ordinal) : (bool?)null;
        VoiceCueChecked? done;
        lock (gate)
        {
            done = Close();
            pending = new Pending(line.Kind, line.Label, tagged, clock());
        }
        Report(done);
    }

    /// <summary>A cue set on <paramref name="player"/>: added to the waiting line's check while in its window; past
    /// the window it closes that check instead.</summary>
    public void CueSet(int cueId, long player)
    {
        VoiceCueChecked? done = null;
        lock (gate)
        {
            if (pending is not { } line)
            {
                return;
            }
            var after = clock() - line.At;
            if (after > window)
            {
                done = Close();
            }
            else if (line.Cues.Count < MaxCues)
            {
                line.Cues.Add(new VoiceCueHeard(cueId, player, categories.TryGetValue(player, out var c) ? c : null, after));
            }
        }
        Report(done);
    }

    /// <summary>The CRI category the game gave <paramref name="player"/>; the newest one counts.</summary>
    public void PlayerCategory(long player, uint category)
    {
        lock (gate)
        {
            categories[player] = category;
        }
    }

    /// <summary>Reports the waiting line's check, if any.</summary>
    public void Flush()
    {
        VoiceCueChecked? done;
        lock (gate)
        {
            done = Close();
        }
        Report(done);
    }

    private VoiceCueChecked? Close()
    {
        var line = pending;
        pending = null;
        return line is null ? null : new VoiceCueChecked(line.Kind, line.Label, line.Tagged, line.Cues);
    }

    private void Report(VoiceCueChecked? done)
    {
        if (done is not null)
        {
            report(done);
        }
    }

    private sealed record Pending(int Kind, string Label, bool? Tagged, long At)
    {
        public List<VoiceCueHeard> Cues { get; } = [];
    }
}
