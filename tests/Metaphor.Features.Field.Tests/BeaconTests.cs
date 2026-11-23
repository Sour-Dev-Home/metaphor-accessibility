using System.Numerics;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Navigation;

namespace Metaphor.Features.Field.Tests;

/// <summary>The beacon toward a picked target (#183): panned by bearing, paced by distance, higher ahead than behind, and
/// stopping on arrival, on a cancel and on leaving free roam. A stand-in field and made-up names only.</summary>
[Trait("Category", "Unit")]
public sealed class BeaconTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly RecordingCues cues = new();
    private readonly PoseField field = new();
    private readonly Beacon beacon;
    private long now = 1000;

    public BeaconTests() => beacon = new Beacon(field, cues, announcer, () => now);

    private static BeaconTarget Person(float x, float z) => new(new FieldUnit(FieldKind.Person, x, 0, z, "Testa"), null);

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Math.PI / 2, -1)]
    [InlineData(-Math.PI / 2, 1)]
    [InlineData(Math.PI, 0)]
    public void PanFollowsTheBearingLeftNegative(double bearing, double pan) =>
        Assert.Equal(pan, Beacon.Pan(bearing), 6);

    [Fact]
    public void PitchIsHighAheadAndLowBehind()
    {
        Assert.Equal(Beacon.AheadHertz, Beacon.Hertz(0), 6);
        Assert.Equal(Beacon.BehindHertz, Beacon.Hertz(Math.PI), 6);
        Assert.InRange(Beacon.Hertz(Math.PI / 2), Beacon.BehindHertz + 1, Beacon.AheadHertz - 1);
        Assert.Equal(Beacon.Hertz(Math.PI / 4), Beacon.Hertz(-Math.PI / 4), 6);
    }

    [Fact]
    public void PulsesComeFasterAsTheTargetNears()
    {
        Assert.Equal(Beacon.NearIntervalMilliseconds, Beacon.IntervalMilliseconds(Beacon.ArrivalDistance));
        Assert.Equal(Beacon.NearIntervalMilliseconds, Beacon.IntervalMilliseconds(0));
        Assert.Equal(Beacon.FarIntervalMilliseconds, Beacon.IntervalMilliseconds(Nearby.Radius));
        Assert.Equal(Beacon.FarIntervalMilliseconds, Beacon.IntervalMilliseconds(Nearby.Radius * 10));
        Assert.True(Beacon.IntervalMilliseconds(1000) < Beacon.IntervalMilliseconds(2000));
    }

    [Fact]
    public void StartSaysTheTargetAndPulsesTowardIt()
    {
        beacon.Start(Person(1000, 0));
        beacon.Tick();

        Assert.True(beacon.Active);
        Assert.Equal(["Guiding to Testa"], Said());
        var tone = Assert.Single(cues.Held);
        Assert.Equal(-1, tone.Pan, 6);
        Assert.Equal(Beacon.Hertz(Math.PI / 2), tone.Hertz, 6);
        Assert.Equal(Beacon.Volume, tone.Volume);
    }

    [Fact]
    public void EachPulseEndsThenTheNextComesAfterTheInterval()
    {
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        var interval = Beacon.IntervalMilliseconds(1000);

        now += Beacon.PulseMilliseconds - 1;
        beacon.Tick();
        Assert.Equal(0, cues.Silenced);
        now += 1;
        beacon.Tick();
        Assert.Equal(1, cues.Silenced);

        now = 1000 + interval - 1;
        beacon.Tick();
        Assert.Single(cues.Held);
        now += 1;
        beacon.Tick();
        Assert.Equal(2, cues.Held.Count);
    }

    // Without a camera read, the facing stands in.
    [Fact]
    public void ThePanFollowsThePlayerTurningWhenTheCameraIsUnread()
    {
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        field.Pose = new FieldPose(0, 0, 0, -Math.PI / 2);
        now += Beacon.IntervalMilliseconds(1000);
        beacon.Tick();

        Assert.Equal(0, cues.Held[0].Pan, 6);
        Assert.Equal(-1, cues.Held[1].Pan, 6);
    }

    // The camera's heading, as the facing's: the target's bearing is measured against what is ahead on screen (#183,
    // by ear 2026-11-21), so the player facing elsewhere doesn't move the beep.
    [Theory]
    [InlineData(0, 1000, 0, Beacon.AheadHertz)]
    [InlineData(0, -1000, 0, Beacon.BehindHertz)]
    [InlineData(-1000, 0, 1, (Beacon.AheadHertz + Beacon.BehindHertz) / 2)]
    [InlineData(1000, 0, -1, (Beacon.AheadHertz + Beacon.BehindHertz) / 2)]
    public void TheBeepFollowsTheCameraNotTheFacing(float x, float z, double pan, double hertz)
    {
        // The player faces left of the camera (headings grow counter-clockwise, so +x is left); the camera looks along +z.
        field.Pose = new FieldPose(0, 0, 0, Math.PI / 2, CameraHeading: 0);
        beacon.Start(Person(x, z));
        beacon.Tick();

        var tone = Assert.Single(cues.Held);
        Assert.Equal(pan, tone.Pan, 6);
        Assert.Equal(hertz, tone.Hertz, 6);
    }

    [Fact]
    public void ThePanFollowsTheCameraTurning()
    {
        field.Pose = new FieldPose(0, 0, 0, 0, CameraHeading: 0);
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        // The camera swings to look right (heading −π/2) while the player stands: the target is now on screen left.
        field.Pose = new FieldPose(0, 0, 0, 0, CameraHeading: -Math.PI / 2);
        now += Beacon.IntervalMilliseconds(1000);
        beacon.Tick();

        Assert.Equal(0, cues.Held[0].Pan, 6);
        Assert.Equal(-1, cues.Held[1].Pan, 6);
    }

    [Fact]
    public void ArrivingWithinTheArrivalDistanceStopsAndSaysSo()
    {
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        field.Pose = new FieldPose(0, 0, (float)(1000 - Beacon.ArrivalDistance + 1), 0);
        now += Beacon.PollMilliseconds;
        beacon.Tick();

        Assert.False(beacon.Active);
        Assert.Equal(["Guiding to Testa", "Reached Testa"], Said());
        Assert.Equal(1, cues.Silenced);
    }

    [Theory]
    [InlineData(FieldKind.Person, 0, 0, 100, true)]
    [InlineData(FieldKind.Person, 0, 0, 151, false)]
    [InlineData(FieldKind.Chest, 0, 0, 250, true)]
    [InlineData(FieldKind.Chest, 0, 0, -250, false)]
    public void ArrivedIsTheReachTestOrTheArrivalDistance(FieldKind kind, float x, float y, float z, bool arrived)
    {
        var thing = Nearby.List(new FieldView(0, 0, 0, 0, [new FieldUnit(kind, x, y, z)])).Single();

        Assert.Equal(arrived, Beacon.Arrived(thing));
    }

    [Fact]
    public void TheDebugLogSaysWhereTheTargetAndThePlayerStoodOnArrival()
    {
        // #183, by ear 2026-11-22: "Reached" a person by some crates. The log names the target's position and the
        // player's at arrival, so a session tells a wrong unit position from a wrong arrival test.
        var log = new List<string>();
        var logged = new Beacon(field, cues, announcer, () => now, log.Add);
        logged.Start(Person(0, 1000));
        logged.Tick();
        field.Pose = new FieldPose(0, 0, 900, 0);
        now += Beacon.PollMilliseconds;
        logged.Tick();

        Assert.Equal(
        [
            "beacon start kind=Person at=(0,0,1000) named=True",
            "beacon reached kind=Person at=(0,0,1000) player=(0,0,900) distance=100 inReach=False",
        ], log);
    }

    [Fact]
    public void TheGamesReachTestAlsoCountsAsArrival()
    {
        beacon.Start(new BeaconTarget(new FieldUnit(FieldKind.Chest, 0, 0, 250), null));
        beacon.Tick();

        Assert.False(beacon.Active);
        Assert.Equal(["Guiding to Chest", "Reached Chest"], Said());
        Assert.Empty(cues.Held);
    }

    [Fact]
    public void LeavingFreeRoamStopsWithoutAWord()
    {
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        field.Roaming = false;
        now += Beacon.PollMilliseconds;
        beacon.Tick();

        Assert.False(beacon.Active);
        Assert.Equal(["Guiding to Testa"], Said());
        Assert.Equal(1, cues.Silenced);
    }

    [Fact]
    public void AnUnreadablePoseOrAThrowingReadStops()
    {
        beacon.Start(Person(0, 1000));
        field.Pose = null;
        beacon.Tick();
        Assert.False(beacon.Active);

        field.Pose = new FieldPose(0, 0, 0, 0);
        beacon.Start(Person(0, 1000));
        field.Throws = true;
        beacon.Tick();
        Assert.False(beacon.Active);
        Assert.Empty(cues.Held);
    }

    [Fact]
    public void StopCancelsAndSaysSo()
    {
        beacon.Start(Person(0, 1000));
        beacon.Tick();
        beacon.Stop();
        beacon.Tick();

        Assert.False(beacon.Active);
        Assert.Equal(["Guiding to Testa", "Guidance off"], Said());
        Assert.Single(cues.Held);
        Assert.Equal(1, cues.Silenced);
    }

    [Fact]
    public void TheFieldIsReadAtMostOncePerPoll()
    {
        beacon.Start(Person(0, 1000));
        for (var i = 0; i < 10; i++)
        {
            beacon.Tick();
            now += 5;
        }

        Assert.Equal(1, field.PoseReads);
        Assert.Equal(1, field.FreeRoamReads);
    }

    [Fact]
    public void AnIdleBeaconReadsNothingAndPlaysNothing()
    {
        beacon.Tick();
        beacon.Stop();

        Assert.Equal(0, field.PoseReads + field.FreeRoamReads);
        Assert.Empty(cues.Held);
        Assert.Equal(0, cues.Silenced);
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ASteadyTickAllocatesNothing()
    {
        beacon.Start(Person(0, 3000));
        beacon.Tick();
        now += Beacon.PulseMilliseconds;
        beacon.Tick();
        now += 1;
        beacon.Tick();

        var before = GC.GetAllocatedBytesForCurrentThread();
        // Long enough to take a poll between pulses.
        for (var i = 0; i < 40; i++)
        {
            now += 2;
            beacon.Tick();
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    // #690: with a planner, the beep points at the route's next corner, not through the wall at the target.
    [Fact]
    public void WithARouteTheBeepPointsAtTheNextCorner()
    {
        // The target is to the left (+x); the route first goes ahead (+z) around something.
        var planner = new StubPlanner(RouteOutcome.Found, new(0, 0, 1000), new(1000, 0, 0));
        var routed = Routed(planner);

        routed.Start(Person(1000, 0));
        routed.Tick();

        var tone = Assert.Single(cues.Held);
        Assert.Equal(Beacon.AheadHertz, tone.Hertz, 6);
        Assert.Equal(0, tone.Pan, 6);
        Assert.Equal(1, planner.Plans);
        Assert.Equal(["Guiding to Testa"], Said());
    }

    [Fact]
    public void ACornerNearTheTargetIsNotReached()
    {
        // The first corner is beside the player: the beacon moves on to the next, and says nothing reached.
        var routed = Routed(new StubPlanner(RouteOutcome.Found, new(0, 0, 50), new(0, 0, 1000)));

        routed.Start(Person(0, 1000));
        routed.Tick();

        Assert.True(routed.Active);
        Assert.Equal(Beacon.AheadHertz, Assert.Single(cues.Held).Hertz, 6);
        Assert.Equal(["Guiding to Testa"], Said());
    }

    [Theory]
    [InlineData(RouteOutcome.NoPath)]
    [InlineData(RouteOutcome.NoNavMesh)]
    [InlineData(RouteOutcome.OffMesh)]
    public void WithNoRouteItPointsStraightAndSaysSoOnce(RouteOutcome outcome)
    {
        var planner = new StubPlanner(outcome);
        var routed = Routed(planner);

        routed.Start(Person(1000, 0));
        routed.Tick();
        now += RouteFollower.RetryMilliseconds;
        routed.Tick();

        Assert.Equal(2, planner.Plans);
        Assert.Equal(["Guiding to Testa", Beacon.NoPathText], Said());
        Assert.Equal(Beacon.Hertz(Math.PI / 2), cues.Held[0].Hertz, 6);
        Assert.False(announcer.Said[1].Interrupt);
    }

    [Fact]
    public void WhileTheNavMeshLoadsItPointsStraightSilently()
    {
        var routed = Routed(new StubPlanner(RouteOutcome.Loading));

        routed.Start(Person(1000, 0));
        routed.Tick();

        Assert.Equal(["Guiding to Testa"], Said());
        Assert.Equal(Beacon.Hertz(Math.PI / 2), Assert.Single(cues.Held).Hertz, 6);
    }

    [Fact]
    public void APlanStillRunningLeavesTheBeepStraightUntilItEnds()
    {
        var plan = new TaskCompletionSource<RoutePlan>();
        var routed = new Beacon(field, cues, announcer, () => now, routes: new StubPlanner(RouteOutcome.NoPath),
            run: _ => plan.Task);

        routed.Start(Person(1000, 0));
        routed.Tick();
        plan.SetResult(new RoutePlan(RouteOutcome.Found, [new(0, 0, 1000), new(1000, 0, 0)], ""));
        now += Beacon.IntervalMilliseconds(1000);
        routed.Tick();

        Assert.Equal(Beacon.Hertz(Math.PI / 2), cues.Held[0].Hertz, 6);
        Assert.Equal(Beacon.AheadHertz, cues.Held[1].Hertz, 6);
    }

    [Fact]
    public void StrayingFromTheRoutePlansAgain()
    {
        var planner = new StubPlanner(RouteOutcome.Found, new(0, 0, 1000), new(1000, 0, 1000));
        var routed = Routed(planner);

        routed.Start(Person(1000, 1000));
        routed.Tick();
        field.Pose = new FieldPose(-500, 0, 500, 0);
        now += Beacon.PollMilliseconds;
        routed.Tick();

        Assert.Equal(2, planner.Plans);
    }

    private Beacon Routed(IRoutePlanner planner) =>
        new(field, cues, announcer, () => now, routes: planner, run: plan => Task.FromResult(plan()));

    private sealed class StubPlanner(RouteOutcome outcome, params Vector3[] corners) : IRoutePlanner
    {
        public int Plans { get; private set; }

        public RoutePlan Plan(int? mapId, Vector3 start, Vector3 target)
        {
            Plans++;
            return new(outcome, outcome == RouteOutcome.Found ? corners : [], "");
        }
    }

    private sealed class PoseField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldPose? Pose { get; set; } = new FieldPose(0, 0, 0, 0);

        public bool Throws { get; set; }

        public int FreeRoamReads { get; private set; }

        public int PoseReads { get; private set; }

        public bool InFreeRoam()
        {
            FreeRoamReads++;
            return Throws ? throw new InvalidOperationException("stand-in failure") : Roaming;
        }

        public FieldView? Read() => null;

        public int? MapId() => null;

        public FieldPose? ReadPose()
        {
            PoseReads++;
            return Pose;
        }
    }
}

/// <summary>A cue player that remembers the tones held on the guidance channel and how often it was silenced.</summary>
internal sealed class RecordingCues : ICuePlayer
{
    private readonly List<HeldTone> held = new(64);

    public IReadOnlyList<HeldTone> Held => held;

    public int Silenced { get; private set; }

    public void Play(Cue cue)
    {
    }

    public void Play(Cue cue, double pan)
    {
    }

    public void Hold(ToneChannel channel, HeldTone tone)
    {
        Assert.Equal(ToneChannel.Guidance, channel);
        held.Add(tone);
    }

    public void Silence(ToneChannel channel)
    {
        Assert.Equal(ToneChannel.Guidance, channel);
        Silenced++;
    }
}
