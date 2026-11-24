using Metaphor.Core.Announcements;
using Metaphor.Core.Input;

namespace Metaphor.Features.Field.Tests;

/// <summary>Scan by direction (#701): RB held in free roam and the right stick pushed says the nearest thing that way,
/// from the screen's facing; the game gets the right stick at rest meanwhile. The scan key says the four ways.</summary>
[Trait("Category", "Unit")]
public sealed class StickScanTests
{
    private const int RB = 0x0800;
    private const int KeyU = 0x55;

    private readonly ScanField field = new();
    private readonly RecordingAnnouncer announcer = new();
    private readonly HashSet<FieldFeature> on = [FieldFeature.StickScan];
    private readonly StickScan scan;

    public StickScanTests()
    {
        // Camera looking along +Z: +X is the screen's left (headings grow counter-clockwise).
        field.View = new FieldView(0, 0, 0, Math.PI, [
            new(FieldKind.Door, 0, 0, 500),
            new(FieldKind.Chest, 300, 0, 0),
            new(FieldKind.Person, 0, 0, -2000),
        ], CameraHeading: 0);
        scan = new StickScan(field, announcer, on.Contains, () => KeyU, () => Nearby.Radius);
    }

    private static PadSticks Right(double x, double y) =>
        PadSticks.Rest with { RightX = PadSticks.Centre + (int)(x * 127), RightY = PadSticks.Centre - (int)(y * 127) };

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void RbAndTheRightStickUpSayWhatIsAheadAndTheCameraStaysStill()
    {
        var handed = scan.Sticks(0, RB, Right(0, 1));

        Assert.Equal(["Ahead: Door, 5 steps ahead"], Said());
        Assert.Equal(PadSticks.Rest, handed);
    }

    [Fact]
    public void EachNewWayIsSaidOnceAndAgainAfterTheStickRests()
    {
        scan.Sticks(0, RB, Right(-1, 0));
        scan.Sticks(0, RB, Right(-1, 0));
        scan.Sticks(0, RB, Right(1, 0));
        scan.Sticks(0, RB, Right(0, 0));
        scan.Sticks(0, RB, Right(1, 0));

        Assert.Equal(["Left: Chest, 3 steps left", "Nothing right", "Nothing right"], Said());
    }

    [Fact]
    public void ThingsBeyondTheEightNearestAreStillFound()
    {
        var crowd = Enumerable.Range(1, Nearby.MaxThings)
            .Select(i => new FieldUnit(FieldKind.Person, 0, 0, i * 10f))
            .Append(new FieldUnit(FieldKind.Chest, 0, 0, -900));
        field.View = new FieldView(0, 0, 0, 0, [.. crowd], CameraHeading: 0);

        scan.Sticks(0, RB, Right(0, -1));

        Assert.Equal(["Behind: Chest, 9 steps behind"], Said());
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(RB, false, true)]
    [InlineData(RB, true, false)]
    public void WithoutRbOutsideFreeRoamOrTurnedOffTheStickReachesTheGame(int buttons, bool roaming, bool enabled)
    {
        field.Roaming = roaming;
        if (!enabled)
        {
            on.Clear();
        }
        var pushed = Right(0, 1);

        Assert.Equal(pushed, scan.Sticks(0, buttons, pushed));
        Assert.Empty(Said());
    }

    [Fact]
    public void TheScanKeySaysTheFourWaysAndIsHidden()
    {
        var handed = scan.Keys([KeyU]);
        scan.Keys([KeyU]);

        Assert.Equal(
            ["Ahead: Door, 5 steps ahead. Left: Chest, 3 steps left. Nothing right. Behind: Person, 20 steps behind"],
            Said());
        Assert.Empty(handed);
    }

    [Fact]
    public void TheScanKeyOutsideFreeRoamReachesTheGame()
    {
        field.Roaming = false;

        Assert.Equal([KeyU], scan.Keys([KeyU]));
        Assert.Empty(Said());
    }

    private sealed class ScanField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldView? View { get; set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => View;

        public int? MapId() => 1;

        public FieldPose? ReadPose() => View?.Pose;
    }
}
