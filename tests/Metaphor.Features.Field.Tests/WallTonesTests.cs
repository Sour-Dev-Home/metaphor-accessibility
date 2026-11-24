using System.Numerics;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;
using Metaphor.Core.Tests.Navigation;

namespace Metaphor.Features.Field.Tests;

/// <summary>The wall tones (#830): four tones ahead, behind, left and right of the camera's horizontal angle, louder as
/// the navmesh's edge nears, silent where the map has no navmesh. A made-up box room stands in for the navmesh.</summary>
[Trait("Category", "Unit")]
public sealed class WallTonesTests
{
    // The room: x from -100 to 200, z from -250 to 50, the player at the origin. With the camera looking along +z, +x is
    // on the screen's left (CameraFrame): the wall ahead is 50 off, behind 250, left 200, right 100.
    private const float MinX = -100;
    private const float MaxX = 200;
    private const float MinZ = -250;
    private const float MaxZ = 50;

    private static readonly WallLimits Limits = new(300, 60);

    private readonly NavField field = new();
    private readonly ChannelCues cues = new();
    private readonly SaidList announcer = new();
    private readonly List<string> logged = [];
    private Task<NavEdges?> edges = Task.FromResult<NavEdges?>(new(Box, _ => new NavCell(0, 1, 2)));
    private bool on = true;
    private bool guiding;
    private long now = 1000;
    private readonly WallTones walls;

    public WallTonesTests() =>
        walls = new WallTones(field, cues, announcer, () => now, () => on, () => Limits, _ => edges,
            new NavFloor(() => now, () => guiding), logged.Add);

    [Fact]
    public void EachDirectionHasItsOwnChannelAndAheadIsHigherThanBehind()
    {
        var directions = Enum.GetValues<WallDirection>();
        Assert.Equal(4, directions.Select(WallTones.Channel).Distinct().Count());
        Assert.True(WallTones.Hertz(WallDirection.Ahead) > WallTones.Hertz(WallDirection.Left));
        Assert.True(WallTones.Hertz(WallDirection.Left) > WallTones.Hertz(WallDirection.Behind));
        Assert.Equal(WallTones.Hertz(WallDirection.Left), WallTones.Hertz(WallDirection.Right));
    }

    [Fact]
    public void VolumeGrowsAsTheWallNearsAndIsSilentBeyondTheRange()
    {
        Assert.Equal(60, WallTones.Volume(0, Limits));
        Assert.Equal(30, WallTones.Volume(150, Limits));
        Assert.Equal(0, WallTones.Volume(300, Limits));
        Assert.Equal(0, WallTones.Volume(900, Limits));
        Assert.True(WallTones.Volume(50, Limits) > WallTones.Volume(100, Limits));
    }

    [Fact]
    public void FourTonesFollowTheCameraFrame()
    {
        walls.Tick();

        AssertTone(WallDirection.Ahead, 50, 0);
        AssertTone(WallDirection.Behind, 250, 0);
        AssertTone(WallDirection.Left, 200, -1);
        AssertTone(WallDirection.Right, 100, 1);
    }

    [Fact]
    public void TurningTheCameraTurnsTheTones()
    {
        // The camera looks along +x: ahead is the 200 wall, its left the -z wall 250 off, its right +z 50 off.
        field.Pose = new FieldPose(0, 0, 0, 0, CameraHeading: Math.PI / 2);

        walls.Tick();

        AssertTone(WallDirection.Ahead, 200, 0);
        AssertTone(WallDirection.Behind, 100, 0);
        AssertTone(WallDirection.Left, 250, -1);
        AssertTone(WallDirection.Right, 50, 1);
    }

    [Fact]
    public void WalkingTowardAWallMakesItLouderAndOnlyChangesReachThePlayer()
    {
        walls.Tick();
        var before = cues.Now(ToneChannel.Wall)!.Value.Volume;
        var calls = cues.Calls.Count;
        now += WallTones.ProbeMilliseconds;
        walls.Tick();
        Assert.Equal(calls, cues.Calls.Count);

        field.Pose = new FieldPose(0, 0, 30, 0, CameraHeading: 0);
        now += WallTones.ProbeMilliseconds;
        walls.Tick();

        Assert.True(cues.Now(ToneChannel.Wall)!.Value.Volume > before);
    }

    [Fact]
    public void ProbesAreThrottled()
    {
        walls.Tick();
        field.Pose = new FieldPose(0, 0, 30, 0, CameraHeading: 0);
        now += WallTones.ProbeMilliseconds - 1;
        walls.Tick();

        Assert.Equal(WallTones.Volume(50, Limits), cues.Now(ToneChannel.Wall)!.Value.Volume);
    }

    [Fact]
    public void NoNavMeshIsSilentAndSaidOncePerMap()
    {
        edges = Task.FromResult<NavEdges?>(null);

        for (var i = 0; i < 5; i++)
        {
            walls.Tick();
            now += WallTones.ProbeMilliseconds;
        }
        field.Map = 2;
        walls.Tick();

        Assert.DoesNotContain(cues.Calls, call => call.Tone is not null);
        Assert.Equal(
            [new Announcement(WallTones.NoNavMeshText, Interrupt: false),
                new Announcement(WallTones.NoNavMeshText, Interrupt: false)],
            announcer.Said);
        Assert.Equal(["wall tones map=1 navmesh=none", "wall tones map=2 navmesh=none"], logged);
    }

