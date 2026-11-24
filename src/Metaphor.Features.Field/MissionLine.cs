using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The current main mission, as the field's mission box titles it (#658). On game build 548ddc95… (Steam build
/// 18330018) the box takes the row of the highest set story flag in the main mission table
/// (<see cref="MainMissionFlags.Current"/>), and each row holds the MD5 of a message label
/// (docs/findings/mission-title-from-the-mission-table.md). The title is that message's text, from the files the game
/// compiled, so it is the game's own words. [NEEDS VERIFICATION] in memory: that the label is the box's title. The
/// objective under it (#743) is the row of the highest set flag in the field objective table, which starts each
/// objective with the same flags (docs/findings/field-map-and-objective-tables-static.md); [NEEDS VERIFICATION] that the
/// box picks it so. A title's trailing colon is dropped, and the objective follows as a sentence of its own. A new
/// mission, or a new objective under the same one, is spoken once, in free roam, looked for at most once a second; the first one seen is not, since it is the
/// mission the game was loaded on. It belongs to "where am I" (<see cref="FieldFeature.WhereAmI"/>): with that setting
/// off, it says nothing. Safe to call from several threads.</summary>
[FeatureSetting<FieldFeature>(FieldFeature.WhereAmI)]
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
    private readonly FreeRoamGate roaming;
    private readonly Func<bool> enabled;
    private readonly Action<string>? log;
    private readonly Func<IReadOnlyList<MainMissionRow>?> objectives;
    private readonly Lock gate = new();
    private long? lastPoll;
    private MainMissionRow? seen;
    private MainMissionRow? seenObjective;
    private bool objectivesRead;

    /// <summary>A mission line that reads the flags through <paramref name="storyFlag"/> and the main mission table
    /// through <paramref name="rows"/> (null when it can't be read), finds titles in <paramref name="messages"/> and
    /// cleans them with <paramref name="cleaner"/>, speaks through <paramref name="announcer"/>, keeps time by
    /// <paramref name="milliseconds"/>, speaks only while <paramref name="roaming"/> says the player roams the field,
    /// writes each change's flag and label to <paramref name="log"/> when there is one (Debug builds), and reads the
    /// field objectives through <paramref name="objectives"/> (null, or a null table, for none).</summary>
    public MissionLine(
        Func<int, bool?> storyFlag, Func<IReadOnlyList<MainMissionRow>?> rows, IMessageLookup messages,
        TextCleaner cleaner, IAnnouncer announcer, Func<long> milliseconds, FreeRoamGate roaming,
        Action<string>? log = null, Func<IReadOnlyList<MainMissionRow>?>? objectives = null,
        Func<bool>? enabled = null)
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
        this.objectives = objectives ?? (() => null);
        this.enabled = enabled ?? (() => true);
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
        if (!enabled())
        {
            return null;
        }
        var now = milliseconds();
        lock (gate)
        {
            if (lastPoll is { } last && now - last < PollMilliseconds)
            {
                return null;
            }
            lastPoll = now;
        }
        if (!roaming.Open() || Read() is not { } row)
        {
            return null;
        }
        // A new objective under the same main mission is a change too (#658's by-ear check of 2026-11-24). The objective
        // table loads after the first look, so an objective is compared only once one has been read.
        var table = objectives();
        var objective = table is null ? null : MainMissionFlags.Current(storyFlag, table);
        bool first;
        bool missionChanged;
        lock (gate)
        {
            missionChanged = row != seen;
            var objectiveChanged = table is not null && objectivesRead && objective != seenObjective;
            if (!missionChanged && !objectiveChanged)
            {
                RecordObjective(table, objective);
                return null;
            }
            first = seen is null;
            seen = row;
            RecordObjective(table, objective);
        }
        LogChange(missionChanged ? row : objective, missionChanged ? "mission" : "objective");
        if (first || Title(row) is not { } title)
        {
            return null;
        }
        var said = Spoken(title);
        announcer.Announce(new Announcement(said, Interrupt: false));
        return said;
    }

    // Under the gate: the objective last seen, once the table could be read.
    private void RecordObjective(IReadOnlyList<MainMissionRow>? table, MainMissionRow? objective)
    {
        if (table is not null)
        {
            seenObjective = objective;
            objectivesRead = true;
        }
    }

    private void LogChange(MainMissionRow? row, string what)
    {
        if (log is null)
        {
            return;
        }
        if (row is null)
        {
            log(what + " none changed");
            return;
        }
        var message = messages.FindByLabelHash(row.LabelHash);
        log(string.Create(CultureInfo.InvariantCulture,
            $"{what} flag=0x{row.Flag:x} label={(message is null ? "(unknown) hash=" + row.LabelHash : message.Label)} changed"));
    }

    private MainMissionRow? Read() => rows() is { } table ? MainMissionFlags.Current(storyFlag, table) : null;

    // The title, then the objective as a sentence of its own when one is known, so no colon is read out between them
    // (#743's by-ear check); the title's own trailing colon is dropped.
    private string? Title(MainMissionRow row)
    {
        if (Text(row.LabelHash) is not { } title)
        {
            return null;
        }
        title = title.TrimEnd().TrimEnd(':').TrimEnd();
        return objectives() is { } table && MainMissionFlags.Current(storyFlag, table) is { } objective &&
            Text(objective.LabelHash) is { } then
            ? $"{title}. {then}"
            : title;
    }

    private string? Text(string labelHash) =>
        messages.FindByLabelHash(labelHash) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
