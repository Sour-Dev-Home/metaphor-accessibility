using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>What the beacon guides to: a unit as the nearby list read it, and the door reach value read with it.</summary>
/// <param name="Unit">The target, at the position it had when picked.</param>
/// <param name="DoorReachValue">As <see cref="FieldView.DoorReachValue"/>, for the target's reach test.</param>
public sealed record BeaconTarget(FieldUnit Unit, int? DoorReachValue);

/// <summary>A repeating tone toward a target picked from the nearby list (#183): panned by its bearing from the camera's
/// heading (what is ahead on screen; the player's facing when the camera isn't read), higher ahead than behind (stereo
/// alone can't tell them apart), and pulsing faster as the target nears. With a route planner it points at the next
/// corner of a route over the navmesh (#690, <see cref="RouteFollower"/>), planned off the game's thread; with no route
/// it points straight at the target, and says once when that is because none was found. It stops on arrival at the
/// target itself (the game's reach test for the target's kind, or within <see cref="ArrivalDistance"/>), on
/// <see cref="Stop"/>, and silently on leaving free roam or when the player can't be read. <see cref="Tick"/> is called
/// on every input read; it reads the field at most once per <see cref="PollMilliseconds"/>, and allocates nothing
/// between a pulse's start and the next except when a route is asked for. Its keys are gated by
/// <see cref="FieldFeature.Guidance"/> in <see cref="FieldControls"/> (#661).</summary>
[FeatureSetting<FieldFeature>(FieldFeature.Guidance, FieldFeature.GuidancePause)]
public sealed class Beacon
{
    /// <summary>How often the field is read while guiding.</summary>
    public const long PollMilliseconds = 50;

    /// <summary>How long each pulse sounds.</summary>
    public const long PulseMilliseconds = 80;

    /// <summary>The ground distance, in game units, that counts as arrived for any kind. Running covers about 700 units a
    /// second (by-ear walk of 2026-11-21, on PR #643), so this is about a fifth of a second's run, and the doors' near
    /// reach.</summary>
    public const double ArrivalDistance = 150;

    /// <summary>The gap between pulse starts at <see cref="ArrivalDistance"/> or nearer.</summary>
    public const long NearIntervalMilliseconds = 200;

    /// <summary>The gap between pulse starts at the nearby list's radius or farther.</summary>
    public const long FarIntervalMilliseconds = 1200;

    /// <summary>The pitch straight ahead: A5.</summary>
    public const double AheadHertz = 880;

    /// <summary>The pitch straight behind: A4, an octave down.</summary>
    public const double BehindHertz = 440;

    /// <summary>The pulses' volume, within the sound cue volume.</summary>
    public const int Volume = 80;

    /// <summary>How long free roam must have held after it ended before the beep plays again (#709).</summary>
    public const long FreeRoamSettleMilliseconds = 1000;

    /// <summary>What is said, once per start, when a route was asked for and none found (#690).</summary>
    public const string NoPathText = "No path found, pointing straight";

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly ICuePlayer cues;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Action<string>? log;
    private readonly IRoutePlanner? routes;
    private readonly Func<Func<RoutePlan>, Task<RoutePlan>> run;
    private readonly RouteFollower follower = new();
    private Task<RoutePlan>? pending;
    private Vector3 pendingFrom;
    private bool saidStraight;
    private readonly FreeRoamGate roam;
    private readonly Func<bool> pauseOn;
    private int? map;
    private bool paused;
    private BeaconTarget? target;
    private volatile bool active;
    private bool polled;
    private long polledAt;
    private double distance;
    private double bearing;
    private bool sounding;
    private long pulseEndsAt;
    private long nextPulseAt;

    /// <summary>A beacon that reads <paramref name="field"/>, plays on <paramref name="cues"/>' guidance channel, speaks
    /// through <paramref name="announcer"/>, keeps time by <paramref name="milliseconds"/> and writes the target's and the
    /// player's positions, as numbers, to <paramref name="log"/> on a start and an arrival when there is one (Debug
    /// builds), with each route's outcome and what it read. With <paramref name="routes"/> it guides along routes that
    /// <paramref name="run"/> plans (on the thread pool unless given). <paramref name="pauseOn"/> says whether leaving
    /// free roam pauses the beep (<see cref="FieldFeature.GuidancePause"/>, on when null) rather than ending it.</summary>
    public Beacon(
        IFieldReads field, ICuePlayer cues, IAnnouncer announcer, Func<long> milliseconds, Action<string>? log = null,
        IRoutePlanner? routes = null, Func<Func<RoutePlan>, Task<RoutePlan>>? run = null, Func<bool>? pauseOn = null)
    {
        this.pauseOn = pauseOn ?? (() => true);
        this.routes = routes;
        this.run = run ?? Task.Run;
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.field = field;
        this.cues = cues;
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.log = log;
        roam = new FreeRoamGate(field, FreeRoamSettleMilliseconds, milliseconds);
    }

