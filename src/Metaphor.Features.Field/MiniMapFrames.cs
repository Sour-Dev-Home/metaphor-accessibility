using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Logging;

namespace Metaphor.Features.Field;

/// <summary>One minimap record of <c>MiniMap_Data.bin</c> (#652): an icon point <c>(px, py)</c> is at world
/// <c>x = OriginX − Scale × px</c>, <c>z = OriginZ − Scale × py</c>.</summary>
/// <param name="Record">Its id.</param>
/// <param name="OriginX">World <c>x</c> of point 0 across.</param>
/// <param name="OriginZ">World <c>z</c> of point 0 down.</param>
/// <param name="Scale">World units per point.</param>
public readonly record struct MiniMapFrame(int Record, float OriginX, float OriginZ, float Scale)
{
    /// <summary>World <c>x, z</c> of icon point <paramref name="x"/>, <paramref name="y"/>.</summary>
    public (float X, float Z) ToWorld(int x, int y) =>
        ((float)(OriginX - ((double)Scale * x)), (float)(OriginZ - ((double)Scale * y)));
}

/// <summary>One floor of a map's <c>MiniMap_SetData.bin</c> row: its record and its height.</summary>
/// <param name="Frame">The floor's record.</param>
/// <param name="Height">The row's height for the floor.</param>
public readonly record struct MiniMapFloor(MiniMapFrame Frame, float Height);

/// <summary>Each map's minimap frames (#652), read offline from the player's own <c>base.cpk</c> on game build
/// 548ddc95… (Steam build 18330018) (docs/findings/minimap-data-converts-map-points-to-world.md). Both tables have the
/// game's <c>ui</c> table header: <c>u64</c> version 2, row count, field count and row size, then a <c>u64</c> per
/// field, the rows after it. <c>MiniMap_Data.bin</c>'s 14-byte rows are a <c>u16</c> id and <c>f32</c>
/// <c>Ox, Oz, s</c>; <c>MiniMap_SetData.bin</c>'s 62-byte rows a <c>u16</c> map id, ten <c>u16</c> record ids (0 for an
/// unused slot) and ten <c>f32</c> heights. A record of <c>(0, 0, 1)</c> is a map with no minimap and is left out.
/// </summary>
public sealed class MiniMapFrames
{
    /// <summary>Where the game keeps the records.</summary>
    public const string DataPath = "COMMON/ui/map/MiniMap_Data.bin";

    /// <summary>Where the game keeps each map's floors.</summary>
    public const string SetDataPath = "COMMON/ui/map/MiniMap_SetData.bin";

    /// <summary>The largest table read: the game's are 21,064 and 18,800 bytes.</summary>
    public const int MaxSize = 256 * 1024;

    /// <summary>One record row's size.</summary>
    public const int DataRowSize = 14;

    /// <summary>One map row's size.</summary>
    public const int SetRowSize = 62;

    /// <summary>The floor slots in a map row.</summary>
    public const int Slots = 10;

    private const ulong Version = 2;
    private const int HeaderSize = 32;

    private readonly Dictionary<int, MiniMapFloor[]> maps;

    private MiniMapFrames(Dictionary<int, MiniMapFloor[]> maps) => this.maps = maps;

    /// <summary>How many maps have a frame.</summary>
    public int Count => maps.Count;

    /// <summary>Map <paramref name="map"/>'s floors in slot order, or empty when it has no minimap.</summary>
    public IReadOnlyList<MiniMapFloor> Floors(int map) => maps.TryGetValue(map, out var floors) ? floors : [];

    /// <summary>Whether map <paramref name="map"/>'s floors have different records' values (maps 14, 15 and 114 in the
    /// game's tables).</summary>
    public bool FloorsDiffer(int map)
    {
        var floors = Floors(map);
        return floors.Any(floor => !SameValues(floor.Frame, floors[0].Frame));
    }

