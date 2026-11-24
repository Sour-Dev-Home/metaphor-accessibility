namespace Metaphor.Features.Field.Tests;

/// <summary>A field map's icon-point file (#652): a <c>0x14</c>-byte header with the count at <c>+0xc</c> and the first
/// entry at <c>+0x10</c>, then <c>0x34</c>-byte entries with a <c>u16</c> position, a kind and an index. Made-up bytes
/// only, never the game's files.</summary>
[Trait("Category", "Unit")]
public sealed class MapIconPointsTests
{
    internal static byte[] File(params (int X, int Y, int Kind, int Index)[] points) => File(0x14, points);

    internal static byte[] File(int first, params (int X, int Y, int Kind, int Index)[] points)
    {
        var bytes = new byte[first + (points.Length * MapIconPoints.EntrySize)];
        BitConverter.GetBytes(1).CopyTo(bytes, 0);
        BitConverter.GetBytes(1).CopyTo(bytes, 4);
        BitConverter.GetBytes(points.Length).CopyTo(bytes, 0xc);
        BitConverter.GetBytes(first).CopyTo(bytes, 0x10);
        for (var i = 0; i < points.Length; i++)
        {
            var at = first + (i * MapIconPoints.EntrySize);
            BitConverter.GetBytes(1).CopyTo(bytes, at);
            BitConverter.GetBytes((ushort)points[i].X).CopyTo(bytes, at + 4);
            BitConverter.GetBytes((ushort)points[i].Y).CopyTo(bytes, at + 6);
            BitConverter.GetBytes(points[i].Kind).CopyTo(bytes, at + 0xc);
            BitConverter.GetBytes(points[i].Index).CopyTo(bytes, at + 0x10);
            "Made_Up_Name"u8.ToArray().CopyTo(bytes, at + 0x14);
        }
        return bytes;
    }

    [Fact]
    public void ParseReadsEachEntrysPositionKindAndIndex()
    {
        var points = MapIconPoints.Parse(File((1478, 1025, 4, 9), (60000, 94, 5, 0)));

        Assert.Equal([new MapIconPoint(1478, 1025, 4, 9), new MapIconPoint(60000, 94, 5, 0)], points);
    }

    [Fact]
    public void ParseStartsAtTheHeadersFirstEntryOffset()
    {
        var points = MapIconPoints.Parse(File(0x20, (7, 8, 4, 2)));

        Assert.Equal([new MapIconPoint(7, 8, 4, 2)], points);
    }

    [Fact]
    public void AnEmptyFileHasNoPoints() => Assert.Empty(MapIconPoints.Parse(File())!);

    [Fact]
    public void ParseRefusesWhatIsntAnIconPointFile()
    {
        Assert.Null(MapIconPoints.Parse(new byte[0x13]));
        Assert.Null(MapIconPoints.Parse(File(0x10, (1, 2, 4, 3))));
        var shortened = File((1, 2, 4, 3));
        Assert.Null(MapIconPoints.Parse(shortened.AsSpan(0, shortened.Length - 1)));
        var huge = File();
        BitConverter.GetBytes(uint.MaxValue).CopyTo(huge, 0xc);
        Assert.Null(MapIconPoints.Parse(huge));
        var past = File();
        BitConverter.GetBytes(0x1000).CopyTo(past, 0x10);
        Assert.Null(MapIconPoints.Parse(past));
    }

    [Fact]
    public void PathsAndNamesFollowTheExesFormat()
    {
        Assert.Equal("COMMON/field/source/layout/map_icon_point/mip_0001_002_000.dat", MapIconPoints.ArchivePath(1, 2, 0));
        Assert.Equal("COMMON/field/source/layout/map_icon_point/mip_0030_001_001.dat", MapIconPoints.ArchivePath(30, 1, 1));
        Assert.Equal("f_0153_002", MapIconPoints.FieldName(153, 2));
    }

    [Fact]
    public void DescribeWritesNumbersOnly()
    {
        Assert.Equal("(1478,1025 k=4 i=9) (7,8 k=5 i=0)",
            MapIconPoints.Describe([new(1478, 1025, 4, 9), new(7, 8, 5, 0)]));
        Assert.Equal("none", MapIconPoints.Describe([]));
    }

    [Fact]
    public void TheSourceReadsAFieldOnceAndSaysWhetherItHasAVariant()
    {
        var reads = new List<string>();
        var lines = new List<string>();
        var source = new MapIconPointSource(path =>
        {
            reads.Add(path);
            return path.EndsWith("_000.dat", StringComparison.Ordinal) ? File((1, 2, 4, 3)) :
                path.Contains("0001_001_001", StringComparison.Ordinal) ? File() : null;
        }, lines.Add, run: Inline);

        var first = source.For(1, 1);
        var again = source.For(1, 1);
        var other = source.For(1, 2);

        Assert.Equal([new MapIconPoint(1, 2, 4, 3)], first!.Points);
        Assert.True(first.HasVariant);
        Assert.Same(first, again);
        Assert.False(other!.HasVariant);
        Assert.Equal(4, reads.Count);
        Assert.Contains(lines, line => line.StartsWith("objective: f_0001_001: 1 icon points, variant 1 exists", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldWithNoFileHasNoPointsAndAnUnreadableOneNone()
    {
        var lines = new List<string>();
        var source = new MapIconPointSource(
            path => path.Contains("0002_", StringComparison.Ordinal) ? [1, 2, 3] : null, lines.Add, run: Inline);

        Assert.Empty(source.For(1, 1)!.Points);
        Assert.Null(source.For(2, 1));
        Assert.Contains("objective: f_0002_001: icon points unreadable", lines);
    }

    [Fact]
    public void AReadStillRunningIsNullAndLoading()
    {
        var gate = new TaskCompletionSource<FieldIconPoints?>();
        var source = new MapIconPointSource(_ => File(), _ => { }, run: _ => gate.Task);

        Assert.False(source.Loading(1, 1));
        Assert.Null(source.For(1, 1));
        Assert.True(source.Loading(1, 1));

        gate.SetResult(new FieldIconPoints([], false));

        Assert.False(source.Loading(1, 1));
        Assert.NotNull(source.For(1, 1));
    }

    [Fact]
    public void AReadThatThrowsIsLoggedAndLeavesTheFieldWithout()
    {
        var lines = new List<string>();
        var source = new MapIconPointSource(_ => throw new IOException("locked"), lines.Add, run: Inline);

        Assert.Null(source.For(1, 1));
        Assert.Contains("objective: f_0001_001: can't read the archive: IOException", lines);
    }

    internal static Task<FieldIconPoints?> Inline(Func<FieldIconPoints?> load) => Task.FromResult(load());
}
