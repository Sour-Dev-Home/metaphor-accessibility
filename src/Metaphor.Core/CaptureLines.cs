using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Metaphor.Core.Settings;

namespace Metaphor.Core;

/// <summary>The capture build's message-log lines: Debug-only probes that log what a by-ear session needs to pin a
/// blocked issue's read (#693, #949, #950, #702, #930, #809, #253, #199). Numbers and class names only, no game text.
/// Each line starts with its probe's name, which the session checklist names. Debug builds only: no Release assembly
/// calls this type (<c>Rules.DebugOnlyTypes</c> in the architecture tests). Every offset is a static lead on game build
/// 548ddc95… (Steam build 18330018) and <c>[NEEDS VERIFICATION]</c> until a session reads it; each one's source is
/// cited where the hook layer reads it (<c>CaptureProbes</c>).</summary>
public static class CaptureLines
{
    /// <summary>Zone 1 of a field enemy's sight on its controller: origin, forward and axis vectors (16 bytes each),
    /// then radius and angle floats, <c>C+0x1c0</c> to <c>C+0x1f8</c> (docs/findings/field-enemy-sight-zones-static.md).
    /// </summary>
    public const int ZoneStart = 0x1c0;

    /// <summary>How many bytes of the zone <see cref="Zone"/> reads.</summary>
    public const int ZoneLength = 0x38;

    /// <summary>The start of a chest's block the opened-state diff covers (docs/findings/field-units-names-exits-static.md,
    /// stop 3).</summary>
    public const int ChestBlockStart = 0x2880;

    /// <summary>How many bytes of the chest's block are diffed.</summary>
    public const int ChestBlockLength = 0x50;

    /// <summary>The follower rank array's size: 23 followers, an <c>int</c> each
    /// (docs/findings/money-follower-rank-and-shop-static.md).</summary>
    public const int FollowerRanksLength = 23 * 4;

    // The pad buttons a press line names: Start (#809), Back, the triggers (RT for #263), and the stick clicks (R3 for
    // Auto Battle #257, L3 for Retry #262). Bits from BindingNames.
    private static readonly string[] Watched = ["Start", "Back", "LT", "RT", "LS", "RS"];

    // The game's key list holds Windows virtual-key codes; Enter is VK_RETURN (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).
    private const int EnterCode = 0x0D;

    /// <summary>#693: zone 1 of the nearest enemy, its distance from the player and its AI state as
    /// <c>FieldEnemyState</c> describes it.</summary>
    public static string Zone(long enemy, double distance, string ai, ReadOnlySpan<byte> zone)
    {
        if (zone.Length < ZoneLength)
        {
            return Text($"zone enemy=0x{enemy:x} dist={distance:F2} zone=? {ai}");
        }
        var (origin, forward, axis) = (Vector(zone, 0x00), Vector(zone, 0x10), Vector(zone, 0x20));
        var (radius, angle) = (Float(zone, 0x30), Float(zone, 0x34));
        return Text(
            $"zone enemy=0x{enemy:x} dist={distance:F2} origin={origin} forward={forward} axis={axis} radius={radius:F2} angle={angle:F4} {ai}");
    }

    /// <summary>#949, #950: one pickup (<c>fldGmcGather</c>), its database row, its link and its taken bit.</summary>
    public static string Pickup(long unit, float x, float z, int? row, int? link, int? bit, bool? taken) =>
        Text($"pickup unit=0x{unit:x} at={x:F0},{z:F0} row={Number(row)} link={Number(link)} bit={Number(bit)} taken={Flag(taken)}");

    private static string Flag(bool? value) => value is { } set ? set ? "1" : "0" : "?";

    /// <summary>Bit <paramref name="bit"/> mod 8 of a flag bank byte, as the game's bank set and test (<c>0x70cf20</c>,
    /// <c>0x70d3f0</c>) address it: byte <c>n &gt;&gt; 3</c>, bit <c>n &amp; 7</c>.</summary>
    public static bool BitSet(byte value, int bit) => (value >> (bit & 7) & 1) != 0;

    /// <summary>#950: the nearest chest's block, whole the first time (<paramref name="before"/> empty), then only the
    /// bytes that changed, as <c>+offset:old&gt;new</c>.</summary>
    public static string Chest(long unit, int? layout, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var head = Text($"chest unit=0x{unit:x} layout={Number(layout)}");
        if (before.Length != after.Length)
        {
            return head + " block=" + Convert.ToHexString(after).ToLowerInvariant();
        }
        var changed = new StringBuilder();
        for (var i = 0; i < after.Length; i++)
        {
            if (before[i] != after[i])
            {
                changed.Append(changed.Length == 0 ? "" : ",").Append(
                    Text($"+0x{ChestBlockStart + i:x}:{before[i]:x2}>{after[i]:x2}"));
            }
        }
        return head + " changed=" + (changed.Length == 0 ? "-" : changed.ToString());
    }

    /// <summary>#702: the player's lock words as <c>IFieldReads.PlayerLocks</c> words them, free roam and the map.</summary>
    public static string Locks(string? locks, bool freeRoam, int? map) =>
        Text($"locks {locks ?? "lock34CC=?"} freeRoam={(freeRoam ? 1 : 0)} map={Number(map)}");

    /// <summary>#809, #263, #257, #262: the watched pad buttons that went down between two reads, or null for none.</summary>
    public static string? Presses(int pad, int before, int after)
    {
        var pressed = Watched.Where(name => BindingNames.Buttons(name) is { } bit && (after & bit) != 0 && (before & bit) == 0)
            .ToList();
        return pressed.Count == 0 ? null : Text($"press pad{pad} {string.Join(',', pressed)}");
    }

    /// <summary>#930: the name entry keyboard object's event bits (<c>+0xa0</c>, read by <c>vf20</c>;
    /// docs/findings/name-entry-static.md).</summary>
    public static string NameEntryEvents(long keyboard, int? bits) =>
        Text($"name-entry-events keyboard=0x{keyboard:x} bits={(bits is { } value ? Text($"0x{value:x8}") : "?")}");

    /// <summary>#930: whether Enter is in the key list, said without the other keys, which may spell the name (#585).</summary>
    public static string Enter(IReadOnlyList<int> keys) => keys.Contains(EnterCode) ? "enter=held" : "enter=-";

    /// <summary>#253: a text pane's element and its class, once per element.</summary>
    public static string PaneOwner(long element, string? className) =>
        Text($"pane-owner elem=0x{element:x} class={className ?? "?"}");

    /// <summary>#199: each follower's rank, in the array's order.</summary>
    public static string Ranks(ReadOnlySpan<byte> ranks)
    {
        var values = new List<string>();
        for (var at = 0; at + 4 <= ranks.Length; at += 4)
        {
            values.Add(BinaryPrimitives.ReadInt32LittleEndian(ranks[at..]).ToString(CultureInfo.InvariantCulture));
        }
        return "follower-ranks " + string.Join(',', values);
    }

    private static string Vector(ReadOnlySpan<byte> bytes, int at) =>
        Text($"{Float(bytes, at):F2},{Float(bytes, at + 4):F2},{Float(bytes, at + 8):F2}");

    private static float Float(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
