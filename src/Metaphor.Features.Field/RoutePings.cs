using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Metaphor.Core.Cues;
using Metaphor.Core.Navigation;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>What a route ping says about the way to the place (#831).</summary>
public enum RoutePingKind
{
    /// <summary>Straight there: the route runs straight, or none was planned (no navmesh, off it, or not yet read).
    /// </summary>
    Straight,

    /// <summary>The route bends: the ping comes from its next corner (a path ping).</summary>
    Around,

    /// <summary>The navmesh holds no path there; the ping comes from the place itself.</summary>
    NoPath,
}

/// <summary>The route ping's limit (#831): its <paramref name="Volume"/> at the place, within the sound cue volume; it
/// falls with distance as the sonar's pings do.</summary>
public readonly record struct RoutePingLimits(int Volume)
{
    /// <summary>The default: as loud as the sonar's nearest ping.</summary>
    public static RoutePingLimits Default { get; } = new(Sonar.NearVolume);

    /// <summary>The limit the settings file's <c>limits.field</c> object gives, kept within 0 to 100, or the default.
    /// </summary>
    public static RoutePingLimits From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Math.Clamp(settings.Limit("field", "routePingVolume", Default.Volume), 0, 100));
    }
}

/// <summary>A ping toward the place picked in the nearby list (#831), each time the list steps to one: from the place
/// itself along the straight line by default, or, where the beacon's route planner (#690, <see cref="IRoutePlanner"/>)
/// finds a route that bends, from the route's next corner (a path ping,
/// docs/findings/field-route-query-for-path-pings-static.md). Its sound says which (<see cref="RoutePingKind"/>), its pan
/// is the shared frame's (<see cref="CameraFrame"/>, #823) and its volume falls with the straight-line distance to the
/// place. A route is planned off the game's thread; the ping waits for it at most
/// <see cref="PlanWaitMilliseconds"/>, then goes straight. It plays on its own tone channel and takes the floor from the
/// sonar while it sounds (<see cref="NavFloor"/>).
/// <para>The idea of a route ping comes from Arcana, the Persona 3 Reload accessibility mod
/// (https://github.com/ChipsAhoiMcCoy/arcana, ChipsAhoiMcCoy, Zlib), whose ping says only the kind of route; idea only,
/// no code copied (CREDITS.md).</para>
/// <para>The field controls call <see cref="Focus"/> on each step and <see cref="Tick"/> on every keyboard read. It plays
/// only while on and in free roam, and goes quiet at once otherwise.</para></summary>
[FeatureSetting<FieldFeature>(FieldFeature.RoutePings)]
public sealed class RoutePings
{
    /// <summary>The longest a ping waits for its route before it goes straight.</summary>
    public const long PlanWaitMilliseconds = 400;

    private static readonly ToneStep[] StraightSteps = [new(1319, 50), new(0, 30), new(1319, 50)];
    private static readonly ToneStep[] AroundSteps = [new(784, 60), new(1175, 60)];
    private static readonly ToneStep[] NoPathSteps = [new(220, 150)];

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly ICuePlayer cues;
    private readonly Func<long> milliseconds;
    private readonly Func<bool> on;
    private readonly Func<RoutePingLimits> limits;
    private readonly IRoutePlanner? routes;
    private readonly Func<Func<RoutePlan>, Task<RoutePlan>> run;
    private readonly NavFloor? floor;
    private readonly Action<string>? log;
    private readonly FreeRoamGate roam;
    private FieldUnit? place;
    private Task<RoutePlan>? plan;
    private Vector3 plannedFrom;
    private long focusedAt;
    private IReadOnlyList<ToneStep> steps = [];
    private int step = -1;
    private long stepEndsAt;
    private double pan;
    private int volume;

    /// <summary>Route pings that read <paramref name="field"/>, play on <paramref name="cues"/>' route channel, keep time
    /// by <paramref name="milliseconds"/>, play only while <paramref name="on"/> says so, read <paramref name="limits"/>
    /// on each ping, plan with <paramref name="routes"/> when given, inside <paramref name="run"/> (the thread pool
    /// unless given), take the floor through <paramref name="floor"/> when there is one, and write each ping to
    /// <paramref name="log"/> when there is one (Debug builds).</summary>
    public RoutePings(
        IFieldReads field, ICuePlayer cues, Func<long> milliseconds, Func<bool> on, Func<RoutePingLimits> limits,
        IRoutePlanner? routes = null, Func<Func<RoutePlan>, Task<RoutePlan>>? run = null, NavFloor? floor = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(limits);
        this.field = field;
        this.cues = cues;
        this.milliseconds = milliseconds;
        this.on = on;
        this.limits = limits;
        this.routes = routes;
        this.run = run ?? Task.Run;
        this.floor = floor;
        this.log = log;
        roam = new FreeRoamGate(field);
    }

