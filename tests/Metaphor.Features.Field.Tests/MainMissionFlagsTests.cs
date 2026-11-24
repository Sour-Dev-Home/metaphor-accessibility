namespace Metaphor.Features.Field.Tests;

/// <summary>The main-mission flag scan (#658): the set story flags in the range the game's current-main-mission read
/// scans, highest first. Made-up flag sets only.</summary>
[Trait("Category", "Unit")]
public sealed class MainMissionFlagsTests
{
    [Fact]
    public void ListsTheHighestSetFlagsInTheRangeFirst()
    {
        HashSet<int> set = [0x1164, 0x1165, 0x1180, 0x11f0, 0x120a, 0x120b];

        var flags = MainMissionFlags.Highest(flag => set.Contains(flag), 3);

        Assert.Equal([0x120a, 0x11f0, 0x1180], flags);
    }

    [Fact]
    public void KeepsTheRangeEnds()
    {
        HashSet<int> set = [0x1164, 0x1165, 0x120b];

        Assert.Equal([0x1165], MainMissionFlags.Highest(flag => set.Contains(flag), 4));
    }

    [Fact]
    public void AnUnreadableFlagGivesNull()
    {
        Assert.Null(MainMissionFlags.Highest(flag => flag == 0x1200 ? null : false, 4));
    }

    [Fact]
    public void TheCurrentRowIsTheHighestSetFlagWithARow()
    {
        HashSet<int> set = [0x1166, 0x1169, 0x116a, 0x1200];
        MainMissionRow[] rows = [new(0x1166, "AA"), new(0x1169, "BB"), new(0x116b, "CC")];

        Assert.Equal(new MainMissionRow(0x1169, "BB"), MainMissionFlags.Current(flag => set.Contains(flag), rows));
    }

    [Fact]
    public void NoCurrentRowWithoutASetFlagWithARowOrAReadableFlag()
    {
        MainMissionRow[] rows = [new(0x1166, "AA"), new(0x1100, "BB")];

        Assert.Null(MainMissionFlags.Current(flag => flag == 0x1100, rows));
        Assert.Null(MainMissionFlags.Current(flag => flag == 0x1166 ? null : false, rows));
        Assert.Null(MainMissionFlags.Current(_ => true, []));
    }

    // #652: the icon points name the field objective table's row by its position from 0.
    [Fact]
    public void TheCurrentIndexIsTheFirstRowWithTheHighestSetFlag()
    {
        HashSet<int> set = [0x1166, 0x1169];
        MainMissionRow[] rows = [new(0x1166, "AA"), new(0x1169, "BB"), new(0x1169, "CC"), new(0x116b, "DD")];

        Assert.Equal(1, MainMissionFlags.CurrentIndex(flag => set.Contains(flag), rows));
        Assert.Null(MainMissionFlags.CurrentIndex(flag => flag == 0x1166 ? null : false, rows));
        Assert.Null(MainMissionFlags.CurrentIndex(_ => false, rows));
    }

    [Fact]
    public void DescribesFlagsAsHexForTheLog()
    {
        Assert.Equal("0x120a,0x1180", MainMissionFlags.Describe([0x120a, 0x1180]));
        Assert.Equal("none", MainMissionFlags.Describe([]));
        Assert.Equal("?", MainMissionFlags.Describe(null));
    }
}
