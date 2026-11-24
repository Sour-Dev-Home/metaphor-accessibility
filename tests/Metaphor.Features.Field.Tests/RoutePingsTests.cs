using System.Numerics;
using Metaphor.Core.Cues;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field.Tests;

/// <summary>The route pings (#831): a ping toward the place the list steps to, straight by default, from the route's
/// next corner when the planner finds one that bends, panned in the camera-relative frame and quieter farther off. A
/// stand-in planner and field only.</summary>
[Trait("Category", "Unit")]
public sealed class RoutePingsTests
{
    // With the camera looking along +z, +x is on the screen's left (CameraFrame).
    private static readonly FieldUnit Place = new(FieldKind.Door, 500, 0, 400);

    private readonly NavField field = new();
    private readonly ChannelCues cues = new();
    private readonly List<string> logged = [];
    private readonly NavFloor floor;
    private RoutePlan? plan;
    private Task<RoutePlan>? slow;
    private bool on = true;
    private long now = 1000;
    private readonly RoutePings pings;

    public RoutePingsTests()
    {
        floor = new NavFloor(() => now, () => false);
        pings = Make(new Planner(this));
    }

    [Fact]
    public void EachKindSoundsItsOwnWay()
    {
        var kinds = Enum.GetValues<RoutePingKind>();
        Assert.Equal(kinds.Length, kinds.Select(kind => string.Join(",", RoutePings.Steps(kind))).Distinct().Count());
        Assert.All(kinds, kind => Assert.NotEmpty(RoutePings.Steps(kind)));
    }

    [Fact]
    public void VolumeFallsWithDistance()
    {
        var limits = new RoutePingLimits(90);
        Assert.Equal(90, RoutePings.Volume(0, limits));
        Assert.True(RoutePings.Volume(500, limits) > RoutePings.Volume(2000, limits));
        Assert.Equal(45, RoutePings.Volume(0, new RoutePingLimits(45)));
    }

    [Fact]
    public void WithNoPlannerThePingComesStraightFromThePlace()
    {
        var straight = Make(null);

        straight.Focus(Place);

        var tone = Assert.Single(cues.Held(ToneChannel.Route));
        Assert.Equal(RoutePings.Steps(RoutePingKind.Straight)[0].Hertz, tone.Hertz);
        Assert.Equal(CameraFrame.Pan(CameraFrame.Bearing(field.Pose!.Value, Place.X, Place.Z)), tone.Pan, 6);
        Assert.Equal(RoutePings.Volume(Math.Sqrt((500 * 500) + (400 * 400)), RoutePingLimits.Default), tone.Volume);
    }

    [Fact]
    public void WithSoundFilesOnAPingPlaysItsKindsFileWholeThenStops()
    {
        var withFiles = new RoutePings(
            field, cues, () => now, () => on, () => RoutePingLimits.Default,
            samples: new NavSamples(NavSamplesTests.Shipped, () => true));
        var file = NavSamplesTests.Shipped.Find(NavSamples.FileName(RoutePingKind.Straight))!;

        withFiles.Focus(Place);
        now += file.Milliseconds - 1;
        withFiles.Tick();

        // One hold of the whole file, its own gap in it, so no silence until it has played.
        var tone = Assert.Single(cues.Held(ToneChannel.Route));
        Assert.Same(file, tone.Sample);
        Assert.False(tone.Loops);
        Assert.NotNull(cues.Now(ToneChannel.Route));
        now += 1;
        withFiles.Tick();
        Assert.Null(cues.Now(ToneChannel.Route));
    }

    [Fact]
    public void WithoutItsFileAPingPlaysItsNotes()
    {
        var withoutFiles = new RoutePings(
            field, cues, () => now, () => on, () => RoutePingLimits.Default,
            samples: new NavSamples(SoundBank.Empty, () => true));

        withoutFiles.Focus(Place);

        Assert.Null(Assert.Single(cues.Held(ToneChannel.Route)).Sample);
    }

    [Fact]
    public void ARouteThatBendsPingsFromItsNextCorner()
    {
        // The route goes straight ahead first, then left to the place.
        plan = new RoutePlan(RouteOutcome.Found, [new Vector3(0, 0, 400), new Vector3(Place.X, 0, Place.Z)], "");

        pings.Focus(Place);

        var tone = Assert.Single(cues.Held(ToneChannel.Route));
        Assert.Equal(RoutePings.Steps(RoutePingKind.Around)[0].Hertz, tone.Hertz);
        Assert.Equal(0, tone.Pan, 6);
        Assert.Contains("route ping kind=Around outcome=Found aim=(0,400)", Assert.Single(logged), StringComparison.Ordinal);
    }

    [Fact]
    public void AStraightRouteIsAStraightPing()
    {
        plan = new RoutePlan(RouteOutcome.Found, [new Vector3(Place.X, 0, Place.Z)], "");

        pings.Focus(Place);

        Assert.Equal(RoutePings.Steps(RoutePingKind.Straight)[0].Hertz, Assert.Single(cues.Held(ToneChannel.Route)).Hertz);
    }

    [Theory]
    [InlineData(RouteOutcome.NoPath, RoutePingKind.NoPath)]
    [InlineData(RouteOutcome.NoNavMesh, RoutePingKind.Straight)]
    [InlineData(RouteOutcome.OffMesh, RoutePingKind.Straight)]
    [InlineData(RouteOutcome.Loading, RoutePingKind.Straight)]
    public void WithoutARouteThePingComesFromThePlaceAndSaysWhy(RouteOutcome outcome, RoutePingKind kind)
    {
        plan = RoutePlan.Failed(outcome, "");

        pings.Focus(Place);

        var tone = Assert.Single(cues.Held(ToneChannel.Route));
        Assert.Equal(RoutePings.Steps(kind)[0].Hertz, tone.Hertz);
        Assert.Equal(-Math.Sin(Math.Atan2(500, 400)), tone.Pan, 6);
    }

