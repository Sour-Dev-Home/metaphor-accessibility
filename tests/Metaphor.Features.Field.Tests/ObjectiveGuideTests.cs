using Metaphor.Core.Navigation;
using Metaphor.Core.Tests.Navigation;

namespace Metaphor.Features.Field.Tests;

/// <summary>"Guide me to the objective" (#652): the current field objective's row picks its markers on the map's field,
/// converted to world by a fitted conversion, plus hand-written waypoints; the nearest is the beacon's target. Made-up
/// flags, rows, fields and points only.</summary>
[Trait("Category", "Unit")]
public sealed class ObjectiveGuideTests
{
    private const int Map = 7;

    // Rows 0 to 2 start with flags 0x1165 to 0x1167; the top set flag picks the row.
    private readonly List<MainMissionRow> rows =
        [new(0x1165, "A0"), new(0x1166, "A1"), new(0x1167, "A2")];

    private readonly HashSet<int> set = [0x1165, 0x1166];
    private readonly Dictionary<string, byte[]> archive = [];
    private IMapToWorld conversion = FittedMapToWorld.Empty;
    private List<ObjectiveWaypoint> waypoints = [];
    private Func<Func<FieldIconPoints?>, Task<FieldIconPoints?>> run = MapIconPointsTests.Inline;
    private WalkGraph? graph;

    private ObjectiveGuide Guide() => new(
        flag => set.Contains(flag), () => rows, map => map == Map ? (1, 2) : null,
        new MapIconPointSource(path => archive.GetValueOrDefault(path), _ => { }, run), conversion, waypoints,
        (a, b) => (a, b) == (1, 2) ? graph : null);

    // Map 7's minimap record, as the default conversion: x = 1000 - 10 px, z = 2000 - 10 py.
    private static MiniMapFrameSource Minimap(Func<Action, Task>? run = null) => new(
        () => (MiniMapFramesTests.Sets((Map, [(5, 0)])), MiniMapFramesTests.Data((5, 1000, 2000, 10))), _ => { },
        run ?? MiniMapFramesTests.Inline);

    private void Points(params (int X, int Y, int Kind, int Index)[] points) =>
        archive[MapIconPoints.ArchivePath(1, 2, 0)] = MapIconPointsTests.File(points);

    private static FittedMapToWorld Fit(double originX, double originZ, double scaleX, double scaleZ) =>
        new(new Dictionary<string, MapConversion> { ["f_0001_002"] = new(originX, originZ, scaleX, scaleZ) });

    private static FieldPose At(float x, float z) => new(x, 10, z, 0);

    [Fact]
    public void TheNearestMarkerForTheCurrentRowIsTheTarget()
    {
        Points((100, 100, 4, 1), (500, 500, 4, 1), (110, 110, 4, 2), (120, 120, 5, 1));
        conversion = Fit(0, 0, 10, 10);

        var answer = Guide().Find(Map, At(4000, 4000));

        var unit = answer.Target!.Unit;
        Assert.Equal((5000f, 5000f), (unit.X, unit.Z));
        Assert.Equal(10, unit.Y);
        Assert.Equal(ObjectiveGuide.TargetName, unit.Name);
    }

    [Fact]
    public void TheMapCursorGetsEveryIconPointInWorldWithTheCurrentObjectivesMarked()
    {
        // #877: every kind, not only the objective's; row 1 is the current one.
        Points((100, 100, 4, 1), (110, 110, 4, 2), (120, 120, 5, 1));
        conversion = Fit(0, 0, 10, 10);

        var marks = Guide().Marks(Map, 10);

        Assert.Equal([(1000f, 1000f, true), (1100f, 1100f, false), (1200f, 1200f, false)], marks);
        Assert.Empty(Guide().Marks(Map + 1, 10));
        Assert.Empty(Guide().Marks(null, 10));
    }

    [Fact]
    public void AMarkerWithNoConversionIsNotReady()
    {
        Points((100, 100, 4, 1));

        Assert.Equal(new ObjectiveAnswer(null, ObjectiveGuide.NotReadyText), Guide().Find(Map, At(0, 0)));
    }

    [Fact]
    public void TheMapsMinimapRecordPlacesItsMarkersByDefault()
    {
        Points((10, 20, 4, 1));
        conversion = new OverriddenMapToWorld(FittedMapToWorld.Empty, Minimap());

        var unit = Guide().Find(Map, At(0, 0)).Target!.Unit;

        Assert.Equal((900f, 1800f), (unit.X, unit.Z));
    }

    [Fact]
    public void AnOverrideForTheFieldWinsOverTheRecord()
    {
        Points((10, 20, 4, 1));
        conversion = new OverriddenMapToWorld(Fit(0, 0, 1, 1), Minimap());

        var unit = Guide().Find(Map, At(5000, 5000)).Target!.Unit;

        Assert.Equal((10f, 20f), (unit.X, unit.Z));
    }

    [Fact]
    public void AMapWithNoRecordIsNotReady()
    {
        Points((10, 20, 4, 1));
        conversion = new OverriddenMapToWorld(FittedMapToWorld.Empty, new MiniMapFrameSource(
            () => (MiniMapFramesTests.Sets((Map + 1, [(5, 0)])), MiniMapFramesTests.Data((5, 1000, 2000, 10))), _ => { },
            MiniMapFramesTests.Inline));

        Assert.Equal(new ObjectiveAnswer(null, ObjectiveGuide.NotReadyText), Guide().Find(Map, At(0, 0)));
    }

