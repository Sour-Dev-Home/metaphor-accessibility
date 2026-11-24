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

    private ObjectiveGuide Guide() => new(
        flag => set.Contains(flag), () => rows, map => map == Map ? (1, 2) : null,
        new MapIconPointSource(path => archive.GetValueOrDefault(path), _ => { }, run), conversion, waypoints);

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
    public void AMarkerWithNoConversionIsNotReady()
    {
        Points((100, 100, 4, 1));

        Assert.Equal(new ObjectiveAnswer(null, ObjectiveGuide.NotReadyText), Guide().Find(Map, At(0, 0)));
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
            "conversion=False waypoints=1 variant1=False points=(1478,1025 k=4 i=1) (7,8 k=5 i=0)", line);
    }

    [Fact]
    public void DescribeSaysWhatIsUnknown() =>
        Assert.Equal("objective key map=? field=? player=? row=1 flag=0x1166 conversion=False waypoints=0 " +
            "variant1=False points=no field", Guide().Describe("key", null, null));
}
