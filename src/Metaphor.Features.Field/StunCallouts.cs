using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Announcements;

namespace Metaphor.Features.Field;

/// <summary>Says a field enemy as it becomes stunned (#772), so the player knows it can be struck into a battle with the
/// advantage: "Enemy stunned". Stunned is the enemy's break byte, <see cref="FieldUnit.Stunned"/>, which the game sets
/// when a strike empties its stun gauge and clears when the stun times out or the enemy is hit again; high confidence in
/// code, but that it is the stun the player sees is [NEEDS VERIFICATION]
/// (docs/findings/enemy-stun-break-and-battle-condition-static.md, game build 548ddc95, Steam build 18330018). Only the
/// enemies the nearby list reads, within its radius, are seen.
/// <para>The field controls call <see cref="Check"/> on every keyboard read, as for <see cref="ReachCallouts"/>. It reads
/// the field only in free roam and at most once per <see cref="CheckIntervalMilliseconds"/>. A stunned enemy is held by
/// where it stands, which it doesn't leave while stunned, and let go once no stunned enemy has been seen there for
/// <see cref="LeaveMilliseconds"/>; so one stun is said once, and the next one after it ends is said again. The callout
/// queues behind what is being said.</para></summary>
[Metaphor.Core.Settings.FeatureSetting<FieldFeature>(FieldFeature.StunCallouts)]
public sealed class StunCallouts
{
    /// <summary>The least time between two reads of the field: five a second, well within a stun's few seconds.</summary>
    public const long CheckIntervalMilliseconds = 200;

    /// <summary>How long a held stun goes unseen before it counts as over: one second, so one missed read isn't a new
    /// stun.</summary>
    public const long LeaveMilliseconds = 1000;

    /// <summary>Ground distance within which a stunned enemy is the one already held.</summary>
    public const double SameEnemyDistance = 100;

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
    public StunCallouts(IFieldReads field, IAnnouncer announcer, Func<long> milliseconds)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.field = field;
        roam = new FreeRoamGate(field);
        this.announcer = announcer;
        this.milliseconds = milliseconds;
    }

    /// <summary>Reads the field when a check is due and the player roams freely, and says each enemy newly stunned since
    /// the last read. A read that throws says nothing, so it never reaches the game.</summary>
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
                text = roam.Open() && field.Read() is { } view ? Landed(view, now) : null;
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

    private string? Landed(FieldView view, long now)
    {
        List<string>? landed = null;
        foreach (var unit in view.Units)
        {
            if (unit.Kind != FieldKind.Enemy || !unit.Stunned)
            {
                continue;
            }
            if (held.Find(stun => stun.Matches(unit)) is { } stun)
            {
                stun.At(unit, now);
                continue;
            }
            var kept = new Held();
            kept.At(unit, now);
            held.Add(kept);
            (landed ??= []).Add($"{unit.Name ?? Nearby.KindWord(unit.Kind)} stunned");
        }
        held.RemoveAll(stun => now - stun.SeenAt >= LeaveMilliseconds);
        return landed is null ? null : string.Join(". ", landed);
    }

    private sealed class Held
    {
        private float x;
        private float z;

        public long SeenAt { get; private set; }

        public bool Matches(FieldUnit unit)
        {
            var dx = unit.X - x;
            var dz = unit.Z - z;
            return dx * dx + dz * dz < SameEnemyDistance * SameEnemyDistance;
        }

        public void At(FieldUnit unit, long now) => (x, z, SeenAt) = (unit.X, unit.Z, now);
    }
}