    /// <summary>A kind's notes, played one after another (a 0 Hz note is a gap): straight, two quick high notes; around,
    /// two notes rising; no path, one low note. Placeholders, the mod's own choice, apart from the sonar's and the
    /// beep's.</summary>
    public static IReadOnlyList<ToneStep> Steps(RoutePingKind kind) => kind switch
    {
        RoutePingKind.Straight => StraightSteps,
        RoutePingKind.Around => AroundSteps,
        _ => NoPathSteps,
    };

    /// <summary>The volume for a place <paramref name="distance"/> away: the limit's at <see cref="Beacon.ArrivalDistance"/>
    /// or nearer, falling as the sonar's pings do to the nearby list's radius.</summary>
    public static int Volume(double distance, RoutePingLimits limits) =>
        limits.Volume * Sonar.Volume(distance) / Sonar.NearVolume;

    /// <summary>The list has stepped to <paramref name="unit"/>: plans its route when there is a planner, and pings once
    /// the route is known or the wait is over. A newer step replaces one still waiting.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it doesn't ping.")]
    public void Focus(FieldUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        lock (gate)
        {
            try
            {
                if (!on() || !roam.Open() || field.ReadPose() is not { } pose)
                {
                    return;
                }
                place = unit;
                focusedAt = milliseconds();
                plannedFrom = new Vector3(pose.X, pose.Y, pose.Z);
                plan = routes is { } planner ? Ask(planner, field.MapId(), plannedFrom, unit) : null;
            }
            catch (Exception)
            {
                place = null;
                plan = null;
            }
        }
        Tick();
    }

    /// <summary>Moves the ping on to now: starts one whose route is known or whose wait is over, and plays its notes.
    /// A read that throws silences it, so it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it goes quiet.")]
    public void Tick()
    {
        lock (gate)
        {
            try
            {
                if (!on() || !roam.Open())
                {
                    place = null;
                    plan = null;
                    Quiet();
                    return;
                }
                var now = milliseconds();
                if (place is { } unit && (plan is null or { IsCompleted: true } || now - focusedAt >= PlanWaitMilliseconds))
                {
                    place = null;
                    Start(unit, plan, now);
                    plan = null;
                }
                Play(now);
            }
            catch (Exception)
            {
                place = null;
                plan = null;
                Quiet();
            }
        }
    }

    // Starts planning; its own method so the closure is allocated only on a step.
    private Task<RoutePlan> Ask(IRoutePlanner planner, int? mapId, Vector3 from, FieldUnit unit) =>
        run(() => planner.Plan(mapId, from, new Vector3(unit.X, unit.Y, unit.Z)));

    // Picks the ping's kind and where it comes from, from the player now. Called under the gate.
    private void Start(FieldUnit unit, Task<RoutePlan>? planned, long now)
    {
        if (field.ReadPose() is not { } pose)
        {
            return;
        }
        var outcome = planned is { IsCompletedSuccessfully: true, Result: { } done } ? done : null;
        var kind = RoutePingKind.Straight;
        var aim = new Vector3(unit.X, unit.Y, unit.Z);
        if (outcome is { Outcome: RouteOutcome.Found } found)
        {
            var follower = new RouteFollower();
            follower.Accept(found, plannedFrom, now);
            if (follower.Step(new Vector3(pose.X, pose.Y, pose.Z), now).Aim is { } corner &&
                follower.Corner < found.Corners.Count - 1)
            {
                kind = RoutePingKind.Around;
                aim = corner;
            }
        }
        else if (outcome is { Outcome: RouteOutcome.NoPath })
        {
            kind = RoutePingKind.NoPath;
        }
        var dx = unit.X - pose.X;
        var dz = unit.Z - pose.Z;
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        steps = Steps(kind);
        step = -1;
        stepEndsAt = now;
        pan = CameraFrame.Pan(CameraFrame.Bearing(pose, aim.X, aim.Z));
        volume = Volume(distance, limits());
        floor?.Hold();
        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"route ping kind={kind} outcome={outcome?.Outcome.ToString() ?? "none"} aim=({aim.X:0.#},{aim.Z:0.#}) distance={distance:0.#}"));
    }

    // Plays the ping's next note when the last has ended, and silences it after its last. Called under the gate.
    private void Play(long now)
    {
        if (steps.Count == 0 || now < stepEndsAt)
        {
            return;
        }
        step++;
        if (step >= steps.Count)
        {
            Quiet();
            return;
        }
        var note = steps[step];
        stepEndsAt = now + note.Milliseconds;
        if (note.Hertz <= 0 || volume <= 0)
        {
            cues.Silence(ToneChannel.Route);
        }
        else
        {
            cues.Hold(ToneChannel.Route, new HeldTone(note.Hertz, pan, volume));
        }
    }

    // Ends a ping that is playing. Called under the gate.
    private void Quiet()
    {
        if (steps.Count == 0)
        {
            return;
        }
        steps = [];
        step = -1;
        cues.Silence(ToneChannel.Route);
    }
}
