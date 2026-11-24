using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says a won battle's money and MAG (#166): "Gained 240 money and 30 MAG". The purse is read as the battle's
/// start wipe plays and again after "Victory"; what rose is said, queued, once both have held for
/// <see cref="SettleMilliseconds"/>. The results screen's numbers aren't drawn through anything the mod reads (the
/// 2026-11-23 review on #166), so the purse stands in for them. With <see cref="ExpOn"/> the defeated enemies' table EXP and archetype EXP
/// join the line (<see cref="TableRewards"/>); spoils aren't read yet. Takes each input read
/// only as a clock, at most once every <see cref="ReadEveryMilliseconds"/>, and only for
/// <see cref="WaitMilliseconds"/> after a win. Every read reaches the game unchanged. Safe to call from several
/// threads.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): money and MAG are the game data block's <c>int</c>s at
/// <c>+0x22c58</c> and <c>+0x233fc</c> (docs/findings/money-follower-rank-and-shop-static.md,
/// docs/findings/menu-values-party-unit-static.md); the start wipe and victory animations are in
/// docs/findings/battle-turn-side-start-and-end-layouts-logged.md. When the game adds the rewards, before or after the
/// results screen, is <c>[NEEDS VERIFICATION]</c> by ear (docs/findings/battle-results-rewards-static.md).</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleRewards, BattleFeature.BattleRewardExp)]
public sealed class BattleRewards : ILayoutAnimationObserver, IInputControls
{
    /// <summary>The least time between two reads of the purse after a win.</summary>
    public const long ReadEveryMilliseconds = 100;

    /// <summary>How long a changed purse holds before it is said: a guess to check by ear.</summary>
    public const long SettleMilliseconds = 500;

    /// <summary>How long after a win the purse is watched.</summary>
    public const long WaitMilliseconds = 60_000;

    private readonly IBattlePurse purse;
    private readonly IAnnouncer announcer;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private (int Money, int Mag)? atStart;
    private (int Exp, int ArchetypeExp, int Money, int Magla)? atVictory;
    private long? wonAt;
    private long lastRead = long.MinValue / 2;
    private (int Money, int Mag)? changed;
    private long changedSince;

    /// <summary>Rewards read from <paramref name="purse"/> and said through <paramref name="announcer"/>, timed by
    /// <paramref name="time"/>.</summary>
    public BattleRewards(IBattlePurse purse, IAnnouncer announcer, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(purse);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        this.purse = purse;
        this.announcer = announcer;
        this.time = time;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [BattleStartLine.Layout, BattleEndLine.Layout];

    /// <summary>A start wipe takes the purse; "Victory" starts the watch; another ending stops it.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout == BattleStartLine.Layout)
        {
            var start = Read();
            lock (gate)
            {
                atStart = start;
                wonAt = null;
                changed = null;
            }
        }
        else if (played.Layout == BattleEndLine.Layout &&
            played.Animation is BattleEndLine.VictoryAnimation or BattleEndLine.EscapedAnimation
                or BattleEndLine.DefeatedAnimation)
        {
            var won = played.Animation == BattleEndLine.VictoryAnimation;
            // The defeated enemies' table values, taken as "Victory" plays, before the battle's enemies are forgotten.
            var table = won && ExpOn() ? TableRewards?.Invoke() : null;
            lock (gate)
            {
                wonAt = won && atStart is not null ? Now() : null;
                changed = null;
                atVictory = table;
            }
        }
    }

    /// <summary>The defeated enemies' EXP, archetype EXP, money and magla by the enemy tables (#166), or null when
    /// unknown; none when unset.</summary>
    public Func<(int Exp, int ArchetypeExp, int Money, int Magla)?>? TableRewards { get; init; }

    /// <summary>Whether the table's EXP is said (<see cref="BattleFeature.BattleRewardExp"/>); off when unset.</summary>
    public Func<bool> ExpOn { get; init; } = () => false;

    /// <summary>Hears the table's values beside the purse's (Debug builds), to check whether the results screen scales
    /// them; none when null.</summary>
    public Action<string>? Log { get; init; }

    public int Pad(int pad, int buttons, bool made)
    {
        Tick();
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Tick();
        return keys;
    }

    /// <summary>What a rise in the purse says, with the table's EXP and archetype EXP when above 0, or null when nothing
    /// rose.</summary>
    public static string? Says(int money, int mag, int exp = 0, int archetypeExp = 0)
    {
        var parts = new List<string>();
        if (money > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{money} money"));
        }
        if (mag > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{mag} MAG"));
        }
        if (parts.Count == 0)
        {
            return null;
        }
        if (exp > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{exp} EXP"));
        }
        if (archetypeExp > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{archetypeExp} archetype EXP"));
        }
        return "Gained " + (parts.Count == 1
            ? parts[0]
            : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1]);
    }

    private void Tick()
    {
        var now = Now();
        string? said = null;
        string? logged = null;
        lock (gate)
        {
            if (wonAt is not { } won || atStart is not { } start || now - lastRead < ReadEveryMilliseconds)
            {
                return;
            }
            lastRead = now;
            if (now - won > WaitMilliseconds)
            {
                wonAt = null;
                return;
            }
            if (Read() is not { } current || current == start)
            {
                changed = null;
                return;
            }
            if (changed != current)
            {
                changed = current;
                changedSince = now;
                return;
            }
            if (now - changedSince < SettleMilliseconds)
            {
                return;
            }
            var table = atVictory;
            said = Says(current.Money - start.Money, current.Mag - start.Mag, table?.Exp ?? 0, table?.ArchetypeExp ?? 0);
            logged = string.Create(CultureInfo.InvariantCulture,
                $"purse money={current.Money - start.Money} mag={current.Mag - start.Mag} table exp={table?.Exp} aexp={table?.ArchetypeExp} money={table?.Money} magla={table?.Magla}");
            wonAt = null;
            atStart = null;
            changed = null;
            atVictory = null;
        }
        if (logged is not null)
        {
            Log?.Invoke(logged);
        }
        if (said is not null)
        {
            announcer.Announce(new Announcement(said, Interrupt: false));
        }
    }

    private (int Money, int Mag)? Read() =>
        purse.Money() is { } money && purse.Mag() is { } mag ? (money, mag) : null;

    private long Now() => time.GetUtcNow().ToUnixTimeMilliseconds();
}