    /// <summary>Whether the beacon is guiding.</summary>
    public bool Active => active;

    /// <summary>Whether a thing already counts as reached: the game's reach test for its kind, or within
    /// <see cref="ArrivalDistance"/>.</summary>
    public static bool Arrived(NearbyThing thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        return Arrived(thing.Distance, thing.InReach);
    }

    /// <summary>As <see cref="Arrived(NearbyThing)"/>, for a ground distance and the reach test's answer.</summary>
    public static bool Arrived(double distance, bool inReach) => inReach || distance <= ArrivalDistance;

    /// <summary>From -1 (left) to +1 (right) for a bearing, left positive: full left or right at the side, centred
    /// ahead and behind.</summary>
    public static double Pan(double bearing) => -Math.Sin(bearing);

    /// <summary>The pitch for a bearing: <see cref="AheadHertz"/> ahead, <see cref="BehindHertz"/> behind, halfway at
    /// the side.</summary>
    public static double Hertz(double bearing) =>
        BehindHertz + ((AheadHertz - BehindHertz) * (1 + Math.Cos(bearing)) / 2);

    /// <summary>The gap between pulse starts for a ground distance: straight-line from
    /// <see cref="NearIntervalMilliseconds"/> at <see cref="ArrivalDistance"/> to <see cref="FarIntervalMilliseconds"/>
    /// at the nearby list's radius, held beyond both.</summary>
    public static long IntervalMilliseconds(double distance)
    {
        var t = Math.Clamp((distance - ArrivalDistance) / (Nearby.Radius - ArrivalDistance), 0, 1);
        return NearIntervalMilliseconds + (long)Math.Round((FarIntervalMilliseconds - NearIntervalMilliseconds) * t);
    }

