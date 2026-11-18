namespace Metaphor.Hooks;

/// <summary>Marks a hook and names the game builds it was verified on, by SHA-256 from
/// <c>src/Metaphor.Core/known-game-builds.json</c>. Checked by architecture rule 6 in docs/coding-standards.md.</summary>
/// <param name="gameBuilds">The SHA-256 of each supported game build, in lowercase hex.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GameHookAttribute(params string[] gameBuilds) : Attribute
{
    /// <summary>The supported game builds' SHA-256 identifiers.</summary>
    public IReadOnlyList<string> GameBuilds { get; } = gameBuilds;

    /// <summary>A hook for development only, such as a spike's listening hook: the loader installs it in Debug builds
    /// and never in Release, which players get. It's still compiled and checked like any other hook.</summary>
    public bool DevOnly { get; init; }

    /// <summary>For a risky hook, the bare name of a file that must be in the mod's folder for it to install; null for
    /// none. Without the file the loader skips it with a log line naming the file, and doesn't count it as expected.
    /// With it, the hook still goes through the build check and signature scan (<see cref="Metaphor.Core.HookOptIn"/>).
    /// </summary>
    public string? OptInFile { get; init; }

    /// <summary>The opt-in's name in plain words, which the load message says (#223); the file name when null. Hooks
    /// sharing an <see cref="OptInFile"/> give the same name.</summary>
    public string? OptInName { get; init; }

    /// <summary>For one of a pair of hooks that only work together, the other's class name; null for none. If either
    /// can't go in, the loader installs neither and logs why, and it tells each with <c>PairInstalled</c> only once both
    /// are in (#172, <see cref="Metaphor.Core.HookSpec.Partner"/>).</summary>
    public string? Partner { get; init; }
}
