using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Metaphor.Core.Replay;

/// <summary>Which records of a log a fixture keeps: those from <paramref name="FromMilliseconds"/> to
/// <paramref name="ToMilliseconds"/> (both included) of the <paramref name="Kinds"/> named (<c>compile</c>,
/// <c>request</c>, <c>select</c>, <c>cursor</c>, <c>pad</c>, <c>window</c>).</summary>
public sealed record FixtureWindow(long FromMilliseconds, long ToMilliseconds, IReadOnlyCollection<string> Kinds);

/// <summary>What a fixture says about itself: the behaviour it shows, the raw source of the session it came from, and
/// the game build it was recorded on (ground rule 2).</summary>
public sealed record FixtureHeader(string Title, string Source, string GameBuild);

/// <summary>Turns a window of a local <see cref="MessageLog"/> into a replay fixture (#149;
/// <see cref="SessionFixture"/> has the format). Only record kinds, message labels, file paths, numbers and times
/// relative to the window's first kept record are kept; everything else, such as a compiled file's source or a pane's
/// text, is dropped. A kept field that doesn't look like a label, path or number is refused, never copied, so a
/// fixture can't carry game text (ground rule 4).</summary>
public static partial class SessionLogConverter
{
    /// <summary>Converts <paramref name="log"/>'s lines. Throws <see cref="FormatException"/>, naming the line but not
    /// its text, when a kept record holds something that looks like text.</summary>
    public static string Convert(IEnumerable<string> log, FixtureWindow window, FixtureHeader header)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(header);
        if (!GameBuildPattern().IsMatch(header.GameBuild))
        {
            throw new ArgumentException("The game build must be the exe's SHA-256, as 64 lower-case hex digits.", nameof(header));
        }
        var fixture = new StringBuilder();
        foreach (var line in new[] { header.Title, $"source: {header.Source}", $"game build: {header.GameBuild}" })
        {
            fixture.Append("# ").Append(line.ReplaceLineEndings(" ")).Append('\n');
        }
        long? first = null;
        string? lastCursorManager = null;
        var number = 0;
        foreach (var line in log)
        {
            number++;
            var record = RecordPattern().Match(line);
            if (!record.Success)
            {
                continue;
            }
            var at = long.Parse(record.Groups["ms"].Value, CultureInfo.InvariantCulture);
            if (at < window.FromMilliseconds || at > window.ToMilliseconds)
            {
                continue;
            }
            if (record.Groups["kind"].Value == "screen")
            {
                // The #213 spike logged each cursor's screen class as a record of its own, right after the cursor's.
                if (SpikeScreenClass(record.Groups["rest"].Value, number) is { } spike && spike.Manager == lastCursorManager
                    && spike.ScreenClass is { } screenClass)
                {
                    fixture.Length--;
                    fixture.Append(" screen=").Append(screenClass).Append('\n');
                }
                lastCursorManager = null;
                continue;
            }
            lastCursorManager = null;
            if (Kept(record.Groups["kind"].Value, record.Groups["rest"].Value, number) is not { } kept
                || !window.Kinds.Contains(kept.Kind))
            {
                continue;
            }
            first ??= at;
            fixture.Append((at - first.Value).ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(kept.Kind).Append(' ').Append(kept.Fields).Append('\n');
            if (kept.Kind == "cursor" && !kept.Fields.Contains(" screen=", StringComparison.Ordinal))
            {
                lastCursorManager = CursorPattern().Match(record.Groups["rest"].Value).Groups["mgr"].Value;
            }
        }
        return fixture.ToString();
    }

    // A spike log's screen record: the manager, and its owner's class, null when unreadable.
    private static (string Manager, string? ScreenClass)? SpikeScreenClass(string rest, int number)
    {
        var screen = Matched(SpikeScreenPattern(), rest, number);
        return (screen["mgr"].Value, screen["owner"].Value == "?" ? null : screen["owner"].Value);
    }

