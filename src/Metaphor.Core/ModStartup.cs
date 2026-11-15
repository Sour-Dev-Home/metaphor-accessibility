using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Metaphor.Core;

/// <summary>What the mod does first when the game loads it: identify the running game build and report through its
/// status file whether it will install its hooks. The Reloaded-II entry point in the hook layer only calls this.</summary>
public static class ModStartup
{
    /// <summary>The game build reported when the game executable couldn't be hashed.</summary>
    public const string UnknownBuild = "unknown";

    /// <summary>Hashes <paramref name="gameExe"/>, looks it up in <paramref name="knownBuilds"/>, and writes the result
    /// into <paramref name="modFolder"/>'s status file. Never throws: Reloaded-II rethrows a mod's start exception
    /// (docs/raw-sources/reloaded-ii-mod-entry-point-2026-11-15.md), so any failure becomes a refusal instead.</summary>
    /// <returns>The status, which the caller should log: writing it to the folder is best effort.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must refuse rather than reach the game.")]
    public static ModStatus CheckAndReport(
        string modId, string gameExe, string modFolder, Func<IReadOnlyList<GameBuild>> knownBuilds, int hooksExpected)
    {
        var status = Check(modId, gameExe, knownBuilds, hooksExpected);
        try
        {
            ModStatusFile.Write(modFolder, status);
        }
        catch (Exception)
        {
            // Nothing else to tell the smoke test with; the caller logs the returned status.
        }
        return status;
    }

    [SuppressMessage("Design", "CA1031", Justification = "Any failure must refuse rather than reach the game.")]
    private static ModStatus Check(string modId, string gameExe, Func<IReadOnlyList<GameBuild>> knownBuilds, int hooksExpected)
    {
        string gameBuild;
        try
        {
            using var stream = File.OpenRead(gameExe);
            gameBuild = Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception e)
        {
            // The error's type, not its message: the message holds the exe's path, which can name the user's folder.
            return ModStatus.Refused(modId, UnknownBuild, hooksExpected, $"can't hash the game executable: {e.GetType().Name}");
        }

        try
        {
            return knownBuilds().Any(build => build.Sha256 == gameBuild)
                ? ModStatus.Ready(modId, gameBuild, hooksLoaded: 0, hooksExpected)
                : ModStatus.Refused(modId, gameBuild, hooksExpected, "unknown game build");
        }
        catch (Exception e)
        {
            // KnownGameBuilds.All fails as a TypeInitializationException; its inner exception says why.
            return ModStatus.Refused(modId, gameBuild, hooksExpected, e.GetBaseException().Message);
        }
    }
}