    /// <summary>Map <paramref name="map"/>'s frame for a player at height <paramref name="height"/>, or null when it
    /// has no minimap. The floor is the one with the highest height at or below the player's, else the lowest; with no
    /// height, the first. That a height picks the floor so is [NEEDS VERIFICATION] (the finding's "Floors"); it only
    /// matters where the floors differ.</summary>
    public MiniMapFrame? FrameFor(int map, float? height)
    {
        var floors = Floors(map);
        if (floors.Count == 0)
        {
            return null;
        }
        if (height is not { } y || floors.Count == 1)
        {
            return floors[0].Frame;
        }
        var below = floors.Where(floor => floor.Height <= y).ToList();
        return (below.Count > 0 ? below.MaxBy(floor => floor.Height) : floors.MinBy(floor => floor.Height)).Frame;
    }

    /// <summary>What was read, for the log.</summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture, $"minimap frames: {Count} maps");

    /// <summary>The frames in the two tables, or null when either isn't its table: another version or row size, or a
    /// length that isn't the header and its rows exactly.</summary>
    public static MiniMapFrames? Parse(ReadOnlySpan<byte> setData, ReadOnlySpan<byte> data)
    {
        if (Rows(data, DataRowSize) is not { } records || Rows(setData, SetRowSize) is not { } sets)
        {
            return null;
        }
        var frames = new Dictionary<int, MiniMapFrame>();
        for (var i = 0; i < records.Count; i++)
        {
            var row = data.Slice(records.Start + (i * DataRowSize), DataRowSize);
            var id = BinaryPrimitives.ReadUInt16LittleEndian(row);
            var frame = new MiniMapFrame(id, BinaryPrimitives.ReadSingleLittleEndian(row[2..]),
                BinaryPrimitives.ReadSingleLittleEndian(row[6..]), BinaryPrimitives.ReadSingleLittleEndian(row[10..]));
            if (float.IsFinite(frame.OriginX) && float.IsFinite(frame.OriginZ) && float.IsFinite(frame.Scale) &&
                frame.Scale != 0 && !(frame.OriginX == 0 && frame.OriginZ == 0 && frame.Scale == 1))
            {
                frames.TryAdd(id, frame);
            }
        }
        var maps = new Dictionary<int, MiniMapFloor[]>();
        for (var i = 0; i < sets.Count; i++)
        {
            var row = setData.Slice(sets.Start + (i * SetRowSize), SetRowSize);
            var floors = new List<MiniMapFloor>();
            for (var slot = 0; slot < Slots; slot++)
            {
                var record = BinaryPrimitives.ReadUInt16LittleEndian(row[(2 + (2 * slot))..]);
                if (record != 0 && frames.TryGetValue(record, out var frame))
                {
                    floors.Add(new(frame, BinaryPrimitives.ReadSingleLittleEndian(row[(2 + (2 * Slots) + (4 * slot))..])));
                }
            }
            if (floors.Count > 0)
            {
                maps.TryAdd(BinaryPrimitives.ReadUInt16LittleEndian(row), [.. floors]);
            }
        }
        return new(maps);
    }

    private static bool SameValues(MiniMapFrame a, MiniMapFrame b) =>
        a.OriginX == b.OriginX && a.OriginZ == b.OriginZ && a.Scale == b.Scale;

    // The row count and where the rows start, or null when the header isn't the table's.
    private static (int Count, int Start)? Rows(ReadOnlySpan<byte> bytes, int rowSize)
    {
        if (bytes.Length < HeaderSize || bytes.Length > MaxSize ||
            BinaryPrimitives.ReadUInt64LittleEndian(bytes) != Version ||
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]) != (ulong)rowSize)
        {
            return null;
        }
        var count = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        var fields = BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]);
        if (fields > (ulong)(bytes.Length - HeaderSize) / 8)
        {
            return null;
        }
        var start = HeaderSize + (8 * (int)fields);
        return count == (ulong)((bytes.Length - start) / rowSize) && (bytes.Length - start) % rowSize == 0
            ? ((int)count, start)
            : null;
    }
}

