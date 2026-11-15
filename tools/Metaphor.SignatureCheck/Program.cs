// Checks that each hook signature in src/Metaphor.Core/hook-signatures.json matches exactly once in the game exe on
// disk, without launching the game. Usage: Metaphor.SignatureCheck <game exe>
// Exit 0 when every signature matches exactly once, 1 when one doesn't, 2 on a usage error or an unreadable exe.

using Metaphor.Core;

if (args is not [var gameExe])
{
    Console.Error.WriteLine("usage: Metaphor.SignatureCheck <game exe>");
    return 2;
}
if (!File.Exists(gameExe))
{
    Console.Error.WriteLine($"no file at {gameExe}");
    return 2;
}
return SignatureCheck.Run(gameExe, HookSignatures.All, KnownGameBuilds.All, Console.Out);
