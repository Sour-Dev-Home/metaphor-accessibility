namespace Metaphor.Core.Battle;

/// <summary>Told each slot the battle's target cursor is focused on (#253): the cursor object and the index the game
/// passed its vtable's slot 4
/// (docs/findings/battle-target-cursor-index-static.md). Called on the game's thread, possibly every frame.</summary>
public interface IBattleTargetObserver
{
    void Focused(long cursor, int slot);
}

/// <summary>An observer that ignores the target cursor: what the cursor hook reports to when nothing uses it.</summary>
public sealed class NoBattleTarget : IBattleTargetObserver
{
    public void Focused(long cursor, int slot)
    {
    }
}

/// <summary>Which of the battle's enemies a target cursor slot is (#253). On build 548ddc95… the slot indexes the
/// cursor's meshes, one per target position, and that mesh <c>i</c> is enemy <c>i</c> of the battle package's list
/// (<see cref="BattleEnemyList"/>) is a guess, <c>[NEEDS VERIFICATION]</c> by ear: a formation with an empty slot may
/// shift it (docs/findings/battle-target-cursor-index-static.md).</summary>
public static class BattleTargetSlot
{
    /// <summary>The list index of the enemy at <paramref name="slot"/> among <paramref name="enemies"/>, or null when
    /// the slot is past the list.</summary>
    public static int? EnemyIndex(int slot, int enemies) => slot >= 0 && slot < enemies ? slot : null;
}
