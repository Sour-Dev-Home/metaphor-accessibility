using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Metaphor.Core.Battle;

/// <summary>What a battle banner's <c>&lt;V 0&gt;</c> holds (#739): a message serial, or a name's text.</summary>
public readonly record struct BannerName(int? Serial, string? Text);

/// <summary>Message variable slot 0, a battle banner's <c>&lt;V 0&gt;</c>, as the show is called (#739). A slot is 0x28
/// bytes; its first <c>u32</c> is a value (a serial under tag 2) and its byte at <c>+0x20</c> the tag: 1 an int, 2 a
/// message serial, 3 a string, an MSVC <c>std::string</c> whose size is at <c>+0x10</c> and capacity at <c>+0x18</c>,
/// its text inline below a capacity of 16, else behind a pointer at <c>+0</c>
/// (docs/findings/battle-banner-name-variable-static.md, build 548ddc95…, Steam build 18330018). The banner queue puts
/// a party member's or enemy's name serial there; the serials' files are in
/// docs/findings/battle-banner-enemy-name-serials-static.md.</summary>
public static class BannerNameSlot
{
    /// <summary>Where the tag byte sits in a slot.</summary>
    public const int TagAt = 0x20;

    /// <summary>How many bytes the log's probe needs: up to and with the tag.</summary>
    public const int Read = TagAt + 1;

    /// <summary>A whole slot's length.</summary>
    public const int Length = 0x28;

    /// <summary>The longest tag 3 text taken as a name, in bytes; a longer size is a stale or wrong slot.</summary>
    public const int LongestName = 64;

    private const byte SerialTag = 2;
    private const byte StringTag = 3;
    private const int SizeAt = 0x10;
    private const int CapacityAt = 0x18;
    private const int Inline = 16;

    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The slot as the log line ends: " slot0=0x97 tag=2", or " slot0=unread" when fewer than
    /// <see cref="Read"/> bytes were read.</summary>
    public static string Describe(ReadOnlySpan<byte> slot) => slot.Length < Read
        ? " slot0=unread"
        : string.Create(CultureInfo.InvariantCulture,
            $" slot0=0x{BinaryPrimitives.ReadUInt32LittleEndian(slot):x} tag={slot[TagAt]}");

    /// <summary>The name a whole slot holds: its serial under tag 2, its text under tag 3, read through
    /// <paramref name="readAt"/> (address, length; null when the read failed) when it isn't inline. Null for any other
    /// tag, a short read, or a string of an impossible size or not UTF-8. That the text is UTF-8 is
    /// <c>[NEEDS VERIFICATION]</c>.</summary>
    public static BannerName? Name(ReadOnlySpan<byte> slot, Func<long, int, byte[]?> readAt)
    {
        ArgumentNullException.ThrowIfNull(readAt);
        if (slot.Length < Length)
        {
            return null;
        }
        switch (slot[TagAt])
        {
            case SerialTag:
                return new BannerName((int)BinaryPrimitives.ReadUInt32LittleEndian(slot), null);
            case StringTag:
                var size = BinaryPrimitives.ReadInt64LittleEndian(slot[SizeAt..]);
                var capacity = BinaryPrimitives.ReadInt64LittleEndian(slot[CapacityAt..]);
                if (size is <= 0 or > LongestName || size > capacity)
                {
                    return null;
                }
                var bytes = capacity < Inline
                    ? slot[..(int)size].ToArray()
                    : readAt(BinaryPrimitives.ReadInt64LittleEndian(slot), (int)size);
                return bytes is { } read && read.Length == size && Text(read) is { } text
                    ? new BannerName(null, text)
                    : null;
            default:
                return null;
        }
    }

    private static string? Text(byte[] bytes)
    {
        try
        {
            var text = Strict.GetString(bytes);
            return text.Any(char.IsControl) || string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
