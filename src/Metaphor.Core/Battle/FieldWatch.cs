using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core.Battle;

// Issue #162: what the development-only battle listener decodes, kept here so it is tested without the game. The
// battle objects it reads have no known layout yet (docs/raw-sources/battle-state-leads-2026-11-17.md), so the listener
// logs which 32-bit fields change and a capture session ties them to what was on screen. Numbers only, no game text.

/// <summary>One 32-bit field that changed between two reads of an object: its offset and both values.</summary>
public readonly record struct FieldChange(int Offset, int Old, int New);

/// <summary>What changed in an object since its last read: nothing on the first read, and the fields muted by this
/// read.</summary>
public sealed record FieldDiff(bool First, IReadOnlyList<FieldChange> Changes, IReadOnlyList<int> NewlyMuted);

/// <summary>Compares each read of an object with the last read under the same key, field by field (4-byte aligned
/// <c>int</c>s). Only fields that hold a small integer before and after count (<see cref="SmallFields.IsSmall"/>):
/// floats and pointers change all the time and say little. A field that changes on <c>muteAfter</c> reads in a row is a
/// timer or a counter; it is muted for that key and reported once. Not thread-safe: one hook on one thread.</summary>
public sealed class FieldWatch(int muteAfter = 8, int maxKeys = 256)
{
    private sealed class Seen(byte[] bytes)
    {
        public byte[] Bytes { get; set; } = bytes;

        public Dictionary<int, int> Streaks { get; } = [];

        public HashSet<int> Muted { get; } = [];
    }

    private readonly Dictionary<string, Seen> seen = [];

    /// <summary>The changes in <paramref name="snapshot"/> since the last one read under <paramref name="key"/>.</summary>
    public FieldDiff Compare(string key, ReadOnlySpan<byte> snapshot)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!seen.TryGetValue(key, out var last))
        {
            if (seen.Count >= maxKeys)
            {
                seen.Clear();
            }
            seen[key] = new Seen(snapshot.ToArray());
            return new FieldDiff(true, [], []);
        }
        var changes = new List<FieldChange>();
        var muted = new List<int>();
        var length = Math.Min(snapshot.Length, last.Bytes.Length) & ~3;
        for (var offset = 0; offset < length; offset += 4)
        {
            var old = BinaryPrimitives.ReadInt32LittleEndian(last.Bytes.AsSpan(offset));
            var now = BinaryPrimitives.ReadInt32LittleEndian(snapshot[offset..]);
            if (old == now)
            {
                last.Streaks.Remove(offset);
                continue;
            }
            var streak = last.Streaks[offset] = last.Streaks.GetValueOrDefault(offset) + 1;
            if (last.Muted.Contains(offset))
            {
                continue;
            }
            if (streak >= muteAfter)
            {
                last.Muted.Add(offset);
                muted.Add(offset);
            }
            else if (SmallFields.IsSmall(old) && SmallFields.IsSmall(now))
            {
                changes.Add(new FieldChange(offset, old, now));
            }
        }
        last.Bytes = snapshot.ToArray();
        return new FieldDiff(false, changes, muted);
    }

    /// <summary>Drops what was read under <paramref name="key"/>, so its next read counts as a first.</summary>
    public void Forget(string key) => seen.Remove(key);

    /// <summary>Drops every key, as when a new battle starts.</summary>
    public void Clear() => seen.Clear();
}

/// <summary>Reads 32-bit and 64-bit fields of an object whose layout is unknown, and writes them in the log's form.
/// </summary>
public static class SmallFields
{
    /// <summary>The largest magnitude a field may hold to count as a small integer: enough for HP, EXP and money, and far
    /// below a float's bit pattern (1.0f is 0x3F800000) or an address.</summary>
    public const int Limit = 10_000_000;

    /// <summary>True when <paramref name="value"/> looks like a count, an ID or a flag rather than a float or an
    /// address.</summary>
    public static bool IsSmall(int value) => value is >= -Limit and <= Limit;

    /// <summary>The non-zero small fields of <paramref name="snapshot"/>, by offset, at most <paramref name="max"/>.
    /// </summary>
    public static IReadOnlyList<(int Offset, int Value)> NonZero(ReadOnlySpan<byte> snapshot, int max)
    {
        var values = new List<(int, int)>();
        for (var offset = 0; offset + 4 <= snapshot.Length && values.Count < max; offset += 4)
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(snapshot[offset..]);
            if (value != 0 && IsSmall(value))
            {
                values.Add((offset, value));
            }
        }
        return values;
    }

    /// <summary>The 8-byte aligned fields of <paramref name="snapshot"/> that hold one of <paramref name="targets"/>.
    /// </summary>
    public static IReadOnlyList<(int Offset, long Target)> PointersTo(ReadOnlySpan<byte> snapshot, IReadOnlySet<long> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var found = new List<(int, long)>();
        for (var offset = 0; offset + 8 <= snapshot.Length; offset += 8)
        {
            var value = BinaryPrimitives.ReadInt64LittleEndian(snapshot[offset..]);
            if (targets.Contains(value))
            {
                found.Add((offset, value));
            }
        }
        return found;
    }

    /// <summary>The log's form of changes: <c>+0x10:4&gt;3,…</c>, or <c>none</c>.</summary>
    public static string Describe(IReadOnlyList<FieldChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return changes.Count == 0
            ? "none"
            : string.Join(',', changes.Select(c => string.Create(CultureInfo.InvariantCulture, $"+0x{c.Offset:x}:{c.Old}>{c.New}")));
    }

    /// <summary>The log's form of values: <c>+0x4=5,…</c>, or <c>none</c>.</summary>
    public static string Describe(IReadOnlyList<(int Offset, int Value)> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count == 0
            ? "none"
            : string.Join(',', values.Select(v => string.Create(CultureInfo.InvariantCulture, $"+0x{v.Offset:x}={v.Value}")));
    }
}
