using System.Buffers.Binary;

namespace Metaphor.Features.Field.Tests;

/// <summary>The minimap tables (#652): a map's row names its records, one per floor, and a record converts an icon
/// point to world <c>x = Ox − s·px</c>, <c>z = Oz − s·py</c>. Made-up tables only, in the game's <c>ui</c> table layout.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MiniMapFramesTests
{
    /// <summary>A table: the header (version 2, rows, fields, row size, one offset per field), then the rows.</summary>
    internal static byte[] Table(int rowSize, int fields, IReadOnlyList<byte[]> rows, ulong version = 2)
    {
        var start = 32 + (8 * fields);
        var bytes = new byte[start + (rows.Count * rowSize)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, version);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), (ulong)rows.Count);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), (ulong)fields);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), (ulong)rowSize);
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].CopyTo(bytes, start + (i * rowSize));
        }
        return bytes;
    }

    /// <summary>The record table holding <paramref name="records"/>.</summary>
    internal static byte[] Data(params (int Id, float Ox, float Oz, float S)[] records) =>
        Table(MiniMapFrames.DataRowSize, 4, records.Select(record =>
        {
            var row = new byte[MiniMapFrames.DataRowSize];
            BinaryPrimitives.WriteUInt16LittleEndian(row, (ushort)record.Id);
            BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(2), record.Ox);
            BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(6), record.Oz);
            BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(10), record.S);
            return row;
        }).ToList());

    /// <summary>The map table holding <paramref name="maps"/>, each map's records and heights by slot; the rest are
    /// record 0, height −1.</summary>
    internal static byte[] Sets(params (int Map, (int Record, float Height)[] Floors)[] maps) =>
        Table(MiniMapFrames.SetRowSize, 21, maps.Select(map =>
        {
            var row = new byte[MiniMapFrames.SetRowSize];
            BinaryPrimitives.WriteUInt16LittleEndian(row, (ushort)map.Map);
            for (var slot = 0; slot < MiniMapFrames.Slots; slot++)
            {
                var (record, height) = slot < map.Floors.Length ? map.Floors[slot] : (0, -1f);
                BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(2 + (2 * slot)), (ushort)record);
                BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(22 + (4 * slot)), height);
            }
            return row;
        }).ToList());

    private static MiniMapFrames Frames(byte[] sets, byte[] data) => MiniMapFrames.Parse(sets, data)!;

    [Fact]
    public void AMapsRecordConvertsItsPointsToWorld()
    {
        var frames = Frames(Sets((1, [(1, -1030)])), Data((0, 0, 0, 1), (1, 18807, 21899, 18)));

        var frame = frames.FrameFor(1, height: 24)!.Value;

        Assert.Equal((-3f, 16589f), frame.ToWorld(1045, 295));
        Assert.Equal(new MiniMapFrame(1, 18807, 21899, 18), frame);
    }

    [Fact]
    public void AMapWithOnlyTheDefaultRecordOrNoRowHasNoFrame()
    {
        var frames = Frames(Sets((2, [(3, -1)])), Data((3, 0, 0, 1)));

        Assert.Null(frames.FrameFor(2, 0));
        Assert.Null(frames.FrameFor(9, 0));
        Assert.Equal(0, frames.Count);
    }

    [Fact]
    public void TheHeightPicksTheHighestFloorAtOrBelowThePlayer()
    {
        var frames = Frames(
            Sets((12, [(56, -3500), (57, -600)])), Data((56, 100, 200, 10), (57, 300, 400, 20)));

        Assert.Equal(56, frames.FrameFor(12, -2000)!.Value.Record);
        Assert.Equal(57, frames.FrameFor(12, 0)!.Value.Record);
        Assert.Equal(56, frames.FrameFor(12, -9000)!.Value.Record);
        Assert.Equal(56, frames.FrameFor(12, null)!.Value.Record);
        Assert.True(frames.FloorsDiffer(12));
    }

    [Fact]
    public void FloorsWithTheSameValuesDontDiffer()
    {
        var frames = Frames(Sets((12, [(56, -3500), (57, -600)])), Data((56, 1, 2, 3), (57, 1, 2, 3)));

        Assert.False(frames.FloorsDiffer(12));
    }

    [Theory]
    [InlineData(3ul, 0)]
    [InlineData(2ul, 1)]
    [InlineData(2ul, -1)]
    public void ATableWithAnotherVersionOrLengthIsRefused(ulong version, int extra)
    {
        var data = Table(MiniMapFrames.DataRowSize, 4, [new byte[MiniMapFrames.DataRowSize]], version);
        Array.Resize(ref data, data.Length + extra);

        Assert.Null(MiniMapFrames.Parse(Sets(), data));
    }

    [Fact]
    public void ATableWithAnotherRowSizeIsRefused() =>
        Assert.Null(MiniMapFrames.Parse(Data((1, 1, 1, 1)), Data((1, 1, 1, 1))));

    [Fact]
    public void TheSourceReadsOnceOnTheFirstAskAndLogsIt()
    {
        var reads = 0;
        var lines = new List<string>();
        var source = new MiniMapFrameSource(() =>
        {
            reads++;
            return (Sets((1, [(1, 0)])), Data((1, 18807, 21899, 18)));
        }, lines.Add, Inline);
        var spot = new MapSpot(1, "f_0001_001", 0);

        Assert.Equal((-3f, 16589f), source.ToWorld(spot, 1045, 295));
        Assert.Equal((-3f, 16589f), source.ToWorld(spot, 1045, 295));
        Assert.False(source.Loading);

        Assert.Equal(1, reads);
        Assert.StartsWith("minimap frames: 1 maps ", Assert.Single(lines), StringComparison.Ordinal);
        Assert.Equal("minimap:1(18807,21899,18)", source.Describe(spot));
    }

    [Fact]
    public void TheSourceIsLoadingUntilTheReadEnds()
    {
        var gate = new TaskCompletionSource();
        var source = new MiniMapFrameSource(() => (null, null), _ => { }, _ => gate.Task);

        Assert.True(source.Loading);
        Assert.Null(source.ToWorld(new(1, "f_0001_001", 0), 1, 1));
        Assert.Equal("loading", source.Describe(new(1, "f_0001_001", 0)));
    }

    [Fact]
    public void AMissingOrFailedReadConvertsNothingAndSaysWhy()
    {
        var lines = new List<string>();
        var missing = new MiniMapFrameSource(() => (null, Data()), lines.Add, Inline);
        var failed = new MiniMapFrameSource(() => throw new IOException("locked"), lines.Add, Inline);

        Assert.Null(missing.ToWorld(new(1, "f_0001_001", 0), 1, 1));
        Assert.Null(failed.ToWorld(new(1, "f_0001_001", 0), 1, 1));

        Assert.Equal(
            ["minimap frames: set table missing, record table read", "minimap frames: can't read the archive: IOException"],
            lines);
        Assert.Equal("unread", failed.Describe(new(1, "f_0001_001", 0)));
    }

    [Fact]
    public void AFieldInTheOverrideFileWinsOverItsRecord()
    {
        var frames = new MiniMapFrameSource(
            () => (Sets((1, [(1, 0)]), (2, [(2, 0)])), Data((1, 18807, 21899, 18), (2, 100, 100, 1))), _ => { }, Inline);
        var overrides = new FittedMapToWorld(
            new Dictionary<string, MapConversion> { ["f_0001_001"] = new(10, 20, 2, 3) });
        var conversion = new OverriddenMapToWorld(overrides, frames);

        Assert.Equal((12f, 23f), conversion.ToWorld(new(1, "f_0001_001", 0), 1, 1));
        Assert.Equal((99f, 99f), conversion.ToWorld(new(2, "f_0001_002", 0), 1, 1));
        Assert.Equal("override", conversion.Describe(new(1, "f_0001_001", 0)));
        Assert.Equal("minimap:2(100,100,1)", conversion.Describe(new(2, "f_0001_002", 0)));
    }

    internal static Task Inline(Action load)
    {
        load();
        return Task.CompletedTask;
    }
}
