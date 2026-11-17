// Summarises the mod's local message-log.txt (issue #244) without printing game text: counts by record kind, screen
// classes with first and last time and cursor inputs, and the records in a time window (Metaphor.Core's SessionLogSummary).
// Usage: tools/session-log <message-log.txt> [--from <ms>] [--to <ms>] [--offset <ms>]
//   --offset: the gap in ms between the mod's start and the recording's start; times then also show as video times
//   (log time minus the offset).
// Exit 0 on success, 2 on a usage error or a missing file.

using System.Globalization;
using Metaphor.Core.Replay;

const string Usage = "usage: session-log <message-log.txt> [--from <ms>] [--to <ms>] [--offset <ms>]";
if (args.Length == 0 || args.Length % 2 == 0)
{
    Console.Error.WriteLine(Usage);
    return 2;
}
long? from = null, to = null;
long offset = 0;
for (var i = 1; i < args.Length; i += 2)
{
    if (!long.TryParse(args[i + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }
    switch (args[i])
    {
        case "--from": from = value; break;
        case "--to": to = value; break;
        case "--offset": offset = value; break;
        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
}
if (!File.Exists(args[0]))
{
    // Only the file name: this output can go into issues, which hold no local paths.
    Console.Error.WriteLine($"no file named {Path.GetFileName(args[0])} there");
    return 2;
}
Console.Out.Write(SessionLogSummary.Summarize(File.ReadLines(args[0]), new SummaryOptions(from, to, offset)));
return 0;
