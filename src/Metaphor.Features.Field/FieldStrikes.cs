using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Says a field strike's result (#696, the maintainer's Q32 of 2026-11-22): "Hit" when a field enemy's HP drops,
/// "Defeated" when it reaches 0. The HP is <see cref="FieldUnit.Hp"/>, which the game's field-damage handler
/// (<c>fldEnemy::vf58</c>) lowers and <c>FldEnemyIsDead</c> tests against 0 on game build 548ddc95… (Steam build
/// 18330018); high confidence in code, but that a strike of the player's is what lowers it, and that a defeated enemy
/// is still read at 0, are [NEEDS VERIFICATION] (docs/findings/field-enemy-strikes-and-notice-static.md). Only the
/// enemies the nearby list reads, within its radius, are seen. "Stunned" is <see cref="StunCallouts"/>'s; "Battle!",
/// the strike's reach and the Result box aren't found yet.
/// <para>The field controls call <see cref="Check"/> on every keyboard read. It reads the field only in free roam and at
/// most once per <see cref="CheckIntervalMilliseconds"/>. An enemy is followed from read to read as the nearest one
/// within <see cref="SameEnemyDistance"/> of where it was, and let go once unseen for <see cref="LeaveMilliseconds"/>.
/// In a Debug build each drop is logged with the player's distance and bearing to the enemy, the strike's reach for
/// #186's tick, and an enemy let go soon after a hit is logged too: a defeat the HP didn't show, or a command battle.
/// </para></summary>
[FeatureSetting<FieldFeature>(FieldFeature.FieldStrikes)]
public sealed class FieldStrikes
{
    /// <summary>What a drop that leaves HP is said as.</summary>
    public const string HitText = "Hit";

    /// <summary>What a drop to 0 is said as.</summary>
    public const string DefeatedText = "Defeated";

    /// <summary>The least time between two reads of the field: ten a second, so a defeated enemy is read before it
    /// goes.</summary>
    public const long CheckIntervalMilliseconds = 100;

    /// <summary>How long a followed enemy goes unseen before it is let go.</summary>
    public const long LeaveMilliseconds = 1000;

    /// <summary>Ground distance within which an enemy is the one already followed: more than a chasing enemy covers
    /// between two reads at running speed (about 700 units a second, <see cref="Beacon.ArrivalDistance"/>).</summary>
    public const double SameEnemyDistance = 200;

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly FreeRoamGate roam;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Action<string>? log;
    private readonly List<Followed> followed = [];
    private long checkedAt;
    private bool checkedOnce;

    /// <summary>Callouts that read <paramref name="field"/>, speak through <paramref name="announcer"/>, keep time by
    /// <paramref name="milliseconds"/> and write each drop, as numbers, to <paramref name="log"/> when there is one
    /// (Debug builds).</summary>
    public FieldStrikes(IFieldReads field, IAnnouncer announcer, Func<long> milliseconds, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.field = field;
        roam = new FreeRoamGate(field);
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.log = log;
    }

    /// <summary>Reads the field when a check is due and the player roams freely, and says the strongest result since the
    /// last read: "Defeated" over "Hit". A read that throws says nothing, so it never reaches the game.</summary>
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
        var hit = false;
        var defeated = false;
        var claimed = new HashSet<Followed>();
        foreach (var unit in view.Units)
        {
            if (unit.Kind != FieldKind.Enemy || unit.Hp is not { } hp)
            {
                continue;
            }
            var match = followed.Where(enemy => !claimed.Contains(enemy) && enemy.Within(unit))
                .MinBy(enemy => enemy.Distance(unit));
            if (match is null)
            {
                match = new Followed();
                followed.Add(match);
            }
            else if (hp < match.Hp)
            {
                defeated |= hp <= 0;
                hit |= hp > 0;
                match.HitAt = now;
                Log(view, unit, match.Hp, hp);
            }
            claimed.Add(match);
            match.At(unit, hp, now);
        }
        foreach (var gone in followed.Where(enemy => now - enemy.SeenAt >= LeaveMilliseconds))
        {
            if (gone.HitAt is { } at && gone.SeenAt - at < LeaveMilliseconds)
            {
                log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                    $"strike lost lastHp={gone.Hp} hitBeforeGone={now - at}ms"));
            }
        }
        followed.RemoveAll(enemy => now - enemy.SeenAt >= LeaveMilliseconds);
        return defeated ? DefeatedText : hit ? HitText : null;
    }

    private void Log(FieldView view, FieldUnit unit, int before, int after)
    {
        if (log is null)
        {
            return;
        }
        var (distance, bearing, _) = Nearby.Measure(view.Pose, unit, view.DoorReachValue);
        log(string.Create(CultureInfo.InvariantCulture,
            $"strike hp={before}->{after} distance={distance:0.#} bearing={bearing:0.###} heightDiff={unit.Y - view.Y:0.#} stunned={unit.Stunned} at=({unit.X:0.#},{unit.Z:0.#}) player=({view.X:0.#},{view.Z:0.#})"));
    }

    private sealed class Followed
    {
        private float x;
        private float z;

        public int Hp { get; private set; }

        public long SeenAt { get; private set; }

        public long? HitAt { get; set; }

        public double Distance(FieldUnit unit)
        {
            var dx = unit.X - x;
            var dz = unit.Z - z;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }

        public bool Within(FieldUnit unit) => Distance(unit) < SameEnemyDistance;

        public void At(FieldUnit unit, int hp, long now) => (x, z, Hp, SeenAt) = (unit.X, unit.Z, hp, now);
    }
}
