using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field;

/// <summary>The current main mission, as the field's mission box titles it (#658). On game build 548ddc95… (Steam build
/// 18330018) the box takes the row of the highest set story flag in the main mission table
/// (<see cref="MainMissionFlags.Current"/>), and each row holds the MD5 of a message label
/// (docs/findings/mission-title-from-the-mission-table.md). The title is that message's text, from the files the game
/// compiled, so it is the game's own words. [NEEDS VERIFICATION] in memory: that the label is the box's title. A new
/// title is spoken once, in free roam, looked for at most once a second; the first one seen is not, since it is the
/// mission the game was loaded on. Safe to call from several threads.</summary>
public sealed class MissionLine
{
    /// <summary>How often <see cref="Poll"/> reads the game, at most.</summary>
    public const long PollMilliseconds = 1000;

    private readonly Func<int, bool?> storyFlag;
    private readonly Func<IReadOnlyList<MainMissionRow>?> rows;
    private readonly IMessageLookup messages;
    private readonly TextCleaner cleaner;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Func<bool> roaming;
    private readonly Action<string>? log;
    private readonly Lock gate = new();
    private long? lastPoll;
    private MainMissionRow? seen;

    /// <summary>A mission line that reads the flags through <paramref name="storyFlag"/> and the main mission table
    /// through <paramref name="rows"/> (null when it can't be read), finds titles in <paramref name="messages"/> and
    /// cleans them with <paramref name="cleaner"/>, speaks through <paramref name="announcer"/>, keeps time by
    /// <paramref name="milliseconds"/>, speaks only while <paramref name="roaming"/> says the player roams the field,
    /// and writes each change's flag and label to <paramref name="log"/> when there is one (Debug builds).</summary>
    public MissionLine(
        Func<int, bool?> storyFlag, Func<IReadOnlyList<MainMissionRow>?> rows, IMessageLookup messages,
        TextCleaner cleaner, IAnnouncer announcer, Func<long> milliseconds, Func<bool> roaming,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(storyFlag);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(roaming);
        this.storyFlag = storyFlag;
        this.rows = rows;
        this.messages = messages;
        this.cleaner = cleaner;
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.roaming = roaming;
        this.log = log;
    }

    /// <summary>The current main mission's title, read now; null when it can't be read or has no known text.</summary>
    public string? Current => Read() is { } row ? Title(row) : null;

    /// <summary>Looks for a new main mission, at most once every <see cref="PollMilliseconds"/> and only in free roam,
    /// and speaks it. Returns what was said, or null. A read that throws says nothing, so it never reaches the game.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    public string? Poll()
    {
        try
        {
            return Look();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>How a mission is said: after the word the HUD's box is headed with.</summary>
    public static string Spoken(string mission) => $"Mission: {mission}";

    private string? Look()
    {
        var now = milliseconds();
        lock (gate)
        {
            if (lastPoll is { } last && now - last < PollMilliseconds)
            {
                return null;
            }
            lastPoll = now;
        }
        if (!roaming() || Read() is not { } row)
        {
            return null;
        }
        bool first;
        lock (gate)
        {
            if (row == seen)
            {
                return null;
            }
            first = seen is null;
            seen = row;
        }
        var message = messages.FindByLabelHash(row.LabelHash);
        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"mission flag=0x{row.Flag:x} label={(message is null ? "(unknown) hash=" + row.LabelHash : message.Label)} changed"));
        if (first || message is null || cleaner.Clean(message.Text) is not { Length: > 0 } title)
        {
            return null;
        }
        var said = Spoken(title);
        announcer.Announce(new Announcement(said, Interrupt: false));
        return said;
    }

    private MainMissionRow? Read() => rows() is { } table ? MainMissionFlags.Current(storyFlag, table) : null;

    private string? Title(MainMissionRow row) =>
        messages.FindByLabelHash(row.LabelHash) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } title
            ? title
            : null;
}
