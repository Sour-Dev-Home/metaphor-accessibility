using System.Buffers.Binary;
using System.Collections.Concurrent;
using Metaphor.Core.Navigation;

namespace Metaphor.Core.Tests.Navigation;

/// <summary>Naming a map's <c>.NAV</c> file from the map table (#741): row <c>id</c> of <c>MapData.bin</c>, its
/// <c>u16</c>s at <c>+0x8</c> and <c>+0xa</c> (docs/findings/field-navmesh-file-from-map-data.md). Made-up tables
/// built here; no game data.</summary>
[Trait("Category", "Unit")]
public sealed class NavMapFilesTests
{
    private readonly ConcurrentQueue<string> lines = new();

    // A MapData table: "MAPD", the row count at +8, then 0x2c-byte rows from +0x10, each with its file's numbers.
    private static byte[] Table(params (int A, int B)[] rows)
    {
        var bytes = new byte[NavMapFiles.HeaderSize + (rows.Length * NavMapFiles.RowSize)];
        "MAPD"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = bytes.AsSpan(NavMapFiles.HeaderSize + (i * NavMapFiles.RowSize));
            row.Fill(0xff);
            BinaryPrimitives.WriteUInt16LittleEndian(row[8..], (ushort)rows[i].A);
            BinaryPrimitives.WriteUInt16LittleEndian(row[10..], (ushort)rows[i].B);
        }
        return bytes;
    }

    [Fact]
    public void AMapsFileIsItsRowsTwoNumbersNotItsIdSplit()
    {
        var files = NavMapFiles.Parse(Table((0xffff, 0xffff), (1, 1), (150, 2), (178, 41)))!;

        Assert.Equal(4, files.Count);
        Assert.Equal((1, 1), files.FileOf(1));
        Assert.Equal((150, 2), files.FileOf(2));
        Assert.Equal((178, 41), files.FileOf(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ARowWithoutAFileOrPastTheTableNamesNone(int mapId) =>
        Assert.Null(NavMapFiles.Parse(Table((0xffff, 0xffff), (1, 1), (2, 0xffff), (0xffff, 3)))!.FileOf(mapId));

    [Fact]
    public void ATableThatIsntOneIsRefused()
    {
        var good = Table((1, 1), (2, 2));
        var renamed = (byte[])good.Clone();
        renamed[0] = (byte)'X';
        var counted = (byte[])good.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(counted.AsSpan(8), 3);
        var negative = (byte[])good.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(negative.AsSpan(8), -1);

        Assert.NotNull(NavMapFiles.Parse(good));
        Assert.Null(NavMapFiles.Parse(renamed));
        Assert.Null(NavMapFiles.Parse(counted));
        Assert.Null(NavMapFiles.Parse(negative));
        Assert.Null(NavMapFiles.Parse(good[..8]));
    }

    [Fact]
    public void TheArchivesTableIsReadOnceAndLogged()
    {
        var table = Table((0xffff, 0xffff), (1, 1), (150, 2));
        var opens = 0;
        var fileOf = NavMapFiles.FromArchive(() =>
        {
            Interlocked.Increment(ref opens);
            return CpkArchiveTests.Archive(("COMMON/init/data", "MapData.bin", table, table.Length));
        }, lines.Enqueue);

        Assert.Equal((150, 2), fileOf(2));
        Assert.Equal((1, 1), fileOf(1));
        Assert.Null(fileOf(0));
        Assert.Equal(1, opens);
        Assert.Equal(["navmesh: MapData.bin: 3 maps"], lines);
    }

    [Fact]
    public void AMissingOrBrokenTableNamesNoFileAndSaysWhy()
    {
        var missing = NavMapFiles.FromArchive(() => CpkArchiveTests.Archive(("COMMON/init/data", "Other.bin", [1], 1)),
            lines.Enqueue);
        var broken = NavMapFiles.FromArchive(() => CpkArchiveTests.Archive(("COMMON/init/data", "MapData.bin", [1], 1)),
            lines.Enqueue);
        var locked = NavMapFiles.FromArchive(() => throw new IOException("locked"), lines.Enqueue);

        Assert.Null(missing(1));
        Assert.Null(broken(1));
        Assert.Null(locked(1));
        Assert.Null(locked(1));
        Assert.Equal(["navmesh: MapData.bin: not in the archive", "navmesh: MapData.bin: doesn't parse",
            "navmesh: can't read MapData.bin: IOException"], lines);
    }
}
