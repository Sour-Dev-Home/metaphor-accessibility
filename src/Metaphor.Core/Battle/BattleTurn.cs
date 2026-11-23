namespace Metaphor.Core.Battle;

/// <summary>Whose turn it is and the turn icons left (#252): <see cref="Side"/> 0 is the player's turn and 1 the
/// enemy's, <see cref="Full"/> the full icons and <see cref="Half"/> the half ones, as the turn-icon panel shows them
/// (docs/findings/battle-turn-side-start-and-end-layouts-logged.md).</summary>
public sealed record TurnIcons(int Side, int Full, int Half)
{
    /// <summary>The side value on the player's turn.</summary>
    public const int PlayerSide = 0;

    /// <summary>The side value on the enemy's turn.</summary>
    public const int EnemySide = 1;

    /// <summary>The bytes <see cref="From"/> decodes: the side's <c>int</c>, then the full and half icons'
    /// <c>short</c>s.</summary>
    public const int Size = 8;

    // More icons than any party's turn holds is a misread, not a turn.
    private const int MostIcons = 16;

    /// <summary>Decodes the game's <see cref="Size"/> bytes, little-endian; null for too few bytes, a side other than
    /// 0 or 1, or a count out of range.</summary>
    public static TurnIcons? From(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
        {
            return null;
        }
        var side = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var full = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]);
        var half = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]);
        return side is PlayerSide or EnemySide && full is >= 0 and <= MostIcons && half is >= 0 and <= MostIcons
            ? new TurnIcons(side, full, half)
            : null;
    }
}

/// <summary>Reads the turn icons the game holds (#252).</summary>
public interface IBattleTurnIcons
{
    /// <summary>The turn icons now, or null when unknown or out of range. Outside a battle it holds the last one's.
    /// </summary>
    TurnIcons? Read();
}
