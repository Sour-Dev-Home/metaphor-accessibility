using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle;

/// <summary>The enemy the battle's target cursor is on (#253): the last slot the cursor hook reported in this battle,
/// mapped to the battle's enemy list by <see cref="BattleTargetSlot"/>. Each move to another slot counts, so the enemy
/// HP keys can start again from the target after the cursor moves. A slot from an earlier battle counts as none.
/// </summary>
public sealed class TargetedEnemy(HitLines hits) : IBattleTargetObserver
{
    private readonly Lock gate = new();
    private int? slot;
    private long battle;
    private long moves;

    /// <summary>Told each report after this has noted it, such as the target line (#253); none when null. The cursor
    /// hook has one observer, so the battle's others hang off this one.</summary>
    public IBattleTargetObserver? Then { get; init; }

    public void Focused(long cursor, int slot)
    {
        var now = hits.Battle;
        lock (gate)
        {
            if (this.slot != slot || battle != now)
            {
                this.slot = slot;
                battle = now;
                moves++;
            }
        }
        Then?.Focused(cursor, slot);
    }

    /// <summary>The targeted enemy's index among <paramref name="enemies"/> (this battle's list) and the count of
    /// moves so far, or null when the cursor hasn't been on one of them in this battle.</summary>
    public (int Index, long Moves)? In(int enemies)
    {
        var now = hits.Battle;
        lock (gate)
        {
            return slot is { } at && battle == now && BattleTargetSlot.EnemyIndex(at, enemies) is { } index
                ? (index, moves)
                : null;
        }
    }
}
