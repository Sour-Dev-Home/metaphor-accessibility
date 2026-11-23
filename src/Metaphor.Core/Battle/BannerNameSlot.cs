using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core.Battle;

/// <summary>A Debug build's probe of a battle banner's name (#739): message variable slot 0, the banner's <c>&lt;V 0&gt;</c>,
/// as the show is called, for the message log. A slot is 0x28 bytes; its first <c>u32</c> is a value (a serial under
/// tag 2) and its byte at <c>+0x20</c> the tag: 1 an int, 2 a message serial, 3 a string
/// (docs/findings/battle-banner-name-variable-static.md, build 548ddc95…, Steam build 18330018). Nothing reads it for
/// players.</summary>
public static class BannerNameSlot
{
    /// <summary>Where the tag byte sits in a slot.</summary>
    public const int TagAt = 0x20;

    /// <summary>How many bytes of a slot the probe reads: up to and with its tag.</summary>
    public const int Read = TagAt + 1;

    /// <summary>The slot as the log line ends: " slot0=0x97 tag=2", or " slot0=unread" when fewer than
    /// <see cref="Read"/> bytes were read.</summary>
    public static string Describe(ReadOnlySpan<byte> slot) => slot.Length < Read
        ? " slot0=unread"
        : string.Create(CultureInfo.InvariantCulture,
            $" slot0=0x{BinaryPrimitives.ReadUInt32LittleEndian(slot):x} tag={slot[TagAt]}");
}
