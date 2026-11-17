namespace Metaphor.Core;

/// <summary>The held keys in the game's keyboard state (#58): a 32-bit count at <c>+4</c> and up to six virtual-key
/// codes from <c>+8</c>, as <c>KeyboardRead</c> fills them on game build 548ddc95… (Steam build 18330018):
/// docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.</summary>
public static class GameKeyList
{
    /// <summary>How many key codes the state holds.</summary>
    public const int Slots = 6;

    /// <summary>The keys <paramref name="slots"/> holds, by <paramref name="count"/>. A count the game can't have written
    /// reads as no keys.</summary>
    public static IReadOnlyList<int> Read(int count, ReadOnlySpan<byte> slots)
    {
        if (count < 0 || count > Slots || count > slots.Length)
        {
            return [];
        }
        var keys = new int[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = slots[i];
        }
        return keys;
    }

    /// <summary>The six slots holding <paramref name="keys"/> in order, the rest zero; the count to go with them is
    /// <paramref name="keys"/>' own.</summary>
    /// <exception cref="ArgumentException">More than six keys.</exception>
    public static byte[] Write(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count > Slots)
        {
            throw new ArgumentException($"The state holds at most {Slots} keys.", nameof(keys));
        }
        var slots = new byte[Slots];
        for (var i = 0; i < keys.Count; i++)
        {
            slots[i] = (byte)keys[i];
        }
        return slots;
    }
}