    /// <summary>Starts guiding to <paramref name="next"/>, in place of any target before, and says so.</summary>
    public void Start(BeaconTarget next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var startMap = MapOrNull();
        lock (gate)
        {
            map = startMap;
            paused = false;
            target = next;
            follower.Reset();
            pending = null;
            saidStraight = false;
            polled = false;
            sounding = false;
            nextPulseAt = milliseconds();
            active = true;
        }
        var unit = next.Unit;
        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"beacon start kind={unit.Kind} at=({unit.X:0.#},{unit.Y:0.#},{unit.Z:0.#}) named={unit.Name is not null}"));
        announcer.Announce(new Announcement($"Guiding to {Name(next)}", Interrupt: true));
    }

    /// <summary>The player's cancel: stops guiding and says so. Does nothing when idle.</summary>
    public void Stop()
    {
        if (End())
        {
            announcer.Announce(new Announcement("Guidance off", Interrupt: true));
        }
    }

    /// <summary>Moves the beacon on to now: reads the field when a poll is due, stops on arrival or on leaving free
    /// roam, and starts or ends a pulse when one is due. Returns at once when idle. A read that throws stops it
    /// silently, so nothing reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it stops the beacon.")]
    public void Tick()
    {
        if (!active)
        {
            return;
        }
        string? reached = null;
        var straight = false;
        try
        {
            lock (gate)
            {
                if (target is not { } guided)
                {
                    return;
                }
                var now = milliseconds();
                if (!polled || now - polledAt >= PollMilliseconds)
                {
                    polled = true;
                    polledAt = now;
                    // Out of free roam the beep pauses and comes back when free roam has held (#702, #709); on another
                    // map it ends.
                    if (!pauseOn())
                    {
                        // The pause is its own setting while it is tested (#783): off, leaving free roam ends the
                        // beep as before.
                        paused = false;
                        if (!field.InFreeRoam())
                        {
                            EndLocked();
                            return;
                        }
                    }
                    else if (!roam.Open())
                    {
                        if (!paused)
                        {
                            paused = true;
                            sounding = false;
                            cues.Silence(ToneChannel.Guidance);
                            log?.Invoke("beacon pause: not free roam" + Locks());
                        }
                        return;
                    }
                    if (paused)
                    {
                        paused = false;
                        log?.Invoke("beacon resume: free roam held" + Locks());
                        nextPulseAt = now;
                        if (field.MapId() != map)
                        {
                            EndLocked();
                            return;
                        }
                    }
                    if (field.ReadPose() is not { } pose)
                    {
                        EndLocked();
                        return;
                    }
                    var (far, toward, inReach) = Nearby.Measure(pose, guided.Unit, guided.DoorReachValue);
                    if (Arrived(far, inReach))
                    {
                        EndLocked();
                        reached = Name(guided);
                        var unit = guided.Unit;
                        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                            $"beacon reached kind={unit.Kind} at=({unit.X:0.#},{unit.Y:0.#},{unit.Z:0.#}) player=({pose.X:0.#},{pose.Y:0.#},{pose.Z:0.#}) distance={far:0.#} inReach={inReach}"));
                        return;
                    }
                    distance = far;
                    // The beep's direction is from what is ahead on screen, the camera's heading (maintainer, by ear
                    // 2026-11-21 on PR #667); the facing, which the reach test needs, stands in when it isn't read.
                    var ahead = pose.CameraHeading ?? pose.Heading;
                    bearing = Aim(guided.Unit, pose, now, ref straight) is { } corner
                        ? Nearby.Normalised(Math.Atan2(corner.X - pose.X, corner.Z - pose.Z) - ahead)
                        : Nearby.Normalised(toward + pose.Heading - ahead);
                }
                if (paused)
                {
                    return;
                }
                if (sounding && now >= pulseEndsAt)
                {
                    cues.Silence(ToneChannel.Guidance);
                    sounding = false;
                }
                if (now >= nextPulseAt)
                {
                    cues.Hold(ToneChannel.Guidance, new HeldTone(Hertz(bearing), Pan(bearing), Volume));
                    sounding = true;
                    pulseEndsAt = now + PulseMilliseconds;
                    nextPulseAt = now + IntervalMilliseconds(distance);
                }
            }
        }
        catch (Exception)
        {
            End();
            return;
        }
        finally
        {
            if (reached is not null)
            {
                announcer.Announce(new Announcement($"Reached {reached}", Interrupt: true));
            }
            else if (straight)
            {
                announcer.Announce(new Announcement(NoPathText, Interrupt: false));
            }
        }
    }

    // The next corner of the route to guide toward, or null to guide straight: takes a finished plan, follows the
    // route, and asks for a new one when the follower wants it. Sets straight when a plan has found no route, the
    // first time since the start. Called under the gate.
    private Vector3? Aim(FieldUnit unit, FieldPose pose, long now, ref bool straight)
    {
        if (routes is null)
        {
            return null;
        }
        var player = new Vector3(pose.X, pose.Y, pose.Z);
        straight |= TakePlan(now);
        var step = follower.Step(player, now);
        if (step.Replan is { } why && pending is null)
        {
            Ask(routes, field.MapId(), player, new Vector3(unit.X, unit.Y, unit.Z));
            log?.Invoke($"beacon route asked: {why}");
            // A plan that finished at once (or a planner run inline) is followed from this poll.
            straight |= TakePlan(now);
            if (pending is null)
            {
                step = follower.Step(player, now);
            }
        }
        return step.Aim;
    }

    // Starts planning; its own method so the closure is allocated only when a route is asked for.
    private void Ask(IRoutePlanner planner, int? mapId, Vector3 player, Vector3 to)
    {
        pendingFrom = player;
        pending = run(() => planner.Plan(mapId, player, to));
    }

    // Hands a finished plan to the follower; true when it is the first since the start to find no route.
    private bool TakePlan(long now)
    {
        if (pending is not { IsCompleted: true } done)
        {
            return false;
        }
        pending = null;
        var plan = done.IsCompletedSuccessfully
            ? done.Result
            : RoutePlan.Failed(RouteOutcome.NoNavMesh, $"plan failed: {done.Exception?.InnerException?.GetType().Name}");
        follower.Accept(plan, pendingFrom, now);
        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"beacon route {plan.Outcome} corners={plan.Corners.Count} {plan.Detail}"));
        if (plan.Outcome is RouteOutcome.Found or RouteOutcome.Loading || saidStraight)
        {
            return false;
        }
        saidStraight = true;
        return true;
    }

    private static string Name(BeaconTarget guided) => guided.Unit.Name ?? Nearby.KindWord(guided.Unit.Says);

    private bool End()
    {
        lock (gate)
        {
            if (target is null)
            {
                return false;
            }
            EndLocked();
            return true;
        }
    }

    /// <summary>The map now, or null when it can't be read: a read that throws never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it reads as unknown.")]
    private int? MapOrNull()
    {
        try
        {
            return field.MapId();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // The player's lock values for a pause or resume log line (#702's probe); empty in a Release build.
    private string Locks() => log is not null && field.PlayerLocks() is { } locks ? " " + locks : "";

    private void EndLocked()
    {
        target = null;
        active = false;
        sounding = false;
        cues.Silence(ToneChannel.Guidance);
    }
}
