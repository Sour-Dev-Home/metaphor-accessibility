using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core;

/// <summary>#625's probe: one call of the archetype learn function <c>LearnClass</c> (<c>0x389c70</c>, a whole
/// <c>.ecode</c> function, not a jump stub), as a message-log line. On game build 548ddc95… (Steam build 18330018), by
/// Ghidra 12.2 (<c>disasm 0x389c70</c>, 2026-11-24): <c>ecx</c> is the member, whose unit <c>0x3b0eb0</c> returns;
/// <c>rdx</c> the class record, its class ID the <c>int</c> at <c>+0x4</c>; the unit's archetype slots are
/// <see cref="SlotCount"/> slots of <see cref="SlotSize"/> bytes from <c>+0x98</c>, a slot used when bit 0 of its first
/// byte is set, its class ID the <c>int</c> at <c>+0x4</c>. Live values <c>[NEEDS VERIFICATION]</c>.</summary>
public static class LearnClassCall
{
    /// <summary>The class ID's offset in the class record.</summary>
    public const int ClassIdOffset = 0x4;

    /// <summary>The first archetype slot's offset in the member's unit.</summary>
    public const int SlotsOffset = 0x98;

    /// <summary>One slot's size.</summary>
    public const int SlotSize = 0x5c;

    /// <summary>A slot's class ID offset.</summary>
    public const int SlotClassOffset = 0x4;

    /// <summary>How many slots the learn function scans.</summary>
    public const int SlotCount = 0x3c;

    /// <summary>How many bytes of the unit the slots take.</summary>
    public const int SlotsLength = SlotCount * SlotSize;

    /// <summary>Whether a used slot in <paramref name="slots"/> already holds <paramref name="classId"/>, as the learn
    /// function's first loop tests it before learning; null when fewer than <see cref="SlotsLength"/> bytes were
    /// read.</summary>
    public static bool? Known(ReadOnlySpan<byte> slots, int classId)
    {
        if (slots.Length < SlotsLength)
        {
            return null;
        }
        for (var at = 0; at < SlotsLength; at += SlotSize)
        {
            if ((slots[at] & 1) != 0 && BinaryPrimitives.ReadInt32LittleEndian(slots[(at + SlotClassOffset)..]) == classId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The call as <c>learn-class member=2 record=0x… class=17 known=no</c>, each part <c>?</c> when
    /// unreadable.</summary>
    public static string Line(int member, long record, int? classId, bool? known) => string.Create(
        CultureInfo.InvariantCulture,
        $"learn-class member={member} record=0x{record:x} class={(classId is { } id ? id.ToString(CultureInfo.InvariantCulture) : "?")} known={(known is { } k ? k ? "yes" : "no" : "?")}");
}
