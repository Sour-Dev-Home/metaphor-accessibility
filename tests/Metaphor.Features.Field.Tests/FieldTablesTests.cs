using System.Text;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field.Tests;

/// <summary>The field's tables from the game's archive (#742, #743): <c>MapData.bin</c> names each map by its label's
/// MD5 at row <c>+0x1c</c>, and <c>Mission_Field_Data.bin</c> gives each objective's label MD5 and its flag. Made-up
/// labels, maps and flags only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldTablesTests
{
    private static byte[] Hash(string label) => Convert.FromHexString(MessageLibrary.LabelHash(label));

    private static byte[] Header(string magic, int count)
    {
        var header = new byte[0x10];
        Encoding.ASCII.GetBytes(magic).CopyTo(header, 0);
        BitConverter.GetBytes(count).CopyTo(header, 8);
        return header;
    }

    private static byte[] MapRow(string label)
    {
        var row = new byte[FieldTables.MapRowSize];
        Array.Fill(row, (byte)0xff, 0, 0x1c);
        Hash(label).CopyTo(row, 0x1c);
        return row;
    }

    private static byte[] ObjectiveRow(string label, int flag, int high = 0)
    {
        var row = new byte[FieldTables.ObjectiveRowSize];
        Hash(label).CopyTo(row, 0);
        Hash("").CopyTo(row, 0x10);
        BitConverter.GetBytes((ushort)flag).CopyTo(row, 0x20);
        BitConverter.GetBytes((ushort)high).CopyTo(row, 0x22);
        return row;
    }

    [Fact]
    public void EachMapIsNamedByTheLabelInItsRow()
    {
        byte[] bytes = [.. Header("MAPD", 3), .. MapRow(""), .. MapRow("Map_Test_01"), .. MapRow("Map_Test_02")];

        var maps = FieldTables.ParseMaps(bytes);

        Assert.NotNull(maps);
        Assert.Null(maps[0]);
        Assert.Equal(MessageLibrary.LabelHash("Map_Test_01"), maps[1]);
        Assert.Equal(MessageLibrary.LabelHash("Map_Test_02"), maps[2]);
    }

    [Fact]
    public void EachObjectiveHasItsFlagAndLabel()
    {
        byte[] bytes =
            [.. Header("MSN_F", 2), .. ObjectiveRow("MissionText_Test_01", 0x1166), .. ObjectiveRow("MissionText_Test_02", 0x11fd, high: 3)];

        Assert.Equal(
        [
            new MainMissionRow(0x1166, MessageLibrary.LabelHash("MissionText_Test_01")),
            new MainMissionRow(0x11fd, MessageLibrary.LabelHash("MissionText_Test_02")),
        ], FieldTables.ParseObjectives(bytes));
    }

    [Fact]
    public void BytesThatArentTheTablesGiveNull()
    {
        Assert.Null(FieldTables.ParseMaps(Header("MSN_F", 0)));
        Assert.Null(FieldTables.ParseMaps([.. Header("MAPD", 2), .. MapRow("Map_Test_01")]));
        Assert.Null(FieldTables.ParseObjectives(Header("MAPD", 0)));
        Assert.Null(FieldTables.ParseObjectives([.. Header("MSN_F", 2), .. ObjectiveRow("MissionText_Test_01", 0x1166)]));
        Assert.Null(FieldTables.ParseMaps(new byte[4]));
    }

    [Fact]
    public void TheTablesComeFromTheirArchivePathsAndEitherMayBeMissing()
    {
        var files = new Dictionary<string, byte[]>
        {
            [FieldTables.MapDataPath] = [.. Header("MAPD", 2), .. MapRow(""), .. MapRow("Map_Test_01")],
        };

        var tables = FieldTables.From(path => files.GetValueOrDefault(path));

        Assert.Equal(MessageLibrary.LabelHash("Map_Test_01"), tables.MapNameHash(1));
        Assert.Null(tables.MapNameHash(0));
        Assert.Null(tables.MapNameHash(2));
        Assert.Null(tables.MapNameHash(-1));
        Assert.Null(tables.Objectives);
        Assert.Equal("field tables: 2 maps, no objectives", tables.Describe());
    }

    // #652: the same row's file numbers name the map's field, f_<a>_<b>, for its icon points.
    [Fact]
    public void EachMapsFieldComesFromItsRowsFileNumbers()
    {
        var row = MapRow("Map_Test_01");
        BitConverter.GetBytes((ushort)1).CopyTo(row, 0x8);
        BitConverter.GetBytes((ushort)3).CopyTo(row, 0xa);
        var files = new Dictionary<string, byte[]> { [FieldTables.MapDataPath] = [.. Header("MAPD", 2), .. MapRow(""), .. row] };

        var tables = FieldTables.From(path => files.GetValueOrDefault(path));

        Assert.Equal((1, 3), tables.FileOf(1));
        Assert.Null(tables.FileOf(0));
        Assert.Null(tables.FileOf(2));
        Assert.Null(new FieldTablesSource().FileOf(1));
    }

    [Fact]
    public async Task ALoadSaysWhatItReadAndHowLongItTook()
    {
        string? said = null;
        await new FieldTablesSource().LoadInBackground(() => new MemoryStream(), line => said = line);

        // Timed, as a slow step (#824).
        Assert.Matches("^field tables: no maps, no objectives in [0-9]+ ms$", said);
    }

    [Fact]
    public async Task AnArchiveThatCantBeReadLeavesTheSourceEmptyAndSaysWhy()
    {
        var source = new FieldTablesSource();
        Assert.Null(source.MapNameHash(1));
        Assert.Null(source.Objectives);

        string? said = null;
        await source.LoadInBackground(() => throw new IOException("stand-in"), line => said = line);

        Assert.Equal("field tables: can't read the archive: IOException", said);
        Assert.Null(source.MapNameHash(1));
    }

    [Fact]
    public async Task AFailedLoadGoesToTheErrorSinkNotTheOrdinaryOne()
    {
        string? said = null;
        string? error = null;

        await new FieldTablesSource().LoadInBackground(
            () => throw new IOException("stand-in"), line => said = line, line => error = line);

        Assert.Null(said);
        Assert.Equal("field tables: can't read the archive: IOException", error);
    }

    [Fact]
    public async Task ALoadThatFindsNoTablesIsAFailedLoadToo()
    {
        string? said = null;
        string? error = null;

        await new FieldTablesSource().LoadInBackground(() => new MemoryStream(), line => said = line, line => error = line);

        Assert.Null(said);
        Assert.Matches("^field tables: no maps, no objectives in [0-9]+ ms$", error);
    }

    [Fact]
    public async Task TheMapsAreLoadedOnlyOnceTheTableHasBeenRead()
    {
        var source = new FieldTablesSource();
        Assert.False(source.MapsLoaded);

        await source.LoadInBackground(() => new MemoryStream(), _ => { });

        Assert.False(source.MapsLoaded); // read, but it held no map table
    }
}
