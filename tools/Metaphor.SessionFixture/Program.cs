// Writes a replay fixture (issue #149) from a window of the mod's local message-log.txt to standard output: record kinds,
// labels, paths, numbers and relative times only, no game text (Metaphor.Core's SessionLogConverter). Save the output
// under tests/fixtures/sessions/<behaviour>.txt.
// Usage: Metaphor.SessionFixture <message-log.txt> <from ms> <to ms> <kinds> <game build> <raw source> <title>
//   kinds: a comma-separated list of compile, request, select, cursor, pad, window.
// Exit 0 when it wrote the fixture, 1 when the log has a kept record that looks like text, 2 on a usage error.

using System.Globalization;
using Metaphor.Core.Replay;

if (args is not [var log, var fromText, var toText, var kinds, var build, var source, var title]
    || !long.TryParse(fromText, CultureInfo.InvariantCulture, out var from)
    || !long.TryParse(toText, CultureInfo.InvariantCulture, out var to))
{
    Console.Error.WriteLine("usage: Metaphor.SessionFixture <message-log.txt> <from ms> <to ms> <kinds> <game build> <raw source> <title>");
    return 2;
}
if (!File.Exists(log))
{
    // Only the file name: this output can go into issues, which hold no local paths.
    Console.Error.WriteLine($"no file named {Path.GetFileName(log)} there");
    return 2;
}
try
{
    Console.Out.Write(SessionLogConverter.Convert(File.ReadLines(log), new FixtureWindow(from, to, kinds.Split(',')),
        new FixtureHeader(title, source, build)));
    return 0;
}
catch (ArgumentException refused)
{
    Console.Error.WriteLine(refused.Message);
    return 2;
}
catch (FormatException refused)
{
    Console.Error.WriteLine(refused.Message);
    return 1;
}
