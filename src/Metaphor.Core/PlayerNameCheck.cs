using System.Diagnostics.CodeAnalysis;
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
    /// <summary>The file in the mod's folder a development run reads the typed name from: its first line, trimmed. The
    /// mod's folder is outside the repository, so the name is never committed.</summary>
    public const string FileName = "spike-player-name.txt";

    /// <summary>The environment variable read when <see cref="FileName"/> gives no name. Whether Reloaded-II passes it
    /// on to the game is unverified.</summary>
    public const string EnvironmentVariable = "METAPHOR_SPIKE_PLAYER_NAME";

    /// <summary>How much of <see cref="FileName"/> is read, at most.</summary>
    private const int MaxNameChars = 256;

    private readonly byte[]? expected;

    /// <summary>A check against <paramref name="typedName"/>; null or empty compares as <see cref="PlayerNameMatch.Unset"/>.
    /// </summary>
    public PlayerNameCheck(string? typedName) =>
        expected = string.IsNullOrEmpty(typedName) ? null : Encoding.UTF8.GetBytes(typedName);

    /// <summary>A check against the name in <see cref="FileName"/> in <paramref name="modFolder"/>, or else in
    /// <see cref="EnvironmentVariable"/>, if the game process inherited one.</summary>
    public static PlayerNameCheck FromModFolder(string modFolder) =>
        FromModFolder(modFolder, Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>A check against the first line of <see cref="FileName"/> in <paramref name="modFolder"/>, trimmed, or
    /// against <paramref name="fallback"/> when the file is missing, unreadable or gives an empty name.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A spike's name file must never stop the hook that reads it.")]
    public static PlayerNameCheck FromModFolder(string modFolder, string? fallback)
    {
        string? typed = null;
        try
        {
            using var reader = new StreamReader(Path.Combine(modFolder, FileName), Encoding.UTF8);
            // A name is short: read a bounded block, never a whole file of any size, inside the game process.
            var block = new char[MaxNameChars];
            var text = new string(block, 0, reader.ReadBlock(block));
            typed = text.Split('\n', 2)[0].Trim();
        }
        catch (Exception)
        {
        }
        return new(string.IsNullOrEmpty(typed) ? fallback : typed);
    }

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

    /// <summary><paramref name="latin1"/>, a label read as <see cref="Sight"/> reads a string, as a log field: its text
    /// when it reads as an identifier (ASCII letters, digits, <c>_</c>) and doesn't hold the typed name, otherwise only
    /// its length (#120). Null reads as unreadable.</summary>
    public string LabelField(string? latin1)
    {
        if (latin1 is null)
        {
            return "text=(unreadable)";
        }
        var bytes = Encoding.Latin1.GetBytes(latin1);
        if (Compare(bytes) is PlayerNameMatch.Exact or PlayerNameMatch.Contains)
        {
            return string.Create(CultureInfo.InvariantCulture, $"text=(typed name, {bytes.Length} bytes)");
        }
        return latin1.Length > 0 && latin1.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? $"text=\"{latin1}\""
            : string.Create(CultureInfo.InvariantCulture, $"text=(not an identifier, {bytes.Length} bytes)");
    }
}
