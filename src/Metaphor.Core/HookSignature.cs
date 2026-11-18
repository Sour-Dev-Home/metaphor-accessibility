namespace Metaphor.Core;

/// <summary>The byte pattern a hook uses to find its function in the game's code.</summary>
/// <param name="Name">What the hook refers to it by.</param>
/// <param name="Pattern">Hex bytes separated by spaces, with <c>??</c> for a byte that can be anything, as
/// community mods write them: <c>48 8B ?? 10</c>.</param>
/// <param name="GameBuilds">The SHA-256 of each game build it was checked on, from <c>known-game-builds.json</c>.</param>
/// <param name="Evidence">Where it came from and how it was checked: tool, date and steps.</param>
/// <param name="Section">The exe section its match is in, such as <c>.ecode</c>, read from the section table. A hook on a
/// function in <c>.text1</c> must be opt-in or development-only (ADR 0010).</param>
public sealed record HookSignature(
    string Name, string Pattern, IReadOnlyList<string> GameBuilds, string Evidence, string Section);