    [Fact]
    public void ASlowPlanGoesStraightOnceTheWaitIsOver()
    {
        slow = new TaskCompletionSource<RoutePlan>().Task;

        pings.Focus(Place);
        now += RoutePings.PlanWaitMilliseconds - 1;
        pings.Tick();
        Assert.Empty(cues.Calls);

        now += 1;
        pings.Tick();

        Assert.Equal(RoutePings.Steps(RoutePingKind.Straight)[0].Hertz, Assert.Single(cues.Held(ToneChannel.Route)).Hertz);
    }

    [Fact]
    public void ThePingPlaysItsNotesThenStops()
    {
        var straight = Make(null);
        straight.Focus(Place);
        var steps = RoutePings.Steps(RoutePingKind.Straight);
        foreach (var note in steps)
        {
            now += note.Milliseconds;
            straight.Tick();
        }

        // Notes, a gap (a silence) between them, and a silence at the end.
        Assert.Equal(steps.Count(note => note.Hertz > 0), cues.Held(ToneChannel.Route).Count());
        Assert.Null(cues.Now(ToneChannel.Route));
        var calls = cues.Calls.Count;
        now += 1000;
        straight.Tick();
        Assert.Equal(calls, cues.Calls.Count);
    }

    [Fact]
    public void APingTakesTheFloorFromTheSonar()
    {
        pings.Focus(Place);
        Assert.False(floor.SonarMayPing());

        now += NavFloor.PingHoldMilliseconds;

        Assert.True(floor.SonarMayPing());
    }

    [Fact]
    public void NothingPingsWhenOffOrOutOfFreeRoam()
    {
        on = false;
        pings.Focus(Place);
        pings.Tick();
        on = true;
        field.Roaming = false;
        pings.Focus(Place);
        pings.Tick();

        Assert.Empty(cues.Calls);
    }

    [Fact]
    public void LeavingFreeRoamMidPingSilencesIt()
    {
        pings.Focus(Place);
        field.Roaming = false;

        pings.Tick();

        Assert.Null(cues.Now(ToneChannel.Route));
    }

    [Fact]
    public void TheLimitComesFromTheSettingsFileWithinRange()
    {
        var parsed = ModSettings.Parse("""{ "limits": { "field": { "routePingVolume": 140 } } }""",
            SettingCatalogue.Empty, _ => { });

        Assert.Equal(new RoutePingLimits(100), RoutePingLimits.From(parsed));
        Assert.Equal(RoutePingLimits.Default, RoutePingLimits.From(ModSettings.Defaults));
    }

    [Theory]
    [InlineData(45, 1000, 2)]
    [InlineData(-45, 1000, 2)]
    [InlineData(20, 1000, 1)]
    [InlineData(45, TurnPings.RouteWindowMilliseconds + 10, 1)]
    public void ACameraTurnSoonAfterAStepPingsThePlaceAgainFromItsNewSide(double turnDegrees, long after, int expected)
    {
        // #831, step 4: the ping has played through, then the camera turns.
        var turning = new RoutePings(
            field, cues, () => now, () => on, () => RoutePingLimits.Default, turnDegrees: () => 30);
        turning.Focus(Place);
        for (var end = now + after; now < end; now += 10)
        {
            turning.Tick();
        }
        Assert.Null(cues.Now(ToneChannel.Route));
        var played = cues.Held(ToneChannel.Route).Count();

        field.Pose = field.Pose!.Value with { CameraHeading = turnDegrees * Math.PI / 180 };
        turning.Tick();

        // A new ping starts with its first note, from where the place now is.
        var held = cues.Held(ToneChannel.Route).ToList();
        Assert.Equal(expected - 1, held.Count - played);
        if (expected == 2)
        {
            Assert.Equal(RoutePings.Steps(RoutePingKind.Straight)[0].Hertz, held[^1].Hertz);
            Assert.Equal(CameraFrame.Pan(CameraFrame.Bearing(field.Pose!.Value, Place.X, Place.Z)), held[^1].Pan, 6);
        }
    }

    [Fact]
    public void WithoutTheTurnSettingATurnPingsNothing()
    {
        var plain = Make(null);
        plain.Focus(Place);
        for (var end = now + 1000; now < end; now += 10)
        {
            plain.Tick();
        }
        var count = cues.Calls.Count;

        field.Pose = field.Pose!.Value with { CameraHeading = 1.5 };
        plain.Tick();

        Assert.Equal(count, cues.Calls.Count);
    }

    [Fact]
    public void TheTurnAngleIsTheShortWayRound()
    {
        Assert.True(TurnPings.Turned(0, 0.6, 30));
        Assert.True(TurnPings.Turned(0.6, 0, 30));
        Assert.False(TurnPings.Turned(3.1, -3.1, 30));
        Assert.True(TurnPings.Turned(3.1, -2.5, 30));
        Assert.Equal("field.turnPingDegrees", TurnPings.Degrees.Path);
    }

    private RoutePings Make(IRoutePlanner? planner) => new(
        field, cues, () => now, () => on, () => RoutePingLimits.Default, planner,
        run: work => slow ?? Task.FromResult(work()), floor: floor, log: logged.Add);

    private sealed class Planner(RoutePingsTests test) : IRoutePlanner
    {
        public RoutePlan Plan(int? mapId, Vector3 start, Vector3 target) =>
            test.plan ?? RoutePlan.Failed(RouteOutcome.NoNavMesh, "");
    }
}
