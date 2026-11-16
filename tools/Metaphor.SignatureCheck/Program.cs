// Checks that each hook signature in src/Metaphor.Core/hook-signatures.json matches exactly once in the game exe on
// disk, without launching the game. With --probes, checks other mods' signatures instead (issue #21): those in the given
// probes file, or this folder's signature-probes.json, the same file scripts/signature-spike.ps1 sends to the mod.
// With --bytes, prints the exe's bytes at an address relative to the module instead (issue #70; Metaphor.Core's ExeBytes).
// Usage: Metaphor.SignatureCheck <game exe>
//        Metaphor.SignatureCheck --probes <game exe> [probes file]
//        Metaphor.SignatureCheck --bytes <game exe> <rva> <length>
// Exit 0 when every signature matches exactly once, 1 when one doesn't, 2 on a usage error or an unreadable file.
// --bytes exits 0 when it printed the bytes, 1 when the file doesn't hold them, 2 on a usage error.

using Metaphor.Core;

if (args is ["--bytes", var bytesExe, var rvaText, var lengthText])
{
    var rva = ExeBytes.ParseNumber(rvaText);
    var length = ExeBytes.ParseNumber(lengthText);
    if (rva is null || length is null or > int.MaxValue)
    {
        Console.Error.WriteLine("usage: Metaphor.SignatureCheck --bytes <game exe> <rva> <length>, numbers in hex after 0x or in decimal");
        return 2;
    }
    if (!File.Exists(bytesExe))
    {
        // Only the file name: this output goes into issues and raw sources, which hold no local paths.
        Console.Error.WriteLine($"no file named {Path.GetFileName(bytesExe)} there");
        return 2;
    }
    return ExeBytes.Run(bytesExe, rva.Value, (int)length.Value, KnownGameBuilds.All, Console.Out);
}

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
    Console.Error.WriteLine("usage: Metaphor.SignatureCheck [--probes] <game exe> [probes file], or --bytes <game exe> <rva> <length>");
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
