using System.Text.Json;

namespace Metaphor.Core;

/// <summary>One game build: an exact release of the game's executable.</summary>
/// <param name="Sha256">The identifier: the executable's SHA-256 in lowercase hex.</param>
/// <param name="SteamBuildId">A readable label: Steam's build ID for this release.</param>
/// <param name="GameVersion">A readable label: the version the game shows, or null when it hasn't been recorded.</param>
/// <param name="Evidence">How the identifier and labels were found: tool, date and steps.</param>
public sealed record GameBuild(string Sha256, long SteamBuildId, string? GameVersion, string Evidence);

/// <summary>The game builds we have verified, from <c>known-game-builds.json</c>. Adding a build is a change to that
/// file only (docs/contributors/how-to/add-a-game-build.md).</summary>
public static class KnownGameBuilds
{
    private const string ResourceName = "Metaphor.Core.known-game-builds.json";
    private const int Sha256HexLength = 64;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Every known build, in file order.</summary>
    public static IReadOnlyList<GameBuild> All { get; } = Load();

    /// <summary>Reads and validates a known-builds file.</summary>
    /// <exception cref="InvalidDataException">An entry is malformed or listed twice.</exception>
    public static IReadOnlyList<GameBuild> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<GameBuild> builds;
        try
        {
            builds = JsonSerializer.Deserialize<List<GameBuild>>(json, Options) ?? [];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"known-builds file is malformed: {e.Message}", e);
        }

        foreach (var build in builds)
        {
            if (build is null)
            {
                throw new InvalidDataException("known-builds file has an empty entry");
            }
            if (!IsSha256(build.Sha256))
            {
                throw new InvalidDataException($"'{build.Sha256}' is not a lowercase hex SHA-256");
            }
            if (build.SteamBuildId <= 0)
            {
                throw new InvalidDataException($"build {build.Sha256} has no Steam build ID");
            }
            if (string.IsNullOrWhiteSpace(build.Evidence))
            {
                throw new InvalidDataException($"build {build.Sha256} has no evidence");
            }
        }

        var duplicate = builds.GroupBy(build => build.Sha256).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? builds : throw new InvalidDataException($"build {duplicate.Key} is listed twice");
    }

    private static List<GameBuild> Load()
    {
        using var stream = typeof(KnownGameBuilds).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"missing embedded resource {ResourceName}");
        using var reader = new StreamReader(stream);
        return [.. Parse(reader.ReadToEnd())];
    }

    /// <summary>Whether <paramref name="value"/> has the shape of a game build's identifier: lowercase hex SHA-256.</summary>
    internal static bool IsSha256(string? value) =>
        value is { Length: Sha256HexLength } && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
}
