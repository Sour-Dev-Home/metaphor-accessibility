using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Logging;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field;

/// <summary>The field's tables from the game's archive (#742, #743), read offline on game build 548ddc95… (Steam build
/// 18330018) (docs/findings/field-map-and-objective-tables-static.md). <c>MapData.bin</c> (<c>MAPD</c>, the count at
/// <c>+0x8</c>, <c>0x2c</c>-byte rows from <c>+0x10</c>, one per map id) holds each map's own name as its label's MD5
/// at row <c>+0x1c</c>. <c>Mission_Field_Data.bin</c> (<c>MSN_F</c>, <c>0x24</c>-byte rows) holds each field
/// objective's label MD5 at <c>+0x0</c> and the story flag that starts it as the <c>u16</c> at <c>+0x20</c>, the flags
/// the main missions start with. Hashes are 32 upper-case hex digits, as
/// <see cref="MessageLibrary.FindByLabelHash"/> takes them; the empty label's is left out.</summary>
public sealed class FieldTables
{
    /// <summary>Where the game keeps the map table in its archive.</summary>
    public const string MapDataPath = "COMMON/init/data/MapData.bin";

    /// <summary>Where the game keeps the field objective table in its archive.</summary>
    public const string ObjectivesPath = "COMMON/mission/source/bin/Mission_Field_Data.bin";

    /// <summary>The largest table read: the game's are 26,680 and 3,472 bytes.</summary>
    public const int MaxSize = 1024 * 1024;

    /// <summary>The size of one map row.</summary>
    public const int MapRowSize = 0x2c;

    /// <summary>The size of one objective row.</summary>
    public const int ObjectiveRowSize = 0x24;

    private const int RowsStart = 0x10;
    private const int MapNameAt = 0x1c;
    private const int ObjectiveFlagAt = 0x20;
    private const int HashSize = 16;

    private static readonly string EmptyLabel = MessageLibrary.LabelHash("");

    private readonly IReadOnlyList<string?>? maps;

    private FieldTables(IReadOnlyList<string?>? maps, IReadOnlyList<MainMissionRow>? objectives)
    {
        this.maps = maps;
        Objectives = objectives;
    }

    /// <summary>The field objectives, or null when their table couldn't be read.</summary>
    public IReadOnlyList<MainMissionRow>? Objectives { get; }

    /// <summary>The label hash of map <paramref name="map"/>'s own name, or null when the table hasn't one.</summary>
    public string? MapNameHash(int map) => maps is not null && map >= 0 && map < maps.Count ? maps[map] : null;

    /// <summary>What was read, for the log.</summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"field tables: {(maps is null ? "no maps" : $"{maps.Count} maps")}, {(Objectives is null ? "no objectives" : $"{Objectives.Count} objectives")}");

    /// <summary>The tables from the files <paramref name="file"/> gives by archive path (null when it has none).</summary>
    public static FieldTables From(Func<string, byte[]?> file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new(
            file(MapDataPath) is { } map ? ParseMaps(map) : null,
            file(ObjectivesPath) is { } objectives ? ParseObjectives(objectives) : null);
    }

    /// <summary>Each map id's own name hash (null for an empty one), or null when <paramref name="bytes"/> aren't the
    /// map table.</summary>
    public static IReadOnlyList<string?>? ParseMaps(ReadOnlySpan<byte> bytes)
    {
        if (Count(bytes, "MAPD"u8, MapRowSize) is not { } count)
        {
            return null;
        }
        var names = new string?[count];
        for (var i = 0; i < count; i++)
        {
            var hash = Convert.ToHexString(bytes.Slice(RowsStart + i * MapRowSize + MapNameAt, HashSize));
            names[i] = hash == EmptyLabel ? null : hash;
        }
        return names;
    }

    /// <summary>Each field objective's flag and label hash, or null when <paramref name="bytes"/> aren't the objective
    /// table.</summary>
    public static IReadOnlyList<MainMissionRow>? ParseObjectives(ReadOnlySpan<byte> bytes)
    {
        if (Count(bytes, "MSN_F"u8, ObjectiveRowSize) is not { } count)
        {
            return null;
        }
        var rows = new MainMissionRow[count];
        for (var i = 0; i < count; i++)
        {
            var row = bytes.Slice(RowsStart + i * ObjectiveRowSize, ObjectiveRowSize);
            rows[i] = new(BinaryPrimitives.ReadUInt16LittleEndian(row[ObjectiveFlagAt..]), Convert.ToHexString(row[..HashSize]));
        }
        return rows;
    }

    // The row count, or null when the bytes don't start with the magic or are shorter than the count says.
    private static int? Count(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> magic, int rowSize)
    {
        if (bytes.Length < RowsStart || !bytes.StartsWith(magic))
        {
            return null;
        }
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);
        return count >= 0 && count <= (bytes.Length - RowsStart) / rowSize ? count : null;
    }
}

/// <summary>The field's tables once they have loaded (#742, #743), read off the game's thread so a key press never
/// waits on the archive. Empty until then, and when they couldn't be read.</summary>
public sealed class FieldTablesSource
{
    private volatile FieldTables? tables;

    /// <summary>The label hash of map <paramref name="map"/>'s own name, or null when it isn't known.</summary>
    public string? MapNameHash(int map) => tables?.MapNameHash(map);

    /// <summary>The field objectives, or null while they load or when they couldn't be read.</summary>
    public IReadOnlyList<MainMissionRow>? Objectives => tables?.Objectives;

    /// <summary>Reads the tables from the archive <paramref name="open"/> opens, on the thread pool, and tells
    /// <paramref name="done"/> what came of it: a line for the log naming no folder.</summary>
    public Task LoadInBackground(Func<Stream> open, Action<string> done)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(done);
        return Task.Run(() => done(Load(open)));
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves the tables empty; it must not reach the game.")]
    private string Load(Func<Stream> open)
    {
        var started = StepTimer.Start();
        try
        {
            using var archive = open();
            var read = FieldTables.From(path => CpkArchive.ReadFile(archive, path, FieldTables.MaxSize));
            tables = read;
            return $"{read.Describe()} {StepTimer.Took(started)}";
        }
        catch (Exception e)
        {
            return $"field tables: can't read the archive: {e.GetType().Name}";
        }
    }
}
