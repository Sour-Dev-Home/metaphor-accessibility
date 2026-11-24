using System.Collections.Concurrent;
using System.Globalization;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>A battle list row's cost (#692 (b), #260): the row's name serial gives its label, whose MD5 finds the skill
/// in the game's skill tables (<see cref="SkillCosts"/>, docs/findings/skill-cost-from-skill-tables.md, build
/// 548ddc95…). Says "4 MP", "15 percent HP", or with the turn icons for the Synthesis list "2 turn icons, 12 MP". The
/// table's base cost is said; whether the list shows less under a passive or a halving effect is
/// <c>[NEEDS VERIFICATION]</c> by ear, as are the money cost's words and what the turn icon number counts (whole or
/// half icons). The menus' battle lists say it after the name (<see cref="BattleListAdditions"/>).</summary>
[FeatureSetting<BattleFeature>(BattleFeature.BattleSkillCosts)]
public sealed class SkillCostLines
{
    private readonly Func<int, string?> labelOf;
    private readonly Func<string, SkillCost?> costOf;
    private readonly Func<bool> on;
    private readonly ConcurrentDictionary<int, byte> logged = new();

    /// <param name="labelOf">A name serial's label, or null when unknown (<see cref="MessageSerials.FindBySerial"/>).
    /// </param>
    /// <param name="costOf">A label hash's cost (<see cref="SkillCostSource.CostOf"/>).</param>
    /// <param name="on">Whether costs are said now, read at each row.</param>
    public SkillCostLines(Func<int, string?> labelOf, Func<string, SkillCost?> costOf, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(labelOf);
        ArgumentNullException.ThrowIfNull(costOf);
        ArgumentNullException.ThrowIfNull(on);
        this.labelOf = labelOf;
        this.costOf = costOf;
        this.on = on;
    }

    /// <summary>A Debug build's log of each row's lookup, once a serial: its label, the skill id and the raw cost, for
    /// the by-ear check against the screen. None, when not set.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The cost said after the row named by <paramref name="nameSerial"/>, or null when off, unknown or free.
    /// </summary>
    public string? CostOf(int nameSerial, bool turnIcons)
    {
        if (!on() || labelOf(nameSerial) is not { } label)
        {
            return null;
        }
        var cost = costOf(MessageLibrary.LabelHash(label));
        if (Log is { } log && logged.TryAdd(nameSerial, 0))
        {
            log(cost is null
                ? string.Create(CultureInfo.InvariantCulture, $"serial={nameSerial} label={label} cost=none")
                : string.Create(CultureInfo.InvariantCulture,
                    $"serial={nameSerial} label={label} id={cost.Id} type={cost.Type} percent={cost.Percent} value={cost.Value} icons={cost.TurnIcons}"));
        }
        return cost is null ? null : Says(cost, turnIcons);
    }

    /// <summary>What a cost says, or null for none: the HP or MP cost, a percent before a flat amount, after the turn
    /// icons when <paramref name="turnIcons"/>.</summary>
    public static string? Says(SkillCost cost, bool turnIcons)
    {
        ArgumentNullException.ThrowIfNull(cost);
        List<string> said = [];
        if (turnIcons && cost.TurnIcons > 0)
        {
            said.Add(cost.TurnIcons == 1
                ? "1 turn icon"
                : string.Create(CultureInfo.InvariantCulture, $"{cost.TurnIcons} turn icons"));
        }
        if (cost.Costs)
        {
            var what = cost.Type switch
            {
                SkillCost.Hp => "HP",
                SkillCost.Mp => "MP",
                _ => "money",
            };
            said.Add(cost.Percent > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{cost.Percent} percent {what}")
                : string.Create(CultureInfo.InvariantCulture, $"{cost.Value} {what}"));
        }
        return said.Count > 0 ? string.Join(", ", said) : null;
    }
}

/// <summary>Starts reading the enemy tables (#164, #166) on the first input read it gets, as
/// <see cref="SkillCostLoad"/> does; every read reaches the game unchanged.</summary>
[FeatureSetting<BattleFeature>(BattleFeature.BattleAffinities, BattleFeature.BattleRewardExp)]
public sealed class EnemyTableLoad(EnemyTableSource source) : IInputControls
{
    private int started;

    public int Pad(int pad, int buttons, bool made)
    {
        Start();
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Start();
        return keys;
    }

    private void Start()
    {
        if (Volatile.Read(ref started) == 0 && Interlocked.Exchange(ref started, 1) == 0)
        {
            _ = source.Start();
        }
    }
}

/// <summary>Starts reading the skill tables (#692 (b)) on the first input read it gets, which only a game the hooks went
/// into sends; every read reaches the game unchanged.</summary>
[FeatureSetting<BattleFeature>(BattleFeature.BattleSkillCosts)]
public sealed class SkillCostLoad(SkillCostSource source) : IInputControls
{
    private int started;

    public int Pad(int pad, int buttons, bool made)
    {
        Start();
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Start();
        return keys;
    }

    private void Start()
    {
        if (Volatile.Read(ref started) == 0 && Interlocked.Exchange(ref started, 1) == 0)
        {
            _ = source.Start();
        }
    }
}
