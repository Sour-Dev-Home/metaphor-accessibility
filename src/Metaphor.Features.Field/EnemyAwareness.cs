using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Input;

namespace Metaphor.Features.Field;

/// <summary>A sound and a line when a field enemy notices the player, and a softer sound when it gives up (#649, the
/// maintainer's design of 2026-11-22: "a distinct alert sound from the enemy's direction plus '&lt;enemy&gt; noticed
/// you' once; when it gives up, a softer falling sound, no speech"). It reads each nearby enemy's AI state,
/// <see cref="FieldUnit.AiState"/>, which only Debug builds read, so it is heard only in by-ear sessions until a
/// session settles the states.
/// <para>Which states mean what is [NEEDS VERIFICATION] on game build 548ddc95… (Steam build 18330018): the only lead is
/// <c>FldEnemySetAIState_Discover</c>, which requests state <see cref="NoticedState"/>
/// (docs/findings/field-enemy-awareness-static.md). So an enemy notices when its state, or the state it is asked to
/// change to, becomes <see cref="NoticedState"/>; and it gives up when it is back in the state it was in before it
/// noticed, its patrol, whatever that index is. Debug builds log each enemy's every state change, so the next session's
/// log settles both edges (docs/findings/field-enemy-strikes-and-notice-static.md's checklist).</para>
/// <para>The field's input chain calls <see cref="Keys"/> on every keyboard read; it reads the field only in free roam,
/// at most once per <see cref="CheckIntervalMilliseconds"/>. Enemies carry no id, so one is followed by where it
/// stands: the enemy nearest where a followed one was, within <see cref="SameEnemyDistance"/>, is that one, and one
/// unseen for <see cref="ForgetMilliseconds"/> is forgotten.</para></summary>
[Metaphor.Core.Settings.FeatureSetting<FieldFeature>(FieldFeature.EnemyAwareness)]
public sealed class EnemyAwareness : IInputControls
{
    /// <summary>The least time between two reads of the field: five a second.</summary>
    public const long CheckIntervalMilliseconds = 200;

    /// <summary>The state <c>FldEnemySetAIState_Discover</c> requests (<c>0x1e</c>); taken as "noticed you".
    /// [NEEDS VERIFICATION]</summary>
    public const int NoticedState = 30;

    /// <summary>How far an enemy may move between two reads and still be the one followed: a chase is quick.</summary>
    public const double SameEnemyDistance = 300;

    /// <summary>How long a followed enemy goes unseen before it is forgotten.</summary>
    public const long ForgetMilliseconds = 5000;

    /// <summary>What is said when an enemy notices the player. The mod reads no enemy names, so it says "Enemy".
    /// </summary>
    public const string NoticedText = "Enemy noticed you";

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly ICuePlayer cues;
    private readonly IAnnouncer announcer;
    private readonly Func<long> milliseconds;
    private readonly Func<bool> on;
    private readonly Action<string>? log;
    private readonly FreeRoamGate roam;
    private readonly List<Followed> followed = [];
    private long checkedAt;
    private bool checkedOnce;
    private int numbered;

    /// <summary>Awareness cues that read <paramref name="field"/>, play on <paramref name="cues"/>, speak through
    /// <paramref name="announcer"/>, keep time by <paramref name="milliseconds"/>, act only while <paramref name="on"/>
    /// says so, and write each enemy's state changes to <paramref name="log"/> when there is one (Debug builds).
    /// </summary>
    public EnemyAwareness(
        IFieldReads field, ICuePlayer cues, IAnnouncer announcer, Func<long> milliseconds, Func<bool> on,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(on);
        this.field = field;
        this.cues = cues;
        this.announcer = announcer;
        this.milliseconds = milliseconds;
        this.on = on;
        this.log = log;
        roam = new FreeRoamGate(field);
    }

    /// <summary>Hands the pad's buttons on untouched.</summary>
    public int Pad(int pad, int buttons, bool made) => buttons;

