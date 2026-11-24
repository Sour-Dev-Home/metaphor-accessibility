using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Where a wall tone points, from the camera's horizontal angle (#830).</summary>
public enum WallDirection
{
    Ahead,
    Behind,
    Left,
    Right,
}

/// <summary>The wall tones' limits (#830): a wall farther than <paramref name="RangeUnits"/> (game units, about 100 a
/// step, as <see cref="Nearby.UnitsPerStep"/>) is silent, and a tone grows to <paramref name="Volume"/> (within the sound
/// cue volume) as its wall nears.</summary>
public readonly record struct WallLimits(int RangeUnits, int Volume)
{
    /// <summary>The defaults, the mod's own guess until a by-ear session tunes them: about three steps, and quieter
    /// than the sonar's nearest ping.</summary>
    public static WallLimits Default { get; } = new(300, 50);

    /// <summary>The limits the settings file's <c>limits.field</c> object gives: the range kept within 50 to 2000, the
    /// volume 0 to 100, each missing one the default.</summary>
    public static WallLimits From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            Math.Clamp(settings.Limit("field", "wallRange", Default.RangeUnits), 50, 2000),
            Math.Clamp(settings.Limit("field", "wallVolume", Default.Volume), 0, 100));
    }
}

/// <summary>Four looped tones for the walls ahead, behind, left and right of the camera's horizontal angle (#830), each
/// on its own tone channel, louder as its wall nears and silent beyond the range. Directions are the shared frame's
/// (<see cref="CameraFrame"/>, #823: the listener at the player, turned by the camera's horizontal angle, pitch
/// ignored): plain stereo pan, ahead higher than the sides and the sides higher than behind, since stereo alone can't
/// tell ahead from behind. The distance is the map's navmesh, <see cref="Core.Navigation.WalkGraph.DistanceToWall"/>
/// (docs/findings/field-wall-probe-raycast-static.md): no hook and no game call. It starts from the nearest cell, since
/// the player stands in the eroded border along a wall, and adds the agent radius the cells are shrunk by (#830). Its
/// edges also mark ledges and drops, and miss doors and people. Where the map has no navmesh the tones are
/// silent, said once per map and written to the Debug log.
/// <para>The idea of wall tones comes from Arcana, the Persona 3 Reload accessibility mod
/// (https://github.com/ChipsAhoiMcCoy/arcana, ChipsAhoiMcCoy, Zlib); idea only, no code copied (CREDITS.md).</para>
/// <para>The field controls call <see cref="Tick"/> on every keyboard read; it probes at most once per
/// <see cref="ProbeMilliseconds"/>, only while on and in free roam, and goes quiet at once otherwise. While another
/// navigation sound has the floor the tones play at <see cref="NavFloor.DuckedPercent"/>.</para></summary>
[FeatureSetting<FieldFeature>(FieldFeature.WallTones)]
public sealed class WallTones
{
    /// <summary>The least time between two probes: ten a second.</summary>
    public const long ProbeMilliseconds = 100;

    /// <summary>The least time between two probe lines in the Debug log: one a second.</summary>
    public const long LogProbeMilliseconds = 1000;

    /// <summary>How long free roam must have held before the tones play again, as the sonar.</summary>
    public const long FreeRoamSettleMilliseconds = 1000;

    /// <summary>What is said, once per map, when the map has no navmesh to probe.</summary>
    public const string NoNavMeshText = "No wall tones on this map";

    private static readonly WallDirection[] Directions = Enum.GetValues<WallDirection>();

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly ICuePlayer cues;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Func<bool> on;
    private readonly Func<WallLimits> limits;
    private readonly Func<int, Task<NavEdges?>> edges;
    private readonly NavFloor? floor;
    private readonly Action<string>? log;
    private readonly FreeRoamGate roam;
    private readonly int[] playing = new int[Directions.Length];
    private readonly CueSample?[] sounds = new CueSample?[Directions.Length];
    private readonly NavSamples samples;
    private readonly HashSet<int> saidNone = [];
    private int? loggedMap;
    private bool probed;
    private long probedAt;
    private bool probeLogged;
    private long probeLoggedAt;

    /// <summary>Wall tones that read <paramref name="field"/>, play on <paramref name="cues"/>' four wall channels,
    /// speak through <paramref name="announcer"/>, keep time by <paramref name="milliseconds"/>, play only while
    /// <paramref name="on"/> says so, read <paramref name="limits"/> on each probe, take each map's navmesh from
    /// <paramref name="edges"/>, give way as <paramref name="floor"/> says when there is one, and write each map's
    /// navmesh outcome to <paramref name="log"/> when there is one (Debug builds). Each direction loops its sound from
    /// <paramref name="samples"/> in place of its tone when it gives one (#841).</summary>
    public WallTones(
        IFieldReads field, ICuePlayer cues, IAnnouncer announcer, Func<long> milliseconds, Func<bool> on,
        Func<WallLimits> limits, Func<int, Task<NavEdges?>> edges, NavFloor? floor = null,
        Action<string>? log = null, NavSamples? samples = null)
    {
        this.samples = samples ?? NavSamples.None;
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(edges);
        this.field = field;
        this.cues = cues;
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.on = on;
        this.limits = limits;
        this.edges = edges;
        this.floor = floor;
        this.log = log;
        roam = new FreeRoamGate(field, FreeRoamSettleMilliseconds, milliseconds);
    }

