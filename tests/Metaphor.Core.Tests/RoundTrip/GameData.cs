namespace Metaphor.Core.Tests.RoundTrip;

/// <summary>The result of looking for game data: the data, or why there is none.</summary>
internal sealed record GameDataSearch(GameData? Data, string MissingReason);

/// <summary>Where a local run finds game data for the <c>RoundTrip</c> tests (ADR 0001: CI never has any).</summary>
/// <param name="Root">The folder holding the extracted game files.</param>
/// <param name="Build">The game build the data came from, in lowercase hex, or <see cref="UnknownBuild"/>.</param>
internal sealed record GameData(string Root, string Build)
{
    public const string EnvironmentVariable = "METAPHOR_GAME_DATA";

    /// <summary>A file in the data folder whose first line is the game build. Later lines are free text, such as the
    /// Steam build ID.</summary>
    public const string BuildFileName = "game-build.txt";

    public const string UnknownBuild = "unknown";

    private const string FallbackFolder = "extracted";
    private const string RepositoryMarker = "Metaphor.sln";
    private const int Sha256HexLength = 64;

    /// <summary>The search for this test run's data.</summary>
    public static GameDataSearch ThisRun { get; } =
        Find(Environment.GetEnvironmentVariable(EnvironmentVariable), AppContext.BaseDirectory);

    /// <summary>True when <see cref="BuildFileName"/> names a valid game build.</summary>
    public bool HasKnownBuild => Build != UnknownBuild;

    /// <summary>Uses the folder the environment variable names, if set, and otherwise the repository's
    /// <c>extracted/</c> folder found by walking up from <paramref name="searchFrom"/>.</summary>
    public static GameDataSearch Find(string? environmentValue, string searchFrom)
    {
        if (!string.IsNullOrEmpty(environmentValue))
        {
            var root = Path.GetFullPath(environmentValue);
            return Load(root, $"{EnvironmentVariable} names {root}, which doesn't exist or is empty");
        }

        var repository = FindRepository(searchFrom);
        var missing = $"no game data: set {EnvironmentVariable}, or extract the game into {FallbackFolder}/";
        return repository is null ? new GameDataSearch(null, missing) : Load(Path.Combine(repository, FallbackFolder), missing);
    }

    private static GameDataSearch Load(string root, string missingReason) =>
        Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()
            ? new GameDataSearch(new GameData(root, ReadBuild(Path.Combine(root, BuildFileName))), string.Empty)
            : new GameDataSearch(null, missingReason);

    private static string ReadBuild(string buildFile)
    {
        if (!File.Exists(buildFile))
        {
            return UnknownBuild;
        }

        var hash = File.ReadLines(buildFile).FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;
        return hash.Length == Sha256HexLength && hash.All(char.IsAsciiHexDigitLower) ? hash : UnknownBuild;
    }

    private static string? FindRepository(string searchFrom)
    {
        for (var directory = new DirectoryInfo(searchFrom); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
