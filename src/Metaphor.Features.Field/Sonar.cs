using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>What the sonar sounds a thing as (#827): one sound per kind.</summary>
public enum SonarKind
{
    People,
    Objects,
    Exits,
}

/// <summary>The field sonar (#827): it reads the nearby list's data and plays short pings for the people, objects and
/// exits within the list's radius, each with its kind's pitch. By default it plays one at a time, every
/// <see cref="OneAtATimeMilliseconds"/>, cycling through what's nearby nearest first, so many people at once never ping
/// together (the maintainer's pick after the 2026-11-23 by-ear session). <see cref="PingsPerSweep"/> above 1 plays the
/// nearest few in a row instead, every <see cref="SweepMilliseconds"/>, at most <see cref="MaxPings"/>. Each ping is placed in the camera-relative frame
/// (<see cref="CameraFrame"/>, #823: the listener at the player, turned by the camera's horizontal angle, pitch ignored)
/// from where the player stands when it sounds: plain stereo pan, and louder nearer. It plays on its own tone channel
/// through the mod's cue output, as the guidance beep does; no new native library. The pitches are sine tones made at
/// run time: the cue player holds only sines, so the generated sound files (#839, <c>tools/Metaphor.NavSounds</c>)
/// render these same pitches and lengths for listening outside the game, and aren't loaded. It yields to the guidance
/// beep and to a route ping (<see cref="NavFloor"/>). Debug builds log the first time each thing sounds and how far off
/// it was, for tuning the radius.
/// <para>The idea of a field sonar comes from Arcana, the Persona 3 Reload accessibility mod
/// (https://github.com/ChipsAhoiMcCoy/arcana, ChipsAhoiMcCoy, Zlib), seen in a player-shared log; idea only, no code
/// copied (CREDITS.md).</para>
/// <para>The field controls call <see cref="Tick"/> on every keyboard read. It pings only while it is on and free roam
/// has held for <see cref="FreeRoamSettleMilliseconds"/>, and silences a ping at once otherwise.</para></summary>
[FeatureSetting<FieldFeature>(FieldFeature.Sonar)]
public sealed class Sonar
{
    /// <summary>How often a sweep of the nearest few starts.</summary>
    public const long SweepMilliseconds = 2000;

    /// <summary>The gap between two pings one at a time. A guess until a by-ear session tunes it.</summary>
    public const long OneAtATimeMilliseconds = 700;

    /// <summary>The settings file's <c>limits.field.sonarPings</c>: how many things a sweep pings. 1, the default, is one
    /// at a time, cycling through what's nearby.</summary>
    public static SliderLimit PingsPerSweep { get; } = new("field", "sonarPings", 1, 1, MaxPings, 1)
    {
        Spoken = "Sonar pings at a time",
        Description = "How many things the field sonar (`sonar`) pings at a time. 1 plays one at a time, cycling " +
            "through what's nearby, nearest first; more plays the nearest few in a row.",
        Order = 90,
    };

    /// <summary>The gap between the starts of two pings in a sweep.</summary>
    public const long PingGapMilliseconds = 200;

    /// <summary>How long a ping sounds.</summary>
    public const long PingMilliseconds = 70;

    /// <summary>The most pings in one sweep, so a sweep ends well before the next.</summary>
    public const int MaxPings = 6;

    /// <summary>The volume at <see cref="Beacon.ArrivalDistance"/> or nearer, within the sound cue volume.</summary>
    public const int NearVolume = 90;

    /// <summary>The volume at the nearby list's radius.</summary>
    public const int FarVolume = 20;

    /// <summary>How long free roam must have held before pings play again, as the guidance beep (#709).</summary>
    public const long FreeRoamSettleMilliseconds = 1000;

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly ICuePlayer cues;
    private readonly Func<long> milliseconds;
    private readonly Func<bool> on;
    private readonly Action<string>? log;
    private readonly NamesTable names;
    private readonly FreeRoamGate roam;
    private readonly NavFloor? floor;
    private readonly Queue<FieldUnit> queue = new();
    private readonly HashSet<string> heard = new(StringComparer.Ordinal);
    private readonly HashSet<string> cycled = new(StringComparer.Ordinal);
    private readonly Func<int> pings;
    private int? heardMap;
    private bool swept;
    private long nextSweepAt;
    private long nextPingAt;
    private bool sounding;
    private long pingEndsAt;

    /// <summary>A sonar that reads <paramref name="field"/>, names things by <paramref name="names"/>, plays on
    /// <paramref name="cues"/>' sonar channel, keeps time by <paramref name="milliseconds"/>, plays only while
    /// <paramref name="on"/> says so, and writes each thing's first ping to <paramref name="log"/> when there is one
    /// (Debug builds). It holds its sweeps while <paramref name="floor"/> says another sound has the floor, and reads
    /// how many things a sweep pings from <paramref name="pings"/> on each sweep (<see cref="PingsPerSweep"/>'s default
    /// when not given).</summary>
    public Sonar(
        IFieldReads field, ICuePlayer cues, Func<long> milliseconds, Func<bool> on, Action<string>? log = null,
        NamesTable? names = null, NavFloor? floor = null, Func<int>? pings = null)
    {
        this.floor = floor;
        this.pings = pings ?? (() => PingsPerSweep.Default);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(on);
        this.field = field;
        this.cues = cues;
        this.milliseconds = milliseconds;
        this.on = on;
        this.log = log;
        this.names = names ?? NamesTable.Empty;
        roam = new FreeRoamGate(field, FreeRoamSettleMilliseconds, milliseconds);
    }

    /// <summary>The sonar's kind for a thing the nearby list calls <paramref name="kind"/>, or null for one it doesn't
    /// sound (enemies).</summary>
    public static SonarKind? KindOf(NearbyKind kind) => kind switch
    {
        NearbyKind.Person => SonarKind.People,
        NearbyKind.Chest or NearbyKind.Item or NearbyKind.Examine => SonarKind.Objects,
        NearbyKind.Door or NearbyKind.Exit => SonarKind.Exits,
        _ => null,
    };

    /// <summary>A kind's pitch: people E5, objects B5, exits E4, a fifth and an octave apart so they stay apart at any
    /// volume. Placeholders, the mod's own choice.</summary>
    public static double Hertz(SonarKind kind) => kind switch
    {
        SonarKind.People => 659,
        SonarKind.Objects => 988,
        _ => 330,
    };

    /// <summary>The volume for a ground distance: <see cref="NearVolume"/> at <see cref="Beacon.ArrivalDistance"/> or
    /// nearer, falling in a straight line to <see cref="FarVolume"/> at the nearby list's radius, held beyond.</summary>
    public static int Volume(double distance)
    {
        var t = Math.Clamp((distance - Beacon.ArrivalDistance) / (Nearby.Radius - Beacon.ArrivalDistance), 0, 1);
        return NearVolume - (int)Math.Round((NearVolume - FarVolume) * t);
    }

    /// <summary>Moves the sonar on to now: ends a ping that is due to end, starts a sweep or the next ping when due.
    /// A read that throws silences it, so it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it goes quiet.")]
    public void Tick()
    {
        lock (gate)
        {
            try
            {
                if (!on() || !roam.Open() || floor?.SonarMayPing() == false)
                {
                    Quiet();
                    return;
                }
                var now = milliseconds();
                if (sounding && now >= pingEndsAt)
                {
                    cues.Silence(ToneChannel.Sonar);
                    sounding = false;
                }
                if (queue.Count == 0 && (!swept || now >= nextSweepAt))
                {
                    Sweep(now);
                }
                if (queue.Count > 0 && now >= nextPingAt)
                {
                    Ping(queue.Dequeue(), now);
                }
            }
            catch (Exception)
            {
                Quiet();
            }
        }
    }

    // Reads the field and queues this sweep's things. Called under the gate.
    private void Sweep(long now)
    {
        var count = Math.Clamp(pings(), 1, MaxPings);
        swept = true;
        nextSweepAt = now + (count == 1 ? OneAtATimeMilliseconds : SweepMilliseconds);
        nextPingAt = now;
        if (field.Read() is not { } read)
        {
            return;
        }
        var view = names.Apply(read);
        if (view.MapId != heardMap)
        {
            heardMap = view.MapId;
            heard.Clear();
            cycled.Clear();
        }
        var things = Nearby.List(view)
            .Where(thing => KindOf(thing.Unit.Says) is not null)
            .Take(MaxPings)
            .Select(thing => thing.Unit)
            .ToList();
        if (count > 1)
        {
            foreach (var unit in things.Take(count))
            {
                queue.Enqueue(unit);
            }
        }
        else if (NextInCycle(things) is { } next)
        {
            queue.Enqueue(next);
        }
    }

    // The nearest thing not yet pinged in this cycle; when every thing nearby has been, a new cycle starts from the
    // nearest. Called under the gate.
    private FieldUnit? NextInCycle(List<FieldUnit> things)
    {
        if (things.Count == 0)
        {
            return null;
        }
        var next = things.FirstOrDefault(unit => !cycled.Contains(Key(unit)));
        if (next is null)
        {
            cycled.Clear();
            next = things[0];
        }
        cycled.Add(Key(next));
        return next;
    }

    // What tells one thing from another between sweeps: its id, or its kind and where it stands.
    private static string Key(FieldUnit unit) =>
        unit.Id ?? string.Create(CultureInfo.InvariantCulture, $"{unit.Kind}@{Math.Round(unit.X)},{Math.Round(unit.Z)}");

    // Plays one ping from where the player stands now. Called under the gate.
    private void Ping(FieldUnit unit, long now)
    {
        nextPingAt = now + PingGapMilliseconds;
        if (field.ReadPose() is not { } pose || KindOf(unit.Says) is not { } kind)
        {
            return;
        }
        var dx = unit.X - pose.X;
        var dz = unit.Z - pose.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        cues.Hold(ToneChannel.Sonar,
            new HeldTone(Hertz(kind), CameraFrame.Pan(CameraFrame.Bearing(pose, unit.X, unit.Z)), Volume(distance)));
        sounding = true;
        pingEndsAt = now + PingMilliseconds;
        if (log is not null && heard.Add(Key(unit)))
        {
            log(string.Create(CultureInfo.InvariantCulture,
                $"sonar first kind={kind} id={unit.Id ?? "?"} distance={distance:0.#} at=({unit.X:0.#},{unit.Y:0.#},{unit.Z:0.#})"));
        }
    }

    // Silences a ping and forgets the sweep, so the next starts afresh. Called under the gate.
    private void Quiet()
    {
        queue.Clear();
        swept = false;
        if (sounding)
        {
            sounding = false;
            cues.Silence(ToneChannel.Sonar);
        }
    }
}
