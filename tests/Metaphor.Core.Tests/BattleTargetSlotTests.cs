using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests;

/// <summary>Which enemy a target cursor slot is (#253): slot <c>i</c> is enemy <c>i</c> of the battle's list, none past
/// it. Made-up counts only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleTargetSlotTests
{
    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(5, 6, 5)]
    public void ASlotInTheListIsThatEnemy(int slot, int enemies, int index) =>
        Assert.Equal(index, BattleTargetSlot.EnemyIndex(slot, enemies));

    [Theory]
    [InlineData(3, 3)]
    [InlineData(9, 6)]
    [InlineData(-1, 3)]
    [InlineData(0, 0)]
    public void ASlotPastTheListIsNoEnemy(int slot, int enemies) =>
        Assert.Null(BattleTargetSlot.EnemyIndex(slot, enemies));
}