/// <summary>Where on which map a point is converted (#652).</summary>
/// <param name="Map">The map id, as <c>MapData.bin</c> numbers it.</param>
/// <param name="Field">Its field, <c>f_0001_001</c>.</param>
/// <param name="Height">The player's world <c>y</c>, or null when it isn't known.</param>
public readonly record struct MapSpot(int Map, string Field, float? Height);

/// <summary>The minimap frames as the default map-to-world conversion (#652), read from the archive on the thread pool
/// once, on the first ask; until then, and when they couldn't be read, no map converts. Thread-safe.</summary>
public sealed class MiniMapFrameSource : IMapToWorld
{
    private readonly Func<(byte[]? SetData, byte[]? Data)> read;
    private readonly Action<string> log;
    private readonly Func<Action, Task> run;
    private readonly Lock gate = new();
    private volatile MiniMapFrames? frames;
    private Task? load;

    /// <summary>A source that reads both tables through <paramref name="read"/> (null for one the archive hasn't),
    /// writes what came of it to <paramref name="log"/>, a line naming no folder, and runs the read with
    /// <paramref name="run"/> (on the thread pool unless given).</summary>
    public MiniMapFrameSource(
        Func<(byte[]? SetData, byte[]? Data)> read, Action<string> log, Func<Action, Task>? run = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(log);
        this.read = read;
        this.log = log;
        this.run = run ?? Task.Run;
    }

    /// <summary>A source over the archive <paramref name="open"/> opens read-only, once.</summary>
    public static MiniMapFrameSource FromArchive(Func<Stream> open, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(open);
        return new(() =>
        {
            using var archive = open();
            return (CpkArchive.ReadFile(archive, MiniMapFrames.SetDataPath, MiniMapFrames.MaxSize),
                CpkArchive.ReadFile(archive, MiniMapFrames.DataPath, MiniMapFrames.MaxSize));
        }, log);
    }

    /// <summary>The frames once read, or null.</summary>
    public MiniMapFrames? Frames => frames;

    /// <inheritdoc/>
    public bool Loading => Start() is { IsCompleted: false };

    /// <inheritdoc/>
    public (float X, float Z)? ToWorld(MapSpot spot, int x, int y)
    {
        Start();
        return frames?.FrameFor(spot.Map, spot.Height)?.ToWorld(x, y);
    }

    /// <inheritdoc/>
    public string Describe(MapSpot spot)
    {
        if (Loading)
        {
            return "loading";
        }
        if (frames?.FrameFor(spot.Map, spot.Height) is not { } frame)
        {
            return frames is null ? "unread" : "none";
        }
        var floors = frames.Floors(spot.Map).Count;
        return string.Create(CultureInfo.InvariantCulture,
            $"minimap:{frame.Record}({frame.OriginX:0.#},{frame.OriginZ:0.#},{frame.Scale:0.#}){(frames.FloorsDiffer(spot.Map) ? $"floors={floors}differ" : "")}");
    }

    private Task Start()
    {
        lock (gate)
        {
            return load ??= run(Load);
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves no frames; it must not reach the game.")]
    private void Load()
    {
        var started = StepTimer.Start();
        try
        {
            var (setData, data) = read();
            if (setData is null || data is null)
            {
                log($"minimap frames: set table {(setData is null ? "missing" : "read")}, record table {(data is null ? "missing" : "read")}");
                return;
            }
            if (MiniMapFrames.Parse(setData, data) is not { } parsed)
            {
                log(string.Create(CultureInfo.InvariantCulture,
                    $"minimap frames: tables of {setData.Length} and {data.Length} bytes aren't the minimap tables"));
                return;
            }
            frames = parsed;
            log($"{parsed.Describe()} {StepTimer.Took(started)}");
        }
        catch (Exception e)
        {
            log($"minimap frames: can't read the archive: {e.GetType().Name}");
        }
    }
}