    /// <summary>The fixture record for one log record, null for a kind fixtures don't keep.</summary>
    private static (string Kind, string Fields)? Kept(string kind, string rest, int number)
    {
        switch (kind)
        {
            case "compile":
                var compile = Matched(CompilePattern(), rest, number);
                return ("compile", $"id={compile["id"].Value} path={compile["path"].Value}");
            case "request" when rest.StartsWith("via=MsgRequestByName ", StringComparison.Ordinal):
                var request = Matched(RequestPattern(), rest, number);
                return ("request", $"kind={request["kind"].Value} label={request["label"].Value}");
            case "select":
                var select = Matched(SelectPattern(), rest, number);
                return ("select", $"kind={select["kind"].Value} prompt={Label(select["first"].Value)} choice={Label(select["second"].Value)}");
            case "cursor":
                var cursor = Matched(CursorPattern(), rest, number);
                var screen = cursor["screen"].Success && cursor["screen"].Value != "?" ? $" screen={cursor["screen"].Value}" : "";
                var count = cursor["count"].Success && cursor["count"].Value != "?" ? $" count={cursor["count"].Value}" : "";
                return ("cursor", $"mgr={cursor["mgr"].Value} class={cursor["class"].Value} cmd={cursor["cmd"].Value} result={cursor["result"].Value} top={cursor["top"].Value} row={cursor["row"].Value} flags={cursor["flags"].Value}{screen}{count}");
            case "input" when rest.StartsWith("pad", StringComparison.Ordinal):
                var pad = Matched(PadPattern(), rest, number);
                return ("pad", $"pad={pad["pad"].Value} buttons={pad["buttons"].Value} returned={pad["returned"].Value}");
            case "msgwindow":
                var window = Matched(WindowPattern(), rest, number);
                return ("window", $"current={(window["current"].Value is "" or "?" ? "-" : window["current"].Value)}");
            default:
                return null;
        }
    }

    private static GroupCollection Matched(Regex pattern, string rest, int number)
    {
        var match = pattern.Match(rest);
        return match.Success
            ? match.Groups
            : throw new FormatException(string.Create(CultureInfo.InvariantCulture,
                $"line {number}: a kept record has a field that isn't a label, path or number; nothing was written."));
    }

    // A label in the log is quoted; an unreadable one is "(unreadable)" and becomes "-".
    private static string Label(string logged) => logged == "(unreadable)" ? "-" : logged.Trim('"');

    // Labels have no spaces and at least one underscore (MES_e00_002_001_004, Title_System_08), so a word or a sentence
    // never passes for one.
    private const string LabelPattern = "[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+";
    private const string QuotedLabel = "\"" + LabelPattern + "\"|\\(unreadable\\)";
    // A class's decorated name from run-time type information, without its .?AV prefix (TitleChoice@ope@ui@app,
    // ?$ConfigMenuChoicePcT@$0HG@@ope@ui@app): no spaces, and at least one @, so a word never passes for one.
    private const string ClassName = "[A-Za-z0-9_?$]+(?:@[A-Za-z0-9_?$]*)+";
    private const string Hex = "0x[0-9a-f]+";
    private const string Int = "-?[0-9]+";

    [GeneratedRegex("^(?<ms>[0-9]+) ms (?<kind>[a-z-]+) (?<rest>.*)$")]
    private static partial Regex RecordPattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex GameBuildPattern();

    // A path inside the game's files, relative, ending in .msg.
    [GeneratedRegex("^id=(?<id>" + Int + ") path=(?<path>[A-Za-z0-9_]+(?:/[A-Za-z0-9_.]+)*\\.msg) chars=[0-9]+$")]
    private static partial Regex CompilePattern();

    [GeneratedRegex("^via=MsgRequestByName kind=(?<kind>" + Int + ") text=\"(?<label>" + LabelPattern + ")\"$")]
    private static partial Regex RequestPattern();

    [GeneratedRegex("^kind=(?<kind>" + Int + ") first=(?<first>" + QuotedLabel + ") second=(?<second>" + QuotedLabel + ")$")]
    private static partial Regex SelectPattern();

    [GeneratedRegex("^mgr=(?<mgr>" + Hex + ") class=(?<class>" + Hex + "|\\?) cmd=(?<cmd>" + Hex + ") result=(?<result>" + Int
        + ") index=" + Int + " top=(?<top>" + Int + ") row=(?<row>" + Int + ") flags=(?<flags>" + Hex + ")(?: screen=(?<screen>"
        + ClassName + "|\\?))?(?: count=(?<count>" + Int + "|\\?))?$")]
    private static partial Regex CursorPattern();

    [GeneratedRegex("^mgr=(?<mgr>" + Hex + ") mgrClass=\\S+ owner=\\S+ ownerClass=(?<owner>" + ClassName + "|\\?) fields=\\S+$")]
    private static partial Regex SpikeScreenPattern();

    [GeneratedRegex("^pad(?<pad>[0-9]+) buttons=(?<buttons>" + Hex + ") handed=" + Hex + " returned=(?<returned>[0-9]+)$")]
    private static partial Regex PadPattern();

    // The window's message pointer, or no window object (none) or no read at all; the rest of the record is numbers.
    [GeneratedRegex("^(?:window=(?:none|\\(unreadable\\))|current=(?<current>" + Hex + "|\\?)(?: state=(?:" + Int
        + "|\\?) msg\\+48=(?:" + Int + "|-|\\?))?)(?: handles=(?:none|\\(unreadable\\)|[0-9?]+\\+[0-9?]+))?$")]
    private static partial Regex WindowPattern();
}
