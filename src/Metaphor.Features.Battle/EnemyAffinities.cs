using System.Globalization;
using Metaphor.Core.Battle;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Each battle enemy's affinities and table rewards from the enemy tables (#164, #255, #166): which enemy id a
/// unit is (its record's table id, #921, unless a hit's max HP contradicts it; else <see cref="EnemyTables.Identify"/>:
/// its max HP, narrowed by the enemy names this battle's banners said), and
/// the affinities the player has found, said after an enemy's HP line ("weak to Fire, resists Ice"). With
/// <see cref="FoundOnly"/> off, every affinity in the table is said. Safe to call from several threads.</summary>
/// <remarks>
/// <para>Where the game keeps the affinities a player has found isn't located
/// (docs/findings/battle-affinity-discovery-from-move-banner.md), so the mod tracks them itself, for this game session
/// only: a move banner (kind 0) names the skill by its <c>Skill.TBL</c> id, whose element byte gives the element; a hit
/// on an enemy after it, within <see cref="MoveMilliseconds"/> and before any hit on the party, marks that element found
/// for that enemy id. A hit with a number marks it whatever the affinity; one without a number only when the table says
/// null or repel, since a miss shows none either. That this matches what the game reveals is <c>[NEEDS VERIFICATION]</c>
/// by ear; a basic attack shows no skill, so it finds nothing.</para>
/// <para>The decision to say only what was found is the maintainer's, 2026-11-24, on #164.</para>
/// </remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleAffinities, BattleFeature.BattleAffinitiesFoundOnly)]
public sealed class EnemyAffinities : IBattleHitObserver
{
    /// <summary>How long after a move banner its hits find its element: a guess to check by ear.</summary>
    public const long MoveMilliseconds = 15_000;

    private readonly Func<EnemyTables?> tables;
    private readonly Func<long> battle;
    private readonly Func<long> clock;
    private readonly Func<int, string?> enemyLabel;
    private readonly Lock gate = new();
    private readonly Dictionary<int, int> found = [];
    private readonly HashSet<string> namesSeen = [];
    private readonly Dictionary<long, int> maxHp = [];
    private readonly Dictionary<long, EnemyIdentity> identified = [];
    private long forBattle = long.MinValue;
    private AffinityElement? move;
    private long moveAt;

