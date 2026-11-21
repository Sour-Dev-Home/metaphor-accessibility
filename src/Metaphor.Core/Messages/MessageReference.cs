using System.Buffers.Binary;

namespace Metaphor.Core.Messages;

/// <summary>A message as a UI panel is handed it (#545): the <paramref name="File"/>'s entry in the game's message-file
/// table and the message's <paramref name="Position"/> in that file, the pair the game resolves a label to.</summary>
/// <remarks>On game build 548ddc95… (Steam build 18330018) the reference is <see cref="Length"/> bytes, the pair first;
/// that the pair is (table entry, position) is read from the exe, not yet seen in play
/// (docs/findings/travel-map-destination-text-static.md). What the other bytes hold is not known.</remarks>
public sealed record MessageReference(int File, int Position)
{
    /// <summary>How many bytes a reference takes.</summary>
    public const int Length = 0x1c;

    /// <summary>The reference in the first bytes of <paramref name="bytes"/>, or null when fewer than
    /// <see cref="Length"/> were given or either number is unset (negative).</summary>
    public static MessageReference? Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length)
        {
            return null;
        }
        var file = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var position = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        return file < 0 || position < 0 ? null : new(file, position);
    }
}

/// <summary>Finds the message a <see cref="MessageReference"/> names.</summary>
public interface IMessageByReference
{
    /// <summary>The message <paramref name="reference"/> names, or null when no known file holds it.</summary>
    GameMessage? Find(MessageReference reference);
}