    /// <summary>Checks the enemies, then hands the keys on untouched.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Check();
        return keys;
    }

    /// <summary>Reads the field when a check is due, the feature is on and the player roams freely, and plays a cue for
    /// each enemy that noticed the player or gave up since the last read. A read that throws does nothing, so it never
    /// reaches the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it does nothing.")]
    public void Check()
    {
        List<(Cue Cue, double Pan)> heard;
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
                if (!on() || !roam.Open() || field.Read() is not { } view)
                {
                    return;
                }
                heard = Follow(view, now);
            }
            catch (Exception)
            {
                return;
            }
        }
        foreach (var (cue, pan) in heard)
        {
            cues.Play(cue, pan);
        }
        if (heard.Any(sound => sound.Cue == Cue.EnemyNoticed))
        {
            announcer.Announce(new Announcement(NoticedText, Interrupt: false));
        }
    }

    // Matches this read's enemies to the followed ones and returns the cues their changes call for. Called under the
    // gate.
    private List<(Cue Cue, double Pan)> Follow(FieldView view, long now)
    {
        var heard = new List<(Cue Cue, double Pan)>();
        var pose = view.Pose;
        var taken = new HashSet<Followed>();
        foreach (var unit in view.Units)
        {
            if (unit.Kind != FieldKind.Enemy || unit.AiState is null)
            {
                continue;
            }
            var one = followed
                .Where(f => !taken.Contains(f) && f.Distance(unit) < SameEnemyDistance)
                .MinBy(f => f.Distance(unit));
            if (one is null)
            {
                one = new Followed(++numbered);
                followed.Add(one);
            }
            taken.Add(one);
            var dx = unit.X - pose.X;
            var dz = unit.Z - pose.Z;
            var pan = CameraFrame.Pan(CameraFrame.Bearing(pose, unit.X, unit.Z));
            if (one.Changed(unit))
            {
                log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                    $"enemy ai n={one.Number} state={Text(one.State)}->{Text(unit.AiState)} ") +
                    string.Create(CultureInfo.InvariantCulture,
                        $"requested={Text(unit.AiRequested)} calm={Text(one.Calm)} noticed={(one.Noticed ? 1 : 0)} ") +
                    string.Create(CultureInfo.InvariantCulture,
                        $"at=({unit.X:0.#},{unit.Z:0.#}) distance={Math.Sqrt((dx * dx) + (dz * dz)):0.#}"));
            }
            if (one.See(unit, now) is { } cue)
            {
                heard.Add((cue, pan));
            }
        }
        followed.RemoveAll(f => now - f.SeenAt >= ForgetMilliseconds);
        return heard;
    }

    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    // One enemy followed from read to read: where it was, its last states, the state it was in before it noticed (its
    // calm one) and whether it has noticed the player.
    private sealed class Followed(int number)
    {
        private float x;
        private float z;
        private bool seen;

        public int Number => number;

        public long SeenAt { get; private set; }

        public int? State { get; private set; }

        public int? Requested { get; private set; }

        public int? Calm { get; private set; }

        public bool Noticed { get; private set; }

        public double Distance(FieldUnit unit)
        {
            var dx = unit.X - x;
            var dz = unit.Z - z;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }

        public bool Changed(FieldUnit unit) => !seen || unit.AiState != State || unit.AiRequested != Requested;

        // Takes this read of it and returns the cue its change calls for, if any.
        public Cue? See(FieldUnit unit, long now)
        {
            (x, z, SeenAt, State, Requested, seen) = (unit.X, unit.Z, now, unit.AiState, unit.AiRequested, true);
            var alert = State == NoticedState || Requested == NoticedState;
            if (!Noticed && alert)
            {
                Noticed = true;
                return Cue.EnemyNoticed;
            }
            if (Noticed && !alert && Calm is { } calm && State == calm && (Requested is null || Requested == calm))
            {
                Noticed = false;
                return Cue.EnemyGaveUp;
            }
            if (!Noticed && !alert)
            {
                Calm = State;
            }
            return null;
        }
    }
}
