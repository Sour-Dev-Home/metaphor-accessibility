using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Logging;

namespace Metaphor.Core.Navigation;

/// <summary>Which <c>f_&lt;a&gt;_&lt;b&gt;.NAV</c> file holds a map's navmesh (#741), from the game's map table
/// <c>COMMON/init/data/MapData.bin</c> in <c>base.cpk</c>. On game build 548ddc95… (Steam build 18330018),
/// docs/findings/field-navmesh-file-from-map-data.md: magic <c>MAPD</c>, the row count at <c>+0x8</c>, then
/// <see cref="RowSize"/>-byte rows from <see cref="HeaderSize"/>, row <c>id</c> for map id <c>id</c>; a row's
/// <c>u16</c>s at <c>+0x8</c> and <c>+0xa</c> are <c>a</c> and <c>b</c>, <c>0xffff</c> for none. Every map id the
/// sessions logged (1, 2, 12, 138, 285, 371, 372) names a file the archive has; that the game's own navmesh is the one
/// it names is [NEEDS VERIFICATION] by ear, which <see cref="NavRoutePlanner"/>'s log of the player's cell checks.
/// Immutable, so thread-safe.</summary>
public sealed class NavMapFiles
{
    /// <summary>Where the table is in the archive.</summary>
    public const string ArchivePath = "COMMON/init/data/MapData.bin";

    /// <summary>The bytes before the first row.</summary>
    public const int HeaderSize = 0x10;

    /// <summary>One row's bytes.</summary>
    public const int RowSize = 0x2c;

    /// <summary>The largest table read (the game's is 26,680 bytes).</summary>
    public const int MaxFileSize = 1024 * 1024;

    private const int FileA = 0x8;
    private const int FileB = 0xa;
    private const ushort None = 0xffff;

    private readonly byte[] bytes;

    private NavMapFiles(byte[] bytes, int count)
    {
        this.bytes = bytes;
        Count = count;
    }

    /// <summary>How many maps the table has rows for.</summary>
    public int Count { get; }

    /// <summary>The table in <paramref name="bytes"/>, or null when it isn't one: no <c>MAPD</c> magic, or fewer bytes
    /// than its row count needs.</summary>
    public static NavMapFiles? Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < HeaderSize || !bytes.AsSpan().StartsWith("MAPD"u8))
        {
            return null;
        }
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
        return count >= 0 && count <= (bytes.Length - HeaderSize) / RowSize ? new((byte[])bytes.Clone(), count) : null;
    }

    /// <summary>Map <paramref name="mapId"/>'s file numbers, or null when the table has no row for it or the row names
    /// no file.</summary>
    public (int A, int B)? FileOf(int mapId)
    {
        if (mapId < 0 || mapId >= Count)
        {
            return null;
        }
        var row = bytes.AsSpan(HeaderSize + (mapId * RowSize), RowSize);
        var a = BinaryPrimitives.ReadUInt16LittleEndian(row[FileA..]);
        var b = BinaryPrimitives.ReadUInt16LittleEndian(row[FileB..]);
        return a == None || b == None ? null : (a, b);
    }

    /// <summary>A lookup that reads the table from the archive <paramref name="open"/> gives on its first call, off the
    /// game's thread as the planner is, and keeps it: a table that couldn't be read names no file for any map, and
    /// <paramref name="log"/> gets one line saying how many maps were read or why none were.</summary>
    public static Func<int, (int A, int B)?> FromArchive(Func<Stream> open, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(log);
        var table = new Lazy<NavMapFiles?>(() => Read(open, log), LazyThreadSafetyMode.ExecutionAndPublication);
        return id => table.Value?.FileOf(id);
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves every map without a file; it must not reach the game.")]
    private static NavMapFiles? Read(Func<Stream> open, Action<string> log)
    {
        const string name = "MapData.bin";
        var started = StepTimer.Start();
        try
        {
            using var archive = open();
            var found = CpkArchive.Lookup(archive, ArchivePath, MaxFileSize);
            if (found.Bytes is null || Parse(found.Bytes) is not { } files)
            {
                log($"navmesh: {name}: {NavMeshSource.Why(found.Status)}");
                return null;
            }
            log(string.Create(CultureInfo.InvariantCulture, $"navmesh: {name}: {files.Count} maps {StepTimer.Took(started)}"));
            return files;
        }
        catch (Exception e)
        {
            log($"navmesh: can't read {name}: {e.GetType().Name}");
            return null;
        }
    }
}
