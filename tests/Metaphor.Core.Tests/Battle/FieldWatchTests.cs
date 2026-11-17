using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests.Battle;

/// <summary>The #162 spike's change filter over battle objects whose layout is unknown: which 32-bit fields changed
/// between two reads, with timers muted and pointers and floats left out. Made-up bytes only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldWatchTests
{
    private static byte[] Ints(params int[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), values[i]);
        }
        return bytes;
    }

    [Fact]
    public void AFirstReadIsMarkedFirstWithNoChanges()
    {
        var watch = new FieldWatch();

        var diff = watch.Compare("pkg", Ints(1, 2, 3));

        Assert.True(diff.First);
        Assert.Empty(diff.Changes);
    }

    [Fact]
    public void ReportsEachChangedSmallFieldWithItsOffset()
    {
        var watch = new FieldWatch();
        watch.Compare("pkg", Ints(4, 2, 3));

        var diff = watch.Compare("pkg", Ints(3, 2, 0));

        Assert.False(diff.First);
        Assert.Equal([new FieldChange(0, 4, 3), new FieldChange(8, 3, 0)], diff.Changes);
    }

    [Fact]
    public void LeavesOutFieldsThatLookLikeFloatsOrPointers()
    {
        var watch = new FieldWatch();
        watch.Compare("pkg", Ints(0x3F80_0000, 5));

        var diff = watch.Compare("pkg", Ints(0x4000_0000, 6));

        Assert.Equal([new FieldChange(4, 5, 6)], diff.Changes);
    }

    [Fact]
    public void KeepsEachKeyApart()
    {
        var watch = new FieldWatch();
        watch.Compare("a", Ints(1));
        watch.Compare("b", Ints(9));

        Assert.Equal([new FieldChange(0, 1, 2)], watch.Compare("a", Ints(2)).Changes);
    }

    [Fact]
    public void MutesAFieldThatChangesOnEveryReadAndSaysSoOnce()
    {
        var watch = new FieldWatch(muteAfter: 3);
        watch.Compare("pkg", Ints(0, 7));
        watch.Compare("pkg", Ints(1, 7));
        watch.Compare("pkg", Ints(2, 7));

        var muting = watch.Compare("pkg", Ints(3, 7));
        var after = watch.Compare("pkg", Ints(4, 8));

        Assert.Equal([0], muting.NewlyMuted);
        Assert.Empty(muting.Changes);
        Assert.Equal([new FieldChange(4, 7, 8)], after.Changes);
        Assert.Empty(after.NewlyMuted);
    }

    [Fact]
    public void AFieldThatRestsBetweenChangesIsNotMuted()
    {
        var watch = new FieldWatch(muteAfter: 2);
        watch.Compare("pkg", Ints(0));
        watch.Compare("pkg", Ints(1));
        watch.Compare("pkg", Ints(1));

        Assert.Equal([new FieldChange(0, 1, 2)], watch.Compare("pkg", Ints(2)).Changes);
    }

    [Fact]
    public void AForgottenKeyStartsAgain()
    {
        var watch = new FieldWatch();
        watch.Compare("unit", Ints(1));

        watch.Forget("unit");

        Assert.True(watch.Compare("unit", Ints(2)).First);
    }

    [Fact]
    public void ListsTheNonZeroSmallFieldsUpToALimit()
    {
        var values = SmallFields.NonZero(Ints(0, 5, 0x3F80_0000, -1, 9), max: 2);

        Assert.Equal([(4, 5), (12, -1)], values);
    }

    [Fact]
    public void FindsTheEightByteFieldsThatPointAtKnownObjects()
    {
        var bytes = new byte[24];
        BitConverter.TryWriteBytes(bytes.AsSpan(8), 0x1_2345_6780L);
        BitConverter.TryWriteBytes(bytes.AsSpan(16), 0x1_0000_0000L);

        var refs = SmallFields.PointersTo(bytes, new HashSet<long> { 0x1_2345_6780L });

        Assert.Equal([(8, 0x1_2345_6780L)], refs);
    }

    [Fact]
    public void WritesChangesValuesAndRefsInTheLogForm()
    {
        Assert.Equal("+0x10:4>3,+0x1c:0>-1",
            SmallFields.Describe([new FieldChange(0x10, 4, 3), new FieldChange(0x1c, 0, -1)]));
        Assert.Equal("+0x4=5,+0xc=-1", SmallFields.Describe([(4, 5), (12, -1)]));
        Assert.Equal("none", SmallFields.Describe(Array.Empty<FieldChange>()));
    }
}