    [Fact]
    public void FramesStillLoadingAreSaid()
    {
        Points((10, 20, 4, 1));
        var gate = new TaskCompletionSource();
        conversion = Minimap(_ => gate.Task);

        Assert.Equal(ObjectiveGuide.LoadingText, Guide().Find(Map, At(0, 0)).Text);
    }

    [Fact]
    public void PollStartsTheFramesRead()
    {
        var started = 0;
        conversion = Minimap(load =>
        {
            started++;
            return MiniMapFramesTests.Inline(load);
        });

        Guide().Poll(() => null, 0);

        Assert.Equal(1, started);
    }

    [Fact]
    public void DescribeLogsEachMarkersWorldPointAndItsDistanceToTheMesh()
    {
        // Two 4 x 4 tiles of 10-unit cells, x 0 to 80, z 0 to 40: marker (97, 198) is at (30, 20), a cell corner, 7
        // units from each of its cells' middles; (0, 0) is at (1000, 2000), off the mesh and past the search.
        graph = new WalkGraph(NavMeshFile.Parse(NavBytes.File(
            [NavBytes.Layer.Open(0, 0).Entry(), NavBytes.Layer.Open(1, 0).Entry()], climb: 2))!);
        Points((97, 198, 4, 1), (0, 0, 4, 1), (5, 5, 4, 2));
        conversion = Minimap();

        var line = Guide().Describe("key", Map, new FieldPose(0, 0, 0, 0));

        Assert.EndsWith(" conversion=minimap:5(1000,2000,10) waypoints=0 variant1=False " +
            "points=(97,198 k=4 i=1) (0,0 k=4 i=1) (5,5 k=4 i=2) world=(97,198)->(30,20)r=7 (0,0)->(1000,2000)r=far",
            line, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeSaysTheMeshIsStillLoading()
    {
        Points((97, 198, 4, 1));
        conversion = Minimap();

        Assert.EndsWith(" world=(97,198)->(30,20)r=?", Guide().Describe("key", Map, null), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownFieldIsNotReady() =>
        Assert.Equal(ObjectiveGuide.NotReadyText, Guide().Find(Map + 1, At(0, 0)).Text);

    [Fact]
    public void NoMarkerAndNoWaypointForTheRowIsSaid()
    {
        Points((100, 100, 4, 2));
        conversion = Fit(0, 0, 1, 1);

        Assert.Equal(ObjectiveGuide.NoMarkerText, Guide().Find(Map, At(0, 0)).Text);
    }

    [Fact]
    public void AHandWrittenWaypointGuidesARowWithNoMarker()
    {
        waypoints = [new("f_0001_002", 1, 900, -900), new("f_0001_002", 2, 1, 1), new("f_0001_003", 1, 1, 1)];

        var unit = Guide().Find(Map, At(0, 0)).Target!.Unit;

        Assert.Equal((900f, -900f), (unit.X, unit.Z));
    }

    [Fact]
    public void NoCurrentObjectiveIsSaid()
    {
        set.Clear();

        Assert.Equal(ObjectiveGuide.NoObjectiveText, Guide().Find(Map, At(0, 0)).Text);
    }

    [Fact]
    public void StandingOnTheNearestOneIsSaid()
    {
        waypoints = [new("f_0001_002", 1, 100, 100)];

        Assert.Equal(ObjectiveGuide.ArrivedText, Guide().Find(Map, At(150, 150)).Text);
    }

    [Fact]
    public void MarkersStillLoadingAreSaid()
    {
        var gate = new TaskCompletionSource<FieldIconPoints?>();
        run = _ => gate.Task;

        Assert.Equal(ObjectiveGuide.LoadingText, Guide().Find(Map, At(0, 0)).Text);
    }

    [Fact]
    public void PollStartsTheFieldsReadAtMostOnceASecond()
    {
        var starts = 0;
        run = load =>
        {
            starts++;
            return Task.FromResult(load());
        };
        var guide = Guide();
        var mapReads = 0;
        int? Read()
        {
            mapReads++;
            return Map;
        }

        guide.Poll(Read, 1000);
        guide.Poll(Read, 1500);
        guide.Poll(Read, 2000);

        Assert.Equal(1, starts);
        Assert.Equal(2, mapReads);
    }

    [Fact]
    public void DescribeLogsWhatAMarkerWalkNeeds()
    {
        Points((1478, 1025, 4, 1), (7, 8, 5, 0));
        waypoints = [new("f_0001_002", 1, 1, 1)];

        var line = Guide().Describe("place", Map, new FieldPose(-2269.44f, 24, 8609.8f, 0));

        Assert.Equal("objective place map=7 field=f_0001_002 player=(-2269.4,24,8609.8) row=1 flag=0x1166 " +
            "conversion=none waypoints=1 variant1=False points=(1478,1025 k=4 i=1) (7,8 k=5 i=0) " +
            "world=(1478,1025)->none", line);
    }

    [Fact]
    public void DescribeSaysWhatIsUnknown() =>
        Assert.Equal("objective key map=? field=? player=? row=1 flag=0x1166 conversion=none waypoints=0 " +
            "variant1=False points=no field world=none", Guide().Describe("key", null, null));
}
