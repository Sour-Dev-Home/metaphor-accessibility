using System.Text;

namespace Metaphor.Core;

/// <summary>Reads strings out of the game's memory through a read function that fails instead of faulting, such as the
/// hook layer's <c>ModuleMemoryReader</c>. The read function takes addresses here, not RVAs.</summary>
public static class MemoryText
{
    private const int AnsiPiece = 32;
    private const int Utf8Piece = 1 << 20;

    /// <summary>Reads a NUL-terminated string at <paramref name="address"/> as Latin-1, up to
    /// <paramref name="maxBytes"/>, in small pieces so a string near the end of readable memory still reads: what was
    /// read before unreadable memory is returned. Null when its first byte can't be read.</summary>
    public static string? ReadAnsi(ChunkedScan.ReadAt read, long address, int maxBytes = 512)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var bytes = new byte[maxBytes];
        var length = 0;
        while (length < maxBytes)
        {
            var piece = bytes.AsSpan(length, Math.Min(AnsiPiece, maxBytes - length));
            if (!read(address + length, piece))
            {
                // The piece crosses into unreadable memory; finish byte by byte up to the terminator.
                piece = bytes.AsSpan(length, 1);
                if (!read(address + length, piece))
                {
                    return length == 0 ? null : Encoding.Latin1.GetString(bytes, 0, length);
                }
            }
            var end = piece.IndexOf((byte)0);
            if (end >= 0)
            {
                length += end;
                break;
            }
            length += piece.Length;
        }
        return Encoding.Latin1.GetString(bytes, 0, length);
    }

    /// <summary>Reads <paramref name="length"/> bytes at <paramref name="address"/> as UTF-8, or null when the length
    /// is negative or over <paramref name="maxBytes"/>, or the bytes can't all be read.</summary>
    public static string? ReadUtf8(ChunkedScan.ReadAt read, long address, int length, int maxBytes = 16 << 20)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (length < 0 || length > maxBytes)
        {
            return null;
        }
        var bytes = new byte[length];
        for (var offset = 0; offset < length; offset += Utf8Piece)
        {
            if (!read(address + offset, bytes.AsSpan(offset, Math.Min(Utf8Piece, length - offset))))
            {
                return null;
            }
        }
        return Encoding.UTF8.GetString(bytes);
    }
}
