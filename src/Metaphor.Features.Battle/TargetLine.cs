using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says the enemy the target cursor moves to (#253): "Second enemy, 840 of 1320, 2 of 3", the enemy HP keys'
/// line with "n of total" (<c>speech.positions</c>), cutting off the line before (ADR 0023). It hears the slot the
/// cursor's vtable swap already reports (<see cref="TargetedEnemy.Then"/>) and maps it to the battle's enemy list
/// (<see cref="BattleTargetSlot"/>), so it needs <c>battleEnemyList</c>; a slot past the list says nothing. The game
/// may report the same slot every frame, so a report is said only when the cursor, its slot or the battle changed, or
/// after <see cref="QuietMilliseconds"/> with no report, which is read as the cursor opened again. Safe to call from
/// any of the game's threads.</summary>
/// <remarks>Which enemy a slot is, whether the ally-side cursor reports through the same function, and how often the
/// game calls it are <c>[NEEDS VERIFICATION]</c> by ear (docs/findings/battle-target-cursor-index-static.md), so this
/// waits for its by-ear check as a testing setting. The Weak or Resist tag the screen shows (#253's spec) has no known
/// read yet.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHits, BattleFeature.BattleEnemyList, BattleFeature.BattleTargetSpeech)]
public sealed class TargetLine : IBattleTargetObserver
{
    /// <summary>How long without a report before the same slot is said again: a guess to check by ear.</summary>
    public const long QuietMilliseconds = 500;

    private readonly HitLines hits;
    private readonly IAnnouncer announcer;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private (long Cursor, int Slot, long Battle)? last;
    private long lastAt;

    /// <summary>Lines said through <paramref name="announcer"/> from <paramref name="hits"/>' enemy list, timed by
    /// <paramref name="time"/>.</summary>
    public TargetLine(HitLines hits, IAnnouncer announcer, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        this.hits = hits;
        this.announcer = announcer;
        this.time = time;
    }

    /// <summary>Whether a move is said: the target line's setting and the enemy list's; on when unset.</summary>
    public Func<bool> On { get; init; } = () => true;

    /// <summary>Whether a line says "of max HP" when the maximum is known (#854); on when unset.</summary>
    public Func<bool> SayMaxHp { get; init; } = () => true;

    /// <summary>Whether a line ends in "n of total" (<c>speech.positions</c>); on when unset.</summary>
    public Func<bool> SayPositions { get; init; } = () => true;

    /// <summary>Reads an enemy's HP before a hit has shown it; none when null.</summary>
    public IBattleUnits? Units { get; init; }

    /// <summary>Says the enemy at <paramref name="slot"/> when the report is a move.</summary>
    public void Focused(long cursor, int slot)
    {
        if (!On())
        {
            return;
        }
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var at = (cursor, slot, hits.Battle);
        lock (gate)
        {
            var same = last == at && now - lastAt < QuietMilliseconds;
            last = at;
            lastAt = now;
            if (same)
            {
                return;
            }
        }
        var enemies = hits.AllEnemies();
        if (BattleTargetSlot.EnemyIndex(slot, enemies.Count) is not { } index)
        {
            return;
        }
        var said = BattleEnemyHp.Line(enemies[index], Units, SayMaxHp());
        var line = SayPositions()
            ? string.Create(CultureInfo.InvariantCulture, $"{said}, {index + 1} of {enemies.Count}")
            : said;
        announcer.Announce(new Announcement(
            Affinities?.Invoke(enemies[index]) is { } affinities ? $"{line}, {affinities}" : line, Interrupt: true));
    }

    /// <summary>What an enemy's affinities add after its line (#164), or null for nothing; none when unset.</summary>
    public Func<ListedEnemy, string?>? Affinities { get; init; }
}