    /// <summary>The tone channel a direction plays on.</summary>
    public static ToneChannel Channel(WallDirection direction) => direction switch
    {
        WallDirection.Ahead => ToneChannel.Wall,
        WallDirection.Behind => ToneChannel.WallBehind,
        WallDirection.Left => ToneChannel.WallLeft,
        _ => ToneChannel.WallRight,
    };

    /// <summary>A direction's bearing in the frame, left positive.</summary>
    public static double Bearing(WallDirection direction) => direction switch
    {
        WallDirection.Ahead => 0,
        WallDirection.Behind => Math.PI,
        WallDirection.Left => Math.PI / 2,
        _ => -Math.PI / 2,
    };

    /// <summary>A direction's pitch: ahead C4, the sides G3, behind C3, all below the sonar and the beep so the walls
    /// sit under them. Placeholders, the mod's own choice.</summary>
    public static double Hertz(WallDirection direction) => direction switch
    {
        WallDirection.Ahead => 262,
        WallDirection.Behind => 131,
        _ => 196,
    };

    /// <summary>The volume for a wall <paramref name="distance"/> away: the limit's volume at the wall, falling in a
    /// straight line to 0 at its range and beyond.</summary>
    public static int Volume(double distance, WallLimits limits)
    {
        if (limits.RangeUnits <= 0 || double.IsNaN(distance))
        {
            return 0;
        }
        var t = Math.Clamp(distance / limits.RangeUnits, 0, 1);
        return (int)Math.Round(limits.Volume * (1 - t));
    }

    /// <summary>Probes the four directions when a probe is due, and sets each tone. A read that throws silences them, so
    /// it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it goes quiet.")]
    public void Tick()
    {
        string? say = null;
        lock (gate)
        {
            try
            {
                if (!on() || !roam.Open())
                {
                    probed = false;
                    Quiet();
                    return;
                }
                var now = milliseconds();
                if (probed && now - probedAt < ProbeMilliseconds)
                {
                    return;
                }
                probed = true;
                probedAt = now;
                say = Probe();
            }
            catch (Exception)
            {
                probed = false;
                Quiet();
                return;
            }
        }
        if (say is not null)
        {
            announcer.Announce(new Announcement(say, Interrupt: false));
        }
    }

    // Sets the four tones from the navmesh; returns what to say, if anything. Called under the gate.
    private string? Probe()
    {
        if (field.MapId() is not { } map)
        {
            Quiet();
            return null;
        }
        var found = edges(map);
        if (!found.IsCompleted)
        {
            Quiet();
            return null;
        }
        if (found is not { IsCompletedSuccessfully: true, Result: { } walls })
        {
            Quiet();
            Log(map, "none");
            return saidNone.Add(map) ? NoNavMeshText : null;
        }
        Log(map, "loaded");
        if (field.ReadPose() is not { } pose)
        {
            Quiet();
            return null;
        }
        var limit = limits();
        var share = floor?.WallPercent() ?? 100;
        var ahead = CameraFrame.Ahead(pose);
        var position = new Vector3(pose.X, pose.Y, pose.Z);
        var distances = new float?[Directions.Length];
        foreach (var direction in Directions)
        {
            var heading = ahead + Bearing(direction);
            var edge = walls.Distance(position, new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading)),
                limit.RangeUnits);
            distances[(int)direction] = edge;
            Set(direction, edge is { } far ? Volume(far, limit) * share / 100 : 0);
        }
        LogProbe(map, walls, position, distances);
        return null;
    }

    // Writes what a probe found, at most once per LogProbeMilliseconds (#830): the map, the start cell and the four
    // distances, ahead, behind, left, right, "-" for none.
    private void LogProbe(int map, NavEdges walls, Vector3 position, float?[] distances)
    {
        var now = milliseconds();
        if (log is null || (probeLogged && now - probeLoggedAt < LogProbeMilliseconds))
        {
            return;
        }
        probeLogged = true;
        probeLoggedAt = now;
        var start = walls.Start(position) is { } cell
            ? string.Create(CultureInfo.InvariantCulture, $"{cell.Tile}/{cell.X},{cell.Y}")
            : "none";
        var each = string.Join(' ', distances.Select(d => d is { } v ? v.ToString("0", CultureInfo.InvariantCulture) : "-"));
        log(string.Create(CultureInfo.InvariantCulture,
            $"wall probe map={map} at={position.X:0},{position.Y:0},{position.Z:0} start={start} ahead/behind/left/right={each}"));
    }

    // Holds a direction's tone at a volume, or silences it at 0; only a change reaches the cue player.
    private void Set(WallDirection direction, int volume)
    {
        var i = (int)direction;
        var sample = volume > 0 ? samples.For(direction) : null;
        if (playing[i] == volume && sounds[i] == sample)
        {
            return;
        }
        playing[i] = volume;
        sounds[i] = sample;
        if (volume > 0)
        {
            cues.Hold(Channel(direction),
                new HeldTone(Hertz(direction), CameraFrame.Pan(Bearing(direction)), volume, sample, Loops: true));
        }
        else
        {
            cues.Silence(Channel(direction));
        }
    }

    private void Log(int map, string outcome)
    {
        if (log is not null && loggedMap != map)
        {
            loggedMap = map;
            log(string.Create(CultureInfo.InvariantCulture, $"wall tones map={map} navmesh={outcome}"));
        }
    }

    // Silences every tone. Called under the gate.
    private void Quiet()
    {
        foreach (var direction in Directions)
        {
            Set(direction, 0);
        }
    }
}
