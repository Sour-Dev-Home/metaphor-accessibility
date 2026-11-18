using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metaphor.Core;

/// <summary>Whether a mod started its hooks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModState>))]
public enum ModState
{
    /// <summary>The mod checked the game build and installed its hooks.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The mod declined to install its hooks, for example on an unknown game build.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,
}

/// <summary>What a mod reports about its start, for the smoke test (scripts/smoke-test.ps1) to read.</summary>
/// <param name="ModId">The mod's Reloaded-II mod ID.</param>
/// <param name="State">Whether it installed its hooks.</param>
/// <param name="GameBuildSha256">The SHA-256 of the game build it found running.</param>
/// <param name="HooksLoaded">How many of its hooks it installed.</param>
/// <param name="HooksExpected">How many hooks it has.</param>
/// <param name="Reason">Why it refused, or null when ready.</param>
public sealed record ModStatus(
    string ModId, ModState State, string GameBuildSha256, int HooksLoaded, int HooksExpected, string? Reason)
{
    /// <summary>The dev opt-ins the run asked for, for the load message (#223). Not in the status file.</summary>
    [JsonIgnore]
    public IReadOnlyList<OptInState> OptIns { get; init; } = [];

    public static ModStatus Ready(string modId, string gameBuildSha256, int hooksLoaded, int hooksExpected) =>
        new(modId, ModState.Ready, gameBuildSha256, hooksLoaded, hooksExpected, null);

    public static ModStatus Refused(string modId, string gameBuildSha256, int hooksExpected, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(modId, ModState.Refused, gameBuildSha256, 0, hooksExpected, reason);
    }
}

/// <summary>Each mod's status file, <see cref="FileName"/> in the mod's own folder. It's replaced in one step, so a
/// reader never sees half a file.</summary>
public static class ModStatusFile
{
    /// <summary>The file name. scripts/smoke-test.ps1 reads the same name; its tests fail if the two differ.</summary>
    public const string FileName = "mod-status.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Writes the status into <paramref name="modFolder"/>, replacing any earlier one.</summary>
    public static void Write(string modFolder, ModStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        AtomicFile.WriteAllText(Path.Combine(modFolder, FileName), JsonSerializer.Serialize(status, Options));
    }
}