    [Fact]
    public void AStillLoadingNavMeshIsSilentAndSaysNothing()
    {
        edges = new TaskCompletionSource<NavEdges?>().Task;

        walls.Tick();

        Assert.Empty(cues.Calls);
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OffTheMeshEveryToneIsSilent()
    {
        walls.Tick();
        field.Pose = new FieldPose(5000, 0, 0, 0, CameraHeading: 0);
        now += WallTones.ProbeMilliseconds;

        walls.Tick();

        Assert.All(Enum.GetValues<WallDirection>(), direction => Assert.Null(cues.Now(WallTones.Channel(direction))));
    }

    [Fact]
    public void TurnedOffOrOutOfFreeRoamEveryToneStops()
    {
        walls.Tick();
        on = false;
        walls.Tick();
        Assert.All(Enum.GetValues<WallDirection>(), direction => Assert.Null(cues.Now(WallTones.Channel(direction))));

        on = true;
        field.Roaming = false;
        walls.Tick();
        Assert.All(Enum.GetValues<WallDirection>(), direction => Assert.Null(cues.Now(WallTones.Channel(direction))));
    }

    [Fact]
    public void TheTonesDuckWhileTheGuidanceBeepGuides()
    {
        guiding = true;

        walls.Tick();

        Assert.Equal(WallTones.Volume(50, Limits) * NavFloor.DuckedPercent / 100,
            cues.Now(ToneChannel.Wall)!.Value.Volume);
    }

    [Fact]
    public void APlayerInTheErodedBorderAlongAWallHearsIt()
    {
        // A real walk graph (two 4 × 4 tiles of 10-unit cells, x 0 to 80, agent radius 5): the player stands at x -3,
        // off every cell, up against the wall at x -5. The camera looks along +z, so -x is on the right.
        var graph = new WalkGraph(NavMeshFile.Parse(NavBytes.File(
            [NavBytes.Layer.Open(0, 0).Entry(), NavBytes.Layer.Open(1, 0).Entry()], climb: 2))!);
        edges = Task.FromResult<NavEdges?>(NavEdges.Of(graph));
        field.Pose = new FieldPose(-3, 0, 15, 0, CameraHeading: 0);

        walls.Tick();

        Assert.Equal(WallTones.Volume(2, Limits), cues.Now(ToneChannel.WallRight)!.Value.Volume);
        Assert.Contains(logged, line => line.StartsWith("wall probe map=1 at=-3,0,15 start=0/0,1 ", StringComparison.Ordinal));
    }

    [Fact]
    public void TheProbeIsLoggedAtMostOnceASecond()
    {
        for (var i = 0; i < 10; i++)
        {
            walls.Tick();
            now += WallTones.ProbeMilliseconds;
        }
        walls.Tick();

        Assert.Equal(
            ["wall tones map=1 navmesh=loaded",
                "wall probe map=1 at=0,0,0 start=0/1,2 ahead/behind/left/right=50 250 200 100",
                "wall probe map=1 at=0,0,0 start=0/1,2 ahead/behind/left/right=50 250 200 100"],
            logged);
    }

    [Fact]
    public void LimitsComeFromTheSettingsFileWithinRange()
    {
        var parsed = ModSettings.Parse("""{ "limits": { "field": { "wallRange": 9000, "wallVolume": 40 } } }""",
            SettingCatalogue.Empty, _ => { });

        Assert.Equal(new WallLimits(2000, 40), WallLimits.From(parsed));
        Assert.Equal(WallLimits.Default, WallLimits.From(ModSettings.Defaults));
    }

    // The distance from a point inside the box to its side along a heading; null outside it.
    private static float? Box(Vector3 position, Vector2 heading, float maxDistance)
    {
        if (position.X is < MinX or > MaxX || position.Z is < MinZ or > MaxZ)
        {
            return null;
        }
        var alongX = heading.X > 1e-6f ? (MaxX - position.X) / heading.X
            : heading.X < -1e-6f ? (MinX - position.X) / heading.X : float.PositiveInfinity;
        var alongZ = heading.Y > 1e-6f ? (MaxZ - position.Z) / heading.Y
            : heading.Y < -1e-6f ? (MinZ - position.Z) / heading.Y : float.PositiveInfinity;
        return Math.Min(maxDistance, Math.Min(alongX, alongZ));
    }

    private void AssertTone(WallDirection direction, double distance, double pan)
    {
        var tone = cues.Now(WallTones.Channel(direction));
        Assert.NotNull(tone);
        Assert.Equal(WallTones.Hertz(direction), tone.Value.Hertz);
        Assert.Equal(WallTones.Volume(distance, Limits), tone.Value.Volume);
        Assert.Equal(pan, tone.Value.Pan, 6);
    }
}
