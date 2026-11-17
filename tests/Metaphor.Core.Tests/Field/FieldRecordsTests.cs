using Metaphor.Core.Field;

namespace Metaphor.Core.Tests.Field;

/// <summary>What the #177, #178 and #180 field watch derives and logs. Made-up positions and ids only.</summary>
[Trait("Category", "Unit")]
public sealed class FieldRecordsTests
{
    [Theory]
    [InlineData(0f, 0f, 0f, 1f, 0)]
    [InlineData(0f, 0.70710677f, 0f, 0.70710677f, 90)]
    [InlineData(0f, -0.70710677f, 0f, 0.70710677f, -90)]
    [InlineData(0f, 1f, 0f, 0f, 180)]
    public void FacingIsTheHeadingOfForwardRotatedAboutY(float x, float y, float z, float w, double expected) =>
        Assert.Equal(expected, FieldGeometry.YawDegrees(x, y, z, w), precision: 3);

    [Fact]
    public void HeadingAndDistanceIgnoreHeight()
    {
        var from = new FieldVector(1, 5, 1);
        var to = new FieldVector(4, -2, 5);

        Assert.Equal(5, FieldGeometry.GroundDistance(from, to), precision: 6);
        Assert.Equal(Math.Atan2(3, 4) * 180 / Math.PI, FieldGeometry.HeadingTo(from, to), precision: 6);
    }

    [Fact]
    public void StateLineRoundsAndMarksWhatWasNotRead()
    {
        var state = new FieldState("fldScene_Idle@fld@app", 301, "fldPlayer@fld@app", new FieldVector(1.25f, 0, -3.04f),
            89.6, 0, 0x100, 5, new FieldVector(0, 2, 0), new FieldVector(0, 2, 10));

        Assert.Equal(
            "scene=fldScene_Idle@fld@app map=301 player=fldPlayer@fld@app pos=1.3,0.0,-3.0 facing=90 locks=0 action=0x100 cam=5 cam60=0.0,2.0,0.0 cam70=0.0,2.0,10.0 camHeading=0",
            state.ToString());
        Assert.Equal(
            "scene=none map=- player=none pos=- facing=- locks=- action=0x0 cam=- cam60=- cam70=- camHeading=-",
            new FieldState(null, null, null, null, null, null, null, null, null, null).ToString());
    }

    [Fact]
    public void NearbyNamesEachNonEmptyListWithItsNearestUnits()
    {
        var player = new FieldVector(0, 0, 0);
        var lists = new Dictionary<int, (int, string?)>
        {
            [2] = (4, "fldNpc@fld@app"),
            [5] = (1, "fldGmcDoor@fld@app"),
            [0] = (0, null),
        };
        FieldUnit[] units =
        [
            new(2, 11, new FieldVector(0, 0, 10)),
            new(2, 12, new FieldVector(3, 0, 0)),
            new(2, 13, new FieldVector(0, 0, -1)),
            new(2, 14, new FieldVector(50, 0, 50)),
            new(5, 7, new FieldVector(0, 0, 0), new FieldVector(-2, 0, 0)),
        ];

        Assert.Equal(
            "npc=4(fldNpc@fld@app)[13@1.0/180 12@3.0/90 11@10.0/0] door=1(fldGmcDoor@fld@app)[7@0.0/0/node2.0/-90]",
            FieldNearby.Describe(player, lists, units));
    }

    [Fact]
    public void NearbyWithNoUnitsSaysNone() =>
        Assert.Equal("none", FieldNearby.Describe(default, new Dictionary<int, (int, string?)>(), []));

    [Fact]
    public void ListsSitEvery0x40BytesFromTheEnemyList()
    {
        Assert.Equal(21, FieldNearby.ListNames.Count);
        Assert.Equal(0x18, FieldNearby.BeginOffset(0));
        Assert.Equal(0x98, FieldNearby.BeginOffset(FieldNearby.ListNames.ToList().IndexOf("npc")));
        Assert.Equal(0x158, FieldNearby.BeginOffset(FieldNearby.ListNames.ToList().IndexOf("door")));
        Assert.Equal(0x418, FieldNearby.BeginOffset(FieldNearby.ListNames.ToList().IndexOf("gayaHit")));
        Assert.Equal(0x518, FieldNearby.BeginOffset(FieldNearby.ListNames.ToList().IndexOf("vehicle")));
    }

    [Fact]
    public void IntervalGateLetsTheFirstCallAndThenOnePerInterval()
    {
        var now = 0L;
        var gate = new IntervalGate(500, () => now);

        Assert.True(gate.Due());
        now = 499;
        Assert.False(gate.Due());
        now = 500;
        Assert.True(gate.Due());
        now = 900;
        Assert.False(gate.Due());
    }
}
