using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core.Menus;

// Issue #249: where a menu cursor's list keeps its item count. Read from the exe on one build
// (docs/raw-sources/capture-2-field-leads-2026-11-17.md): CursorManager's slots 9 and 10 loop over a vector of
// 16-byte items from +0x88 to +0x90 and stop at the 32-bit count at +0x58, testing each item's bytes +8 and +9. What the
// count and the two bytes mean on screen is what the capture session checks.

/// <summary>A cursor manager's list as the #249 probe reads it: the count at <c>+0x58</c>, the length of the item
/// vector at <c>+0x88</c>/<c>+0x90</c>, and how many items have their byte <c>+8</c> or <c>+9</c> set.</summary>
public sealed record CursorListCount(long Manager, int Count, long Items, int? Flagged8, int? Flagged9)
{
    /// <summary>The count's offset in the manager.</summary>
    public const int CountOffset = 0x58;

    /// <summary>The item vector's <c>begin</c> offset; <c>end</c> follows.</summary>
    public const int ItemsBeginOffset = 0x88;

    /// <summary>The size of one item in the vector (a shared pointer: object, then control block).</summary>
    public const int ItemSize = 0x10;

    /// <summary>How many bytes of the manager <see cref="From"/> needs.</summary>
    public const int ManagerBytes = ItemsBeginOffset + 16;

    /// <summary>The count and the vector's length from the manager's first <see cref="ManagerBytes"/> bytes; null when
    /// fewer were read or the vector's end is before its begin.</summary>
    public static (int Count, long Items)? From(ReadOnlySpan<byte> manager)
    {
        if (manager.Length < ManagerBytes)
        {
            return null;
        }
        var count = BinaryPrimitives.ReadInt32LittleEndian(manager[CountOffset..]);
        var begin = BinaryPrimitives.ReadInt64LittleEndian(manager[ItemsBeginOffset..]);
        var end = BinaryPrimitives.ReadInt64LittleEndian(manager[(ItemsBeginOffset + 8)..]);
        return end < begin ? null : (count, (end - begin) / ItemSize);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"mgr=0x{Manager:x} count={Count} items={Items} flag8={Flagged8?.ToString(CultureInfo.InvariantCulture) ?? "?"} flag9={Flagged9?.ToString(CultureInfo.InvariantCulture) ?? "?"}");
}
