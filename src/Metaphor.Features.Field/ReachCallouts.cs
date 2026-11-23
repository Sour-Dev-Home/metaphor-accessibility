using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Announcements;

namespace Metaphor.Features.Field;

/// <summary>Says a door or chest as it comes into reach (#644), the moment the game's action prompt would show: "Door, in
/// reach". It's said once, and again only after the thing has been out of reach for <see cref="LeaveMilliseconds"/>
/// and come back; a slightly wider reach (<see cref="Nearby.NearReach"/>) holds it meanwhile, so standing at the edge
/// doesn't flicker. Reach is the game's own test with its distance 20% longer (<see cref="Nearby.CalloutReach"/>), so people stay out until theirs is found. The game's prompt itself, and the target it
/// picks, sit behind <c>.text1</c> and aren't read (docs/findings/field-interaction-prompt-static.md); its words from
/// <c>AccessPointName.msg</c> are <c>[NEEDS VERIFICATION]</c>, so the kind word stands in.
/// <para>The field controls call <see cref="Check"/> on every keyboard read, which the game makes once a frame by its
/// poll sites (that they run every frame is [NEEDS VERIFICATION]: docs/raw-sources/input-route-2026-11-16.md). It reads the field only in free
/// roam and at most once per <see cref="CheckIntervalMilliseconds"/>; the calls between allocate nothing. The callout
/// queues behind what is being said rather than cutting it off: it's news the player didn't ask for, and the end of a
/// line just spoken may still be playing as free roam comes back.</para></summary>
[Metaphor.Core.Settings.FeatureSetting(Metaphor.Core.Settings.Feature.Reach)]
public sealed class ReachCallouts
{
    /// <summary>The least time between two reads of the field: ten a second, quick enough for a walk.</summary>
    public const long CheckIntervalMilliseconds = 100;

    /// <summary>How long a thing must stay out of near reach (<see cref="Nearby.NearReach"/>) before it counts as gone
    /// and may be said again: one second, so a read that misses it, or the player's idle sway at the edge, isn't a new
    /// arrival (#644). Time outside free roam counts: what stays in reach through a dialogue is still held after it.</summary>
    public const long LeaveMilliseconds = 1000;

    /// <summary>Ground distance within which a thing of the same kind is the one already held, a step.</summary>
    public const double SameThingDistance = 100;

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly FreeRoamGate roam;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly List<Held> held = [];
    private long checkedAt;
    private bool checkedOnce;

    /// <summary>Callouts that read <paramref name="field"/>, speak through <paramref name="announcer"/> and keep time by
    /// <paramref name="milliseconds"/>.</summary>
    public ReachCallouts(IFieldReads field, IAnnouncer announcer, Func<long> milliseconds)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.field = field;
        roam = new FreeRoamGate(field);
        this.announcer = announcer;
        this.milliseconds = milliseconds;
    }

    /// <summary>Reads the field when a check is due and the player roams freely, and says what has come into reach since
    /// the last read. What stays in reach through a menu or a dialogue isn't said again after it; an unreadable field
    /// changes nothing. A read that throws says nothing, so it never reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    public void Check()
    {
        string? text;
        lock (gate)
        {
            var now = milliseconds();
            if (checkedOnce && now - checkedAt < CheckIntervalMilliseconds)
            {
                return;
            }
            checkedOnce = true;
            checkedAt = now;
            try
            {
                text = roam.Open() && field.Read() is { } view ? Entered(view, now) : null;
            }
            catch (Exception)
            {
                return;
            }
        }
        if (text is not null)
        {
            announcer.Announce(new Announcement(text, Interrupt: false));
        }
    }

    /// <summary>Holds on to what is still near reach, lets go of what has been away for <see cref="LeaveMilliseconds"/>,
    /// and returns what newly came into reach, or null for nothing.</summary>
    private string? Entered(FieldView view, long now)
    {
        foreach (var unit in view.Units)
        {
            if (Nearby.NearReach(view, unit) && Holding(unit) is { } thing)
            {
                thing.At(unit, now);
            }
        }
        held.RemoveAll(thing => now - thing.HeldAt >= LeaveMilliseconds);
        List<string>? entered = null;
        foreach (var thing in Nearby.CalloutReach(view))
        {
            var unit = thing.Unit;
            if (Holding(unit) is null)
            {
                var kept = new Held(unit.Kind);
                kept.At(unit, now);
                held.Add(kept);
                (entered ??= []).Add($"{unit.Name ?? Nearby.KindWord(unit.Kind)}, in reach");
            }
        }
        return entered is null ? null : string.Join(". ", entered);
    }

    private Held? Holding(FieldUnit unit) => held.Find(thing => thing.Matches(unit));

    /// <summary>A thing in reach, told apart from the others by its kind and where it was last seen: within
    /// <see cref="SameThingDistance"/> is the same thing, so a door that shifts a little as it opens stays one door.</summary>
    private sealed class Held(FieldKind kind)
    {
        private float x;
        private float z;

        public long HeldAt { get; private set; }

        public bool Matches(FieldUnit unit)
        {
            var dx = unit.X - x;
            var dz = unit.Z - z;
            return unit.Kind == kind && dx * dx + dz * dz < SameThingDistance * SameThingDistance;
        }

        public void At(FieldUnit unit, long now)
        {
            (x, z, HeldAt) = (unit.X, unit.Z, now);
        }
    }
}
