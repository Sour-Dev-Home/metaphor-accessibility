namespace Metaphor.Hooks;

/// <summary>Marks a hook and names the game builds it was verified on, by SHA-256 from
/// <c>src/Metaphor.Core/known-game-builds.json</c>. Checked by architecture rule 6 in docs/coding-standards.md.</summary>
/// <param name="gameBuilds">The SHA-256 of each supported game build, in lowercase hex.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GameHookAttribute(params string[] gameBuilds) : Attribute
{
    /// <summary>The supported game builds' SHA-256 identifiers.</summary>
    public IReadOnlyList<string> GameBuilds { get; } = gameBuilds;
}
