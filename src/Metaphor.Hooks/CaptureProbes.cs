#if DEBUG
using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Metaphor.Features.Field;

namespace Metaphor.Hooks;

/// <summary>The capture build's poll (Debug builds only): every 250 ms it reads what a by-ear session needs to pin
/// blocked issues' reads, and writes a <see cref="CaptureLines"/> line to the message log when one changes. It reads
/// through <see cref="ModuleMemoryReader"/> only; it hooks, calls and writes nothing, so it needs no build check, like
/// <see cref="LiveInspector"/>, and it starts after the hooks are in. Every offset is a static lead on game build
/// 548ddc95… (Steam build 18330018), <c>[NEEDS VERIFICATION]</c> until the session reads it:
/// <list type="bullet">
/// <item>#693: the nearest field enemy's sight zone 1, <c>C+0x1c0</c> to <c>C+0x1f8</c> with <c>C = [enemy+0x3110]</c>
/// (docs/findings/field-enemy-sight-zones-static.md), with its AI state (<see cref="FieldEnemyState"/>).</item>
/// <item>#949, #950: the pickups (<c>fldGmcGather</c>) in the list at manager <c>+0x1d8</c>, each one's row
/// <c>+0x28c0</c>, link <c>+0x28c4</c> and, for link -1, its taken bit: bit <c>u32 row+0xc</c> of the bytes at
/// <c>[[[G+0xac368]+0x1f8]+0xa0]</c>, the row <c>[[fldDatabase+0x107d0+0x8] + n×8]</c>
/// (docs/findings/field-gather-points-and-taken-state-static.md; Ghidra 12.2, decompile <c>0x69b5c0</c>,
/// <c>0x70cf20</c>, <c>0x3b0b20</c>, <c>0x58e2f0</c>, 2026-11-24). And the nearest chest's <c>+0x2880</c> block,
/// whole the first time, then its changed bytes (docs/findings/field-units-names-exits-static.md, stop 3).</item>
/// <item>#702: the player's lock words (<see cref="IFieldReads.PlayerLocks"/>) with free roam and the map, on every
/// change rather than only at the beacon's pause and resume.</item>
/// <item>#199: the follower rank array (<see cref="GameDataRead.FollowerRanks"/>).</item>
/// </list></summary>
internal sealed class CaptureProbes
{
    private const int PollMilliseconds = 250;
    // The pickup list's begin in the unit manager; its end is 8 bytes on (docs/raw-sources/field-actors-2026-11-16.md).
    private const int PickupList = 0x1d8;
    private const int PickupRow = 0x28c0;
    private const int PickupLink = 0x28c4;
    private const int GatherTable = 0x107d0;
    private const int TableRows = 0x8;
    private const int TableDefault = 0x58;
    private const int RowBit = 0xc;
    private const int GatherBank = 0x1f8;
    private const int BankBytes = 0xa0;
    // Bounds that keep a bad read from becoming a far read; not the game's own sizes.
    private const int MaxRow = 0xffff;
    private const int MaxBit = 0x100000;
    // How many pickups a poll logs, nearest first, so their change filter never overflows.
    private const int MaxPickups = InputStateChanges.MaxSources / 2;
    private const int MaxChests = 256;

    private readonly MessageLog log;
    private readonly IFieldReads field;
    private readonly FieldGlobal world;
    private readonly FieldGlobal database;
    private readonly GameDataRead data;
    private readonly InputStateChanges changes = new();
    private readonly InputStateChanges pickups = new();
    private readonly Dictionary<nint, byte[]> chests = [];

    private CaptureProbes(MessageLog log, IFieldReads field, HookServices services)
    {
        this.log = log;
        this.field = field;
        world = services.FieldWorld;
        database = services.FieldDatabase;
        data = services.GameData;
    }

