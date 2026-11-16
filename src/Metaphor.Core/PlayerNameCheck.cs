using System.Globalization;
using System.Text;

namespace Metaphor.Core;

/// <summary>How a string the game handled compares with the name the maintainer typed for the hero (#106).</summary>
public enum PlayerNameMatch
{
    /// <summary>No name to compare with was given.</summary>
    Unset,

    /// <summary>The string is the name, byte for byte.</summary>
    Exact,

    /// <summary>The string holds the name among other text.</summary>
    Contains,

    /// <summary>The name isn't in the string.</summary>
    No,
}

/// <summary>What #106's listeners may log about a string that could be the player's chosen name: its length and how it
/// compares, never the text. <paramref name="Location"/> says where the string was (<c>module+0x…</c>, <c>heap</c>).
/// </summary>
public readonly record struct PlayerNameSighting(int Bytes, PlayerNameMatch Match, string Location)
{
    /// <summary>The sighting as log fields.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"bytes={Bytes} match={Match.ToString().ToLowerInvariant()} at={Location}");
}

/// <summary>Compares strings from the game with the name the maintainer typed for the hero, so a log can say whether a
/// string was the name without holding it (#106). The name is personal data: this type never returns or formats it.
/// </summary>
public sealed class PlayerNameCheck
{
    /// <summary>The environment variable a development run reads the typed name from.</summary>
    public const string EnvironmentVariable = "METAPHOR_SPIKE_PLAYER_NAME";

    private readonly byte[]? expected;

    /// <summary>A check against <paramref name="typedName"/>; null or empty compares as <see cref="PlayerNameMatch.Unset"/>.
    /// </summary>
    public PlayerNameCheck(string? typedName) =>
        expected = string.IsNullOrEmpty(typedName) ? null : Encoding.UTF8.GetBytes(typedName);

    /// <summary>A check against the name in <see cref="EnvironmentVariable"/>, if the game process inherited one.</summary>
    public static PlayerNameCheck FromEnvironment() => new(Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>Compares <paramref name="text"/>, the bytes as the game holds them, with the typed name's UTF-8 bytes.
    /// That the game holds the name as UTF-8 is unverified.</summary>
    public PlayerNameMatch Compare(ReadOnlySpan<byte> text)
    {
        if (expected is null)
        {
            return PlayerNameMatch.Unset;
        }
        if (text.SequenceEqual(expected))
        {
            return PlayerNameMatch.Exact;
        }
        return text.IndexOf(expected) >= 0 ? PlayerNameMatch.Contains : PlayerNameMatch.No;
    }

    /// <summary>A sighting of <paramref name="latin1"/>, a C string read byte for byte as Latin-1 (as
    /// <see cref="MemoryText.ReadAnsi"/> reads it), at <paramref name="location"/>.</summary>
    public PlayerNameSighting Sight(string latin1, string location)
    {
        var bytes = Encoding.Latin1.GetBytes(latin1 ?? "");
        return new PlayerNameSighting(bytes.Length, Compare(bytes), location);
    }
}
