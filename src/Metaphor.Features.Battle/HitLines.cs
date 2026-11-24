using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>A sound played before a hit's number, by the target's side (#631: the maintainer's sound cues, built as
/// their own feature). This feature only calls it.</summary>
public interface IHitCue
{
    void Before(BattleSide side);
}

/// <summary>An enemy this battle has hit, as <see cref="HitLines"/> labels it, with the HP it had left and its max HP
/// (null when unread) at its last hit, and its record address, an identity only.</summary>
public sealed record KnownEnemy(string Label, int Hp, int? HpMax, long Target);

/// <summary>An enemy this battle knows (#750), hit or not: its label, record address (an identity only), and the HP its
/// last hit left and its max HP, each null when no hit has shown it.</summary>
public sealed record ListedEnemy(string Label, long Target, int? Hp, int? HpMax);

/// <summary>Says each target a battle action hits as the hit lands (#247, #738; the maintainer's call of 2026-11-22): a
/// party member as "name damage" ("Strohl 30", "Hulkenberg 12 healed"), an enemy as "Enemy, 52", given an ordinal by
/// the order the battle first hit them when it has hit more than one ("Second enemy, 52"; a label never ends in a digit
/// before the number, since "Ally 1 141" sounds like 1141). Never the HP left (the details key says it), no summary
/// afterwards, and no line for a hit with no HP change. Only hits whose popup carries a number are said. Safe to call
/// from several threads.</summary>
/// <remarks>
/// <para>No safe read of a unit's name is known yet: a party member is named by <c>memberName</c> when it knows its unit
/// number, else "First ally" and so on by slot; an enemy is "Enemy". An enemy's ordinal is the order it was first hit,
/// not its place on screen, which isn't read, and is the one it has when the hit lands.</para>
/// <para>The game gives no "battle over" signal the mod reads, so a battle ends at a long quiet gap
/// (<see cref="BattleGapMilliseconds"/>), which forgets its enemies; the length is a guess to check by ear. Leaving the battle scene ends one sooner
/// (<see cref="BattleEnds"/>, #750).</para>
/// <para>With <see cref="ListOn"/> (#750, <see cref="BattleFeature.BattleEnemyList"/>) the battle package's enemy list
/// (<see cref="Enemies"/>) sets the ordinals from the battle's start, in list order, which is not yet known to be the
/// order on screen (<c>[NEEDS VERIFICATION]</c>, docs/findings/battle-enemy-list-order-and-update-hook-static.md). With
/// <see cref="NumbersOnly"/> (#855, <see cref="BattleFeature.BattleHitNumbers"/>) a hit says its number alone and
/// <see cref="SlotCue"/> marks whose it was by side and place.</para>
/// </remarks>
[FeatureSetting<BattleFeature>(
    BattleFeature.BattleHits, BattleFeature.BattleOutcomes, BattleFeature.BattleEnemyList,
    BattleFeature.BattleHitNumbers)]
public sealed class HitLines : IBattleHitObserver, IBattleEnemyObserver
{
    /// <summary>The party's slots, the count a party member's <see cref="SlotCue"/> is placed among.</summary>
    public const int PartySlots = 4;

    /// <summary>How far left or right the first and last place sound: not fully, so one ear never hears it alone.
    /// </summary>
    public const double WidestPan = 0.8;

    /// <summary>Where place <paramref name="index"/> of <paramref name="count"/> sounds, from -<see cref="WidestPan"/>
    /// (the first) to +<see cref="WidestPan"/> (the last) in even steps; the centre when there is one place.</summary>
    public static double Pan(int index, int count) => count <= 1
        ? 0
        : -WidestPan + 2 * WidestPan * Math.Clamp(index, 0, count - 1) / (count - 1);

    /// <summary>How long the same hit told again is taken as the same one.</summary>
    public const long RepeatMilliseconds = 500;

    /// <summary>How long without a hit before the next hit starts a new battle.</summary>
    public const long BattleGapMilliseconds = 180_000;

    private readonly IAnnouncer announcer;
    private readonly TimeProvider time;
    private readonly Func<int, string?> memberName;
    private readonly IHitCue? cue;
    private readonly Func<bool> outcomesOn;
    private readonly RepeatGate repeats;
    private readonly Lock gate = new();
    private readonly List<long> enemies = [];
    private readonly Dictionary<long, (int Hp, int? HpMax)> enemyHp = [];
    private long lastHit = long.MinValue / 2;
    private long battle;
    // The package the enemy list came from, 0 for none this battle.
    private long listPackage;

    /// <summary>Whether the enemy list numbers the enemies (<see cref="BattleFeature.BattleEnemyList"/>); off when
    /// unset.</summary>
    public Func<bool> ListOn { get; init; } = () => false;

    /// <summary>Whether a hit says its number alone (<see cref="BattleFeature.BattleHitNumbers"/>); off when unset.
    /// </summary>
    public Func<bool> NumbersOnly { get; init; } = () => false;

    /// <summary>Played before a number-only hit's line: the target's side, its place from 0 (an enemy's in the list,
    /// a party member's slot) and how many places there are. Null plays nothing.</summary>
    public Action<BattleSide, int, int>? SlotCue { get; init; }

    /// <summary>The battle package's enemy list (#750): a new package starts a new battle with these enemies in list
    /// order; the same one adds any enemy not yet known at the end, so a defeat, which shrinks the list, keeps every
    /// ordinal. Ignored unless <see cref="ListOn"/>.</summary>
    public void Enemies(long package, IReadOnlyList<long> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (!ListOn())
        {
            return;
        }
        lock (gate)
        {
            if (package != listPackage)
            {
                enemies.Clear();
                enemyHp.Clear();
                battle++;
                listPackage = package;
            }
            foreach (var record in records.Where(record => !enemies.Contains(record)))
            {
                enemies.Add(record);
            }
            // The list belongs to this battle, so the quiet gap counts from it too.
            lastHit = Now();
        }
    }

    /// <summary>Hits said through <paramref name="announcer"/>, timed by <paramref name="time"/>; party members named
    /// by <paramref name="memberName"/> from their unit number, or null when unknown; <paramref name="cue"/> is played
    /// before each number, when there is one; <paramref name="outcomesOn"/> says whether to add ", knocked out",
    /// ", defeated" and ", revived" (<see cref="BattleFeature.BattleOutcomes"/>), on when null.</summary>
    public HitLines(
        IAnnouncer announcer,
        TimeProvider time,
        Func<int, string?> memberName,
        IHitCue? cue = null,
        Func<bool>? outcomesOn = null)
    {
        this.outcomesOn = outcomesOn ?? (() => true);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(memberName);
        this.announcer = announcer;
        this.time = time;
        this.memberName = memberName;
        this.cue = cue;
        repeats = new RepeatGate(RepeatMilliseconds, Now);
    }

    public void Observe(BattleHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (!hit.HasNumber || !repeats.LetThrough(hit.Target, ((long)hit.HpChange << 32) | (uint)hit.HpLeft))
        {
            return;
        }
        string line;
        (int Index, int Count)? place;
        lock (gate)
        {
            var now = Now();
            if (now - lastHit > BattleGapMilliseconds)
            {
                enemies.Clear();
                enemyHp.Clear();
                battle++;
            }
            lastHit = now;
            if (hit.Side == BattleSide.Enemy)
            {
                if (!enemies.Contains(hit.Target))
                {
                    enemies.Add(hit.Target);
                }
                enemyHp[hit.Target] = (hit.HpLeft, hit.HpMax);
            }
            if (hit.HpChange == 0)
            {
                return;
            }
            // The outcome the HP left shows (#254): 0 after damage is a knock-out or a defeat, a gain equal to the HP
            // left was a gain from 0, a revive. Read from the hit's own numbers; no banner is used.
            // It is its own setting while it is tested, so a normal install keeps the plain number.
            var outcome = (hit.HpLeft, hit.HpChange, hit.Side) switch
            {
                (0, < 0, BattleSide.Ally) => ", knocked out",
                (0, < 0, BattleSide.Enemy) => ", defeated",
                (_, > 0, BattleSide.Ally) when hit.HpLeft == hit.HpChange => ", revived",
                _ => "",
            };
            var change = Change(hit.HpChange) + (outcomesOn() ? outcome : "");
            // Numbers only (#855): the place goes to a sound, so an area attack's lines stay short.
            place = NumbersOnly() ? hit.Side switch
            {
                BattleSide.Enemy => (enemies.IndexOf(hit.Target), enemies.Count),
                BattleSide.Ally when hit.Slot is { } at => (at, PartySlots),
                _ => null,
            } : null;
            line = NumbersOnly() ? change : hit.Side switch
            {
                BattleSide.Ally => hit.Slot is { } slot ? MemberSays(slot, hit.Unit, change) : $"Ally, {change}",
                BattleSide.Enemy => $"{EnemyLabel(hit.Target)}, {change}",
                _ => $"Target, {change}",
            };
        }
        if (place is var (index, count))
        {
            SlotCue?.Invoke(hit.Side, index, count);
        }
        cue?.Before(hit.Side);
        announcer.Announce(new Announcement(line, Interrupt: false));
    }

    /// <summary>The enemies this battle has hit, in the order first hit: for the status and details keys.</summary>
    public IReadOnlyList<KnownEnemy> KnownEnemies()
    {
        lock (gate)
        {
            return [.. enemies.Where(enemyHp.ContainsKey)
                .Select(enemy => new KnownEnemy(EnemyLabel(enemy), enemyHp[enemy].Hp, enemyHp[enemy].HpMax, enemy))];
        }
    }

    /// <summary>Every enemy this battle knows, hit or not, in its order (the list's with <see cref="ListOn"/>), each
    /// with the HP its last hit left, null before one (#750).</summary>
    public IReadOnlyList<ListedEnemy> AllEnemies()
    {
        lock (gate)
        {
            return [.. enemies.Select(enemy => enemyHp.TryGetValue(enemy, out var hp)
                ? new ListedEnemy(EnemyLabel(enemy), enemy, hp.Hp, hp.HpMax)
                : new ListedEnemy(EnemyLabel(enemy), enemy, null, null))];
        }
    }

    /// <summary>Whether this battle has hit an enemy it still remembers.</summary>
    public bool RemembersEnemies
    {
        get
        {
            lock (gate)
            {
                return enemies.Count > 0;
            }
        }
    }

    /// <summary>The battle is over (#750): its enemies are forgotten, so the next battle numbers its own from the
    /// first.</summary>
    public void BattleOver()
    {
        lock (gate)
        {
            enemies.Clear();
            enemyHp.Clear();
            battle++;
        }
    }

    /// <summary>Which battle the known enemies belong to: a number that changes each time a battle's enemies are
    /// forgotten, so a key that remembers an enemy (#767) can tell a record address reused by the next battle.
    /// </summary>
    public long Battle
    {
        get
        {
            lock (gate)
            {
                return battle;
            }
        }
    }

    /// <summary>An HP change as said: damage as its number ("34"), a gain as "34 healed".</summary>
    private static string Change(long change) => change < 0
        ? string.Create(CultureInfo.InvariantCulture, $"{-change}")
        : string.Create(CultureInfo.InvariantCulture, $"{change} healed");

    /// <summary>A party member's name followed by <paramref name="rest"/> (a number): <c>memberName</c>'s, or "First
    /// ally," and so on by slot. The comma and the ordinal word keep the reader from running the slot into the number
    /// ("Ally 1 141" sounds like 1141).</summary>
    public string MemberSays(int slot, int? unit, string rest) =>
        unit is { } known && memberName(known) is { Length: > 0 } name
            ? $"{name} {rest}"
            : $"{Ordinal(slot + 1, "ally")}, {rest}";

    /// <summary>"First ally", "Second enemy", ... ; past the tenth, "11th ally".</summary>
    private static string Ordinal(int n, string noun) => n >= 1 && n <= Ordinals.Length
        ? $"{Ordinals[n - 1]} {noun}"
        : string.Create(CultureInfo.InvariantCulture, $"{n}th {noun}");

    private static readonly string[] Ordinals =
        ["First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth"];

    // Called under the gate.
    private string EnemyLabel(long enemy) =>
        enemies.Count > 1
            ? Ordinal(enemies.IndexOf(enemy) + 1, "enemy")
            : "Enemy";

    private long Now() => time.GetUtcNow().ToUnixTimeMilliseconds();
}