    /// <summary>Starts the poll on a background thread, or returns null when there's no message log or field read.</summary>
    public static CaptureProbes? Start(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Log is not { } log || services.Field is not { } field)
        {
            return null;
        }
        var probes = new CaptureProbes(log, field, services);
        new Thread(probes.Run) { IsBackground = true, Name = "metaphor capture probes" }.Start();
        return probes;
    }

    private void Run()
    {
        while (true)
        {
            Guarded(Locks);
            Guarded(Ranks);
            if (Manager() is { } manager && field.ReadPose() is { } player)
            {
                Guarded(() => Zone(manager, player));
                Guarded(() => Pickups(manager, player));
                Guarded(() => Chest(manager, player));
            }
            Thread.Sleep(PollMilliseconds);
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A probe must never stop the poll or reach the game.")]
    private static void Guarded(Action probe)
    {
        try
        {
            probe();
        }
        catch (Exception)
        {
            // A probe that fails this poll tries again on the next.
        }
    }

    private void Write(string key, string line)
    {
        if (changes.Changed(key, line))
        {
            log.Capture(line);
        }
    }

    private void Locks() => Write("locks", CaptureLines.Locks(field.PlayerLocks(), field.InFreeRoam(), field.MapId()));

    private void Ranks()
    {
        if (data.FollowerRanks() is { } ranks)
        {
            Write("ranks", CaptureLines.Ranks(ranks));
        }
    }

    private void Zone(nint manager, FieldPose player)
    {
        if (Nearest(manager, FieldMemory.EnemyList, player) is not { } nearest)
        {
            return;
        }
        var zone = new byte[CaptureLines.ZoneLength];
        if (ModuleMemoryReader.ReadPointer(nearest.Unit + FieldEnemyState.ControllerOffset) is not { } controller ||
            controller == 0 || !ModuleMemoryReader.ReadBytes(controller + CaptureLines.ZoneStart, zone))
        {
            zone = [];
        }
        var ai = FieldEnemyState.Describe(nearest.Unit, address => ModuleMemoryReader.ReadPointer((nint)address),
            address => ModuleMemoryReader.ReadInt32((nint)address));
        Write("zone", CaptureLines.Zone(nearest.Unit, nearest.Distance, ai, zone));
    }

    private void Pickups(nint manager, FieldPose player)
    {
        var near = Units(manager, PickupList)
            .Select(unit => (Unit: unit, At: ModuleMemoryReader.ReadVector3(unit + FieldMemory.Position)))
            .Where(u => u.At is { } at && Distance(at, player) <= Nearby.MaxRadius)
            .OrderBy(u => Distance(u.At!.Value, player))
            .Take(MaxPickups);
        foreach (var (unit, at) in near)
        {
            var row = ModuleMemoryReader.ReadInt32(unit + PickupRow);
            var link = ModuleMemoryReader.ReadInt32(unit + PickupLink);
            var (bit, taken) = link == -1 && row is { } index ? TakenBit(index) : (null, null);
            var line = CaptureLines.Pickup(unit, at!.Value.X, at.Value.Z, row, link, bit, taken);
            if (pickups.Changed(line[..line.IndexOf(" at=", StringComparison.Ordinal)], line))
            {
                log.Capture(line);
            }
        }
    }

    // The taken bit of gather row n, as 0x69b5c0 sets it, or nulls where a read fails.
    private (int? Bit, bool? Taken) TakenBit(int row)
    {
        if (row is < -1 or > MaxRow || database.Object() is not { } db)
        {
            return (null, null);
        }
        var table = db + GatherTable;
        var rowAddress = row == -1
            ? ModuleMemoryReader.ReadPointer(table + TableDefault)
            : ModuleMemoryReader.ReadPointer(table + TableRows) is { } rows && rows != 0
                ? ModuleMemoryReader.ReadPointer(rows + row * 8)
                : null;
        if (rowAddress is not { } address || address == 0 ||
            ModuleMemoryReader.ReadInt32(address + RowBit) is not { } bit)
        {
            return (null, null);
        }
        var value = new byte[1];
        if (bit is < 0 or > MaxBit || data.FlagBanks() is not { } banks ||
            ModuleMemoryReader.ReadPointer(banks + GatherBank) is not { } bank || bank == 0 ||
            ModuleMemoryReader.ReadPointer(bank + BankBytes) is not { } bytes || bytes == 0 ||
            !ModuleMemoryReader.ReadBytes(bytes + (bit >> 3), value))
        {
            return (bit, null);
        }
        return (bit, CaptureLines.BitSet(value[0], bit));
    }

    private void Chest(nint manager, FieldPose player)
    {
        if (Nearest(manager, FieldMemory.ChestList, player) is not { } nearest)
        {
            return;
        }
        var block = new byte[CaptureLines.ChestBlockLength];
        if (!ModuleMemoryReader.ReadBytes(nearest.Unit + CaptureLines.ChestBlockStart, block))
        {
            return;
        }
        if (chests.TryGetValue(nearest.Unit, out var before) && before.AsSpan().SequenceEqual(block))
        {
            return;
        }
        if (chests.Count >= MaxChests)
        {
            chests.Clear();
        }
        chests[nearest.Unit] = block;
        log.Capture(CaptureLines.Chest(nearest.Unit, ModuleMemoryReader.ReadInt32(nearest.Unit + FieldMemory.GimmickLayout),
            before ?? [], block));
    }

    private nint? Manager() =>
        world.Object() is { } fieldWorld && ModuleMemoryReader.ReadPointer(fieldWorld + FieldMemory.Manager) is { } manager &&
        manager != 0 ? manager : null;

    private static (nint Unit, double Distance)? Nearest(nint manager, int list, FieldPose player)
    {
        (nint Unit, double Distance)? nearest = null;
        foreach (var unit in Units(manager, list))
        {
            if (ModuleMemoryReader.ReadVector3(unit + FieldMemory.Position) is { } at &&
                Distance(at, player) is var distance && (nearest is null || distance < nearest.Value.Distance))
            {
                nearest = (unit, distance);
            }
        }
        return nearest;
    }

    // A list's units: begin at manager+list, end 8 bytes on, as FieldMemory reads its lists.
    private static List<nint> Units(nint manager, int list)
    {
        var units = new List<nint>();
        if (ModuleMemoryReader.ReadPointer(manager + list) is not { } first ||
            ModuleMemoryReader.ReadPointer(manager + list + 8) is not { } end || first == 0 || end < first)
        {
            return units;
        }
        var count = (int)Math.Min((end - first) / 8, FieldMemory.MaxPerList);
        for (var i = 0; i < count; i++)
        {
            if (ModuleMemoryReader.ReadPointer(first + i * 8) is { } unit && unit != 0)
            {
                units.Add(unit);
            }
        }
        return units;
    }

    private static double Distance((float X, float Y, float Z) at, FieldPose player)
    {
        var dx = at.X - player.X;
        var dz = at.Z - player.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
#endif
