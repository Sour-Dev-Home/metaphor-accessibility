using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Metaphor.Core.Replay;

/// <summary>What <see cref="SessionLogSummary"/> reports on: an optional window of log time, and the gap to subtract
/// from log times to get video times (#244).</summary>
public sealed record SummaryOptions(long? From = null, long? To = null, long VideoOffsetMs = 0);

/// <summary>Summarises a local <c>message-log.txt</c> (#244) without printing game text: counts by record kind, screen
/// classes with first and last time and cursor inputs, and the same counts for a time window. Only a record's kind, its
/// time, numbers and class names that look like identifiers reach the output; the rest of a record is never copied.
/// A <c>screen</c> record names its manager's class (<c>mgrClass=</c>), and a <c>cursor</c> record carries the manager's
/// address (<c>mgr=</c>), which is how inputs are counted per class. Record forms:
/// <c>src/Metaphor.Core/MessageLog.cs</c>.</summary>
public static partial class SessionLogSummary
{
    [GeneratedRegex(@"^(\d+) ms ([a-z][a-z0-9-]*)(?: |$)", RegexOptions.CultureInvariant)]
    private static partial Regex Record();

    [GeneratedRegex(@"\bmgrClass=([A-Za-z0-9_:<>]{1,120})(?= |$)", RegexOptions.CultureInvariant)]
    private static partial Regex ManagerClass();

    [GeneratedRegex(@"\bmgr=(0x[0-9a-f]{1,16})(?= |$)", RegexOptions.CultureInvariant)]
    private static partial Regex Manager();

    /// <summary>The summary of <paramref name="log"/>'s lines, as text.</summary>
    public static string Summarize(IEnumerable<string> log, SummaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(options);
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var windowKinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var screens = new SortedDictionary<string, Screen>(StringComparer.Ordinal);
        var classOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var cursorsByManager = new Dictionary<string, int>(StringComparer.Ordinal);
        long continuation = 0, unrecognised = 0, first = long.MaxValue, last = long.MinValue;

        foreach (var line in log)
        {
            if (line.StartsWith("  | ", StringComparison.Ordinal))
            {
                continuation++;
                continue;
            }
            if (Record().Match(line) is not { Success: true } match
                || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var at))
            {
                unrecognised++;
                continue;
            }
            var kind = match.Groups[2].Value;
            first = Math.Min(first, at);
            last = Math.Max(last, at);
            Bump(kinds, kind);
            if (at >= (options.From ?? long.MinValue) && at <= (options.To ?? long.MaxValue))
            {
                Bump(windowKinds, kind);
            }
            if (kind == "screen" && ManagerClass().Match(line) is { Success: true } named
                && Manager().Match(line) is { Success: true } owner)
            {
                var name = named.Groups[1].Value;
                screens[name] = screens.GetValueOrDefault(name).Seen(at);
                classOf[owner.Groups[1].Value] = name;
            }
            else if (kind == "cursor" && Manager().Match(line) is { Success: true } cursor)
            {
                cursorsByManager[cursor.Groups[1].Value] = cursorsByManager.GetValueOrDefault(cursor.Groups[1].Value) + 1;
            }
        }

        var inputs = new Dictionary<string, int>(StringComparer.Ordinal);
        var unattributed = 0;
        foreach (var (manager, count) in cursorsByManager)
        {
            if (classOf.TryGetValue(manager, out var name))
            {
                inputs[name] = inputs.GetValueOrDefault(name) + count;
            }
            else
            {
                unattributed += count;
            }
        }

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"records: {kinds.Values.Sum()}, text continuation lines: {continuation}, unrecognised lines: {unrecognised}");
        if (kinds.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"log time: {Time(first, options)} to {Time(last, options)}");
        }
        text.AppendLine("by kind:");
        foreach (var (kind, count) in kinds)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {kind} {count}");
        }
        text.AppendLine("by screen class (first, last, screen records, cursor inputs):");
        foreach (var (name, screen) in screens)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  {name} {Time(screen.First, options)} {Time(screen.Last, options)} {screen.Count} {inputs.GetValueOrDefault(name)}");
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"  cursor inputs with no screen record: {unattributed}");
        if (options.From is not null || options.To is not null)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"window {options.From?.ToString(CultureInfo.InvariantCulture) ?? "start"} to {options.To?.ToString(CultureInfo.InvariantCulture) ?? "end"} ms: {windowKinds.Values.Sum()} records");
            foreach (var (kind, count) in windowKinds)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  {kind} {count}");
            }
        }
        return text.ToString();
    }

    private static void Bump(SortedDictionary<string, int> counts, string key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;

    /// <summary>A log time as <c>ms</c>, and as <c>(video m:ss.mmm)</c> when there is an offset.</summary>
    private static string Time(long logMs, SummaryOptions options)
    {
        if (options.VideoOffsetMs == 0)
        {
            return $"{logMs.ToString(CultureInfo.InvariantCulture)}ms";
        }
        var video = TimeSpan.FromMilliseconds(logMs - options.VideoOffsetMs);
        var sign = video < TimeSpan.Zero ? "-" : "";
        var shown = video.Duration();
        return string.Create(CultureInfo.InvariantCulture,
            $"{logMs}ms(video {sign}{(int)shown.TotalMinutes}:{shown.Seconds:00}.{shown.Milliseconds:000})");
    }

    private readonly record struct Screen(long First, long Last, int Count)
    {
        public Screen Seen(long at) => Count == 0 ? new Screen(at, at, 1) : new Screen(Math.Min(First, at), Math.Max(Last, at), Count + 1);
    }
}
