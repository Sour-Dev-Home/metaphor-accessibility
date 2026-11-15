// Checks that each hook signature in src/Metaphor.Core/hook-signatures.json matches exactly once in the game exe on
// disk, without launching the game. With --probes, checks the other mods' signatures in this folder's
// signature-probes.json instead (issue #21); scripts/deploy-dev.ps1 -ScanSignatures sends the same file to the mod.
// Usage: Metaphor.SignatureCheck [--probes] <game exe>
// Exit 0 when every signature matches exactly once, 1 when one doesn't, 2 on a usage error or an unreadable exe.

using Metaphor.Core;

var probes = args is ["--probes", _];
if ((probes ? args[1..] : args) is not [var gameExe] || gameExe.StartsWith("--", StringComparison.Ordinal))
{
    Console.Error.WriteLine("usage: Metaphor.SignatureCheck [--probes] <game exe>");
    return 2;
}
if (!File.Exists(gameExe))
{
    Console.Error.WriteLine($"no file at {gameExe}");
    return 2;
}
return probes
    ? SignatureCheck.RunProbes(gameExe,
        SignatureProbes.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "signature-probes.json"))),
        KnownGameBuilds.All, Console.Out)
    : SignatureCheck.Run(gameExe, HookSignatures.All, KnownGameBuilds.All, Console.Out);
