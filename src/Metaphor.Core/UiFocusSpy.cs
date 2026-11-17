using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Metaphor.Core;

// Issue #93, second sitting: what the development-only UI focus listeners decode, kept here so it is tested without
// the game. The field offsets are our reading of the exe on one build and are leads until a run shows them
// (docs/raw-sources/ui-focus-candidates-2026-11-16.md).

/// <summary>A UI text pane's draw state (#93), from the first <see cref="Length"/> bytes of the pane: its class (vtable),
/// two 32-bit fields at <c>+0x08</c> and <c>+0x0C</c> (flags, read by the draw), the floats at <c>+0x30</c>,
/// <c>+0x34</c>, <c>+0x38</c> and the 32-bit value at <c>+0x3C</c> the draw passes on (position, then a float and an
/// integer handed to the text element's draw), the 8-byte colour at <c>+0x48</c> and whether it is set (<c>+0x50</c>),
/// and the text element at <c>+0x58</c>. What each field means is a lead, not a finding.</summary>
public sealed record PaneDrawn(
    long Pane, long? ClassRva, int Field08, int Flags, float X, float Y, float Field38, int Field3C, long Colour,
    bool ColourSet, long Element)
{
    /// <summary>How many bytes of the pane <see cref="Read"/> needs.</summary>
    public const int Length = 0x60;

    /// <summary>The draw state in <paramref name="pane"/>'s first <see cref="Length"/> bytes, or null when fewer were
    /// given.</summary>
    public static PaneDrawn? Read(long pane, long moduleBase, long moduleSize, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length)
        {
            return null;
        }
        var vtable = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        return new(
            pane,
            vtable >= moduleBase && vtable - moduleBase < moduleSize ? vtable - moduleBase : null,
            BinaryPrimitives.ReadInt32LittleEndian(bytes[0x08..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[0x0C..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[0x30..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[0x34..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[0x38..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[0x3C..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[0x48..]),
            bytes[0x50] != 0,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[0x58..]));
    }

    /// <summary>The bytes whose change makes a pane worth logging again: the ones this record holds, not the rest of the
    /// pane (which may hold timers).</summary>
    public static byte[] StateKey(ReadOnlySpan<byte> bytes) =>
        bytes.Length < Length ? [] : [.. bytes[0x08..0x10], .. bytes[0x30..0x40], .. bytes[0x48..0x60]];

    /// <summary>The log's form: <c>at=0x… class=0x… x=… y=… f38=… i3c=0x… colour=0x…(set) n08=0x… flags=0x… elem=0x…</c>.
    /// </summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"at=0x{Pane:x} class={UiFocusText.Hex(ClassRva)} x={X} y={Y} f38={Field38} i3c=0x{Field3C:x8} colour=0x{Colour:x16}({(ColourSet ? "set" : "unset")}) n08=0x{Field08:x} flags=0x{Flags:x} elem=0x{Element:x}");
}

/// <summary>Remembers the last state logged for each key (an object's address) and says whether a new one differs, so a
/// listener on a per-frame function logs only changes. Safe to call from any thread.</summary>
public sealed class ChangeGate
{
    /// <summary>The most keys remembered; past it the gate forgets them all and starts again.</summary>
    public const int MaxKeys = 4096;

    private readonly object gate = new();
    private readonly Dictionary<long, byte[]> last = [];

    /// <summary>True when <paramref name="state"/> differs from the one last let through for <paramref name="key"/>
    /// (or there was none), and it is now the one remembered.</summary>
    public bool Changed(long key, ReadOnlySpan<byte> state)
    {
        lock (gate)
        {
            if (last.TryGetValue(key, out var previous) && state.SequenceEqual(previous))
            {
                return false;
            }
            if (last.Count >= MaxKeys)
            {
                last.Clear();
            }
            last[key] = state.ToArray();
            return true;
        }
    }
}

/// <summary>Looks for readable text behind an object whose layout is unknown (#93): the first pointer field whose target
/// reads as a NUL-terminated string of printable characters. A guess to find the layout with, for a local development
/// log only; the text is the game's and is never committed.</summary>
public static class TextProbe
{
    /// <summary>The shortest run of characters taken as text.</summary>
    public const int MinLength = 2;

    // User-mode addresses on x64 Windows end below 0x7FFF'FFFF'0000; the first 64 KiB are never mapped.
    // https://learn.microsoft.com/windows/win32/memory/virtual-address-space
    private const long LowestAddress = 0x10000;
    private const long HighestAddress = 0x7FFF_FFFE_FFFF;

    /// <summary>The offset of the first 8-byte field of <paramref name="fields"/> that points at text, and the text read
    /// as UTF-8, or null. <paramref name="readLatin1"/> reads a NUL-terminated string at an address byte for byte
    /// (null when unreadable).</summary>
    public static (int Offset, string Text)? FirstString(ReadOnlySpan<byte> fields, Func<long, string?> readLatin1)
    {
        ArgumentNullException.ThrowIfNull(readLatin1);
        for (var at = 0; at + sizeof(long) <= fields.Length; at += sizeof(long))
        {
            var pointer = BinaryPrimitives.ReadInt64LittleEndian(fields[at..]);
            if (pointer is < LowestAddress or > HighestAddress)
            {
                continue;
            }
            if (readLatin1(pointer) is { Length: >= MinLength } raw && raw.All(c => c >= ' ' && c != 0x7F) &&
                raw.Any(char.IsLetter))
            {
                return (at, Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(raw)));
            }
        }
        return null;
    }
}

/// <summary>Number formatting shared by the focus records.</summary>
internal static class UiFocusText
{
    public static string Hex(long? value) =>
        value is { } v ? string.Create(CultureInfo.InvariantCulture, $"0x{v:x}") : "?";

    public static string Hex(int? value) =>
        value is { } v ? string.Create(CultureInfo.InvariantCulture, $"0x{v:x}") : "?";

    public static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";
}