    /// <summary>Reads <paramref name="tables"/> once loaded; <paramref name="battle"/> is the hit lines' battle number,
    /// so a new battle forgets its units; <paramref name="enemyLabel"/> gives an enemy name serial's label, or null for
    /// a serial that isn't one.</summary>
    public EnemyAffinities(
        Func<EnemyTables?> tables, Func<long> battle, Func<long> clock, Func<int, string?> enemyLabel)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(enemyLabel);
        this.tables = tables;
        this.battle = battle;
        this.clock = clock;
        this.enemyLabel = enemyLabel;
    }

    /// <summary>Whether affinities are said (<see cref="BattleFeature.BattleAffinities"/>); off when unset.</summary>
    public Func<bool> On { get; init; } = () => false;

    /// <summary>Whether only found affinities are said (<see cref="BattleFeature.BattleAffinitiesFoundOnly"/>); on
    /// when unset.</summary>
    public Func<bool> FoundOnly { get; init; } = () => true;

    /// <summary>Reads an enemy's HP before a hit has shown its maximum; none when null.</summary>
    public IBattleUnits? Units { get; init; }

    /// <summary>Hears each unit told apart and each element found (Debug builds); none when null.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>A banner named <paramref name="serial"/> as its subject (#739): an enemy name narrows the ids.</summary>
    public void Named(int serial)
    {
        if (enemyLabel(serial) is not { } label)
        {
            return;
        }
        lock (gate)
        {
            Current();
            namesSeen.Add(MessageLibrary.LabelHash(label));
        }
    }

    /// <summary>A move banner showed skill <paramref name="skill"/> (0 for a basic attack): its hits find its element.
    /// </summary>
    public void Move(int skill)
    {
        var element = tables()?.ElementOf(skill);
        lock (gate)
        {
            move = element;
            moveAt = clock();
        }
    }

    public void Observe(BattleHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (hit.Side == BattleSide.Ally)
        {
            lock (gate)
            {
                // The party was hit: an enemy acted, so the move is no longer the party's.
                move = null;
            }
            return;
        }
        if (hit.Side != BattleSide.Enemy || tables() is not { } read)
        {
            return;
        }
        string? said = null;
        lock (gate)
        {
            Current();
            if (hit.HpMax is { } max)
            {
                maxHp[hit.Target] = max;
            }
            if (move is not { } element || clock() - moveAt > MoveMilliseconds ||
                IdOf(read, hit.Target, hit.HpMax) is not { } identity || read.Row(identity.Id) is not { } row)
            {
                return;
            }
            var flags = row.Affinity(element).Flags;
            if (hit.HasNumber || (flags & (AffinityFlags.Null | AffinityFlags.Repel)) != 0)
            {
                found[identity.Id] = Found(identity.Id) | (1 << (int)element);
                said = string.Create(CultureInfo.InvariantCulture,
                    $"found id={identity.Id} element={element} flags={flags} kind=0x{hit.Kind:x}");
            }
        }
        if (said is not null)
        {
            Log?.Invoke(said);
        }
    }

    /// <summary>What <paramref name="enemy"/>'s affinities add to its line ("weak to Fire, resists Ice"), or null when
    /// off, unknown, or none to say.</summary>
    public string? Says(ListedEnemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        if (!On() || tables() is not { } read)
        {
            return null;
        }
        int mask;
        EnemyRow row;
        lock (gate)
        {
            Current();
            var max = enemy.HpMax ?? (enemy.Hp is null ? Units?.Hp(enemy.Target) : null);
            if (IdOf(read, enemy.Target, max) is not { } identity || read.Row(identity.Id) is not { } known)
            {
                return null;
            }
            row = known;
            mask = FoundOnly() ? Found(identity.Id) : -1;
        }
        return Describe(row, mask);
    }

    /// <summary>Which enemy table row <paramref name="enemy"/> is, told apart as for its affinities, or null when the
    /// tables aren't loaded or it isn't told apart (#699, its name).</summary>
    public int? TableIdOf(ListedEnemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        if (tables() is not { } read)
        {
            return null;
        }
        lock (gate)
        {
            Current();
            var max = enemy.HpMax ?? (enemy.Hp is null ? Units?.Hp(enemy.Target) : null);
            return IdOf(read, enemy.Target, max)?.Id;
        }
    }

    /// <summary>The table's EXP and archetype EXP over <paramref name="enemies"/>, and its money and magla, or null
    /// when the tables aren't loaded or any enemy isn't told apart.</summary>
    public (int Exp, int ArchetypeExp, int Money, int Magla)? Rewards(IReadOnlyList<ListedEnemy> enemies)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        if (tables() is not { } read || enemies.Count == 0)
        {
            return null;
        }
        (int Exp, int ArchetypeExp, int Money, int Magla) sum = default;
        lock (gate)
        {
            Current();
            foreach (var enemy in enemies)
            {
                if (IdOf(read, enemy.Target, enemy.HpMax) is not { } identity || read.Row(identity.Id) is not { } row)
                {
                    return null;
                }
                sum = (sum.Exp + row.Exp, sum.ArchetypeExp + row.ArchetypeExp, sum.Money + row.Money,
                    sum.Magla + row.Magla);
            }
        }
        return sum;
    }

    /// <summary>The affinities of <paramref name="row"/> among the elements set in <paramref name="mask"/> (bit n for
    /// element n), grouped: weak, resists, nulls, repels, drains; null when none.</summary>
    public static string? Describe(EnemyRow row, int mask)
    {
        ArgumentNullException.ThrowIfNull(row);
        var groups = new List<(string Verb, List<string> Elements)>
        {
            ("weak to", []), ("resists", []), ("nulls", []), ("repels", []), ("drains", []),
        };
        foreach (var element in Enum.GetValues<AffinityElement>())
        {
            if ((mask & (1 << (int)element)) == 0)
            {
                continue;
            }
            var flags = row.Affinity(element).Flags;
            // One verb per element, the strongest first.
            int? group = (flags & AffinityFlags.Drain) != 0 ? 4
                : (flags & AffinityFlags.Repel) != 0 ? 3
                : (flags & AffinityFlags.Null) != 0 ? 2
                : (flags & AffinityFlags.Weak) != 0 ? 0
                : (flags & AffinityFlags.Resist) != 0 ? 1
                : null;
            if (group is { } at)
            {
                groups[at].Elements.Add(element.ToString());
            }
        }
        var parts = groups.Where(g => g.Elements.Count > 0).Select(g => $"{g.Verb} {Join(g.Elements)}").ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string Join(List<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

    private int Found(int id) => found.TryGetValue(id, out var mask) ? mask : 0;

    // Called under the gate: a new battle forgets the units and names, but never what was found.
    private void Current()
    {
        var now = battle();
        if (now == forBattle)
        {
            return;
        }
        forBattle = now;
        namesSeen.Clear();
        maxHp.Clear();
        identified.Clear();
    }

    // Called under the gate.
    private EnemyIdentity? IdOf(EnemyTables read, long record, int? max)
    {
        if (identified.TryGetValue(record, out var known))
        {
            return known;
        }
        var hpKnown = max ?? (maxHp.TryGetValue(record, out var seen) ? seen : null);
        EnemyIdentity? identity;
        // The record's own table id (#921), unless a max HP a hit showed says it is another row.
        if (Units?.TableId(record) is { } id && read.Row(id) is { } row && (hpKnown is null || hpKnown == row.MaxHp))
        {
            identity = new EnemyIdentity(id, "table", 1);
        }
        else if (hpKnown is not { } hp || (identity = read.Identify(hp, namesSeen)) is null)
        {
            return null;
        }
        identified[record] = identity;
        // Joins the Debug log's BtlPackageUpdate head= line by record, to check whether the record holds the id.
        Log?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"enemy record=0x{record:x} id={identity.Id} by={identity.By} hp={hpKnown} candidates={identity.Candidates} names={namesSeen.Count}"));
        return identity;
    }
}
