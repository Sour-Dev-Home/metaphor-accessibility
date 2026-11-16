// Checks that each hook signature in src/Metaphor.Core/hook-signatures.json matches exactly once in the game exe on
// disk, without launching the game. With --probes, checks other mods' signatures instead (issue #21): those in the given
// probes file, or this folder's signature-probes.json, the same file scripts/signature-spike.ps1 sends to the mod.
// Usage: Metaphor.SignatureCheck <game exe>
//        Metaphor.SignatureCheck --probes <game exe> [probes file]
// Exit 0 when every signature matches exactly once, 1 when one doesn't, 2 on a usage error or an unreadable file.

using Metaphor.Core;

var defaultProbes = Path.Combine(AppContext.BaseDirectory, "signature-probes.json");
var (gameExe, probesFile) = args switch
{
    [var exe] when !exe.StartsWith("--", StringComparison.Ordinal) => (exe, null),
    ["--probes", var exe] => (exe, defaultProbes),
    ["--probes", var exe, var file] => (exe, file),
    _ => (null, null),
};
if (gameExe is null)
{
    Console.Error.WriteLine("usage: Metaphor.SignatureCheck [--probes] <game exe> [probes file]");
    return 2;
}
foreach (var file in new[] { gameExe, probesFile })
{
    if (file is not null && !File.Exists(file))
    {
        Console.Error.WriteLine($"no file at {file}");
        return 2;
    }
}
return probesFile is null
    ? SignatureCheck.Run(gameExe, HookSignatures.All, KnownGameBuilds.All, Console.Out)
    : SignatureCheck.RunProbes(gameExe, SignatureProbes.Parse(File.ReadAllText(probesFile)), KnownGameBuilds.All, Console.Out);
