// Reads files out of the game's CPK archives for findings work (#895), into the repository's git-ignored extracted/
// folder only (docs/contributors/how-to/read-game-data-for-findings.md). Metaphor.Core's CpkArchive does the reading.
// Usage: dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list <cpk> [filter]
//        dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- extract <cpk> <path-in-archive>... --out extracted/<dir>
// Exit 0 on success, 1 when a file couldn't be read, 2 on a usage error or a refused path.

using Metaphor.CpkExtract;

return CpkExtractCommand.Run(args, Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory(), Console.Out, Console.Error);
