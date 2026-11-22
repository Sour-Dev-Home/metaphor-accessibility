namespace Metaphor.Core.Tests;

/// <summary>What the nearby list's log record adds for a field enemy (#649, #696): its controller's current and
/// requested AI state and its HP, read from made-up memory.</summary>
[Trait("Category", "Unit")]
public sealed class FieldEnemyStateTests
{
    private const long Enemy = 0x1000;
    private const long Controller = 0x8000;
    private const long Stats = 0x9000;

    private readonly Dictionary<long, long> pointers = new()
    {
        [Enemy + FieldEnemyState.ControllerOffset] = Controller,
        [Enemy + FieldEnemyState.StatsOffset] = Stats,
    };

    private readonly Dictionary<long, int> ints = new()
    {
        // Current state 8 in the low half; the requested 30 and its pending flag 1 four bytes on.
        [Controller + FieldEnemyState.CurrentStateOffset] = 0x7777_0008,
        [Controller + FieldEnemyState.RequestedStateOffset] = 0x0001_001e,
        [Stats + FieldEnemyState.HpOffset] = 42,
    };

    private string Describe() =>
        FieldEnemyState.Describe(Enemy, address => pointers.TryGetValue(address, out var p) ? p : null,
            address => ints.TryGetValue(address, out var i) ? i : null);

    [Fact]
    public void ItSaysTheCurrentStateTheRequestedOneAndTheHp() =>
        Assert.Equal("aiState=8 aiNext=30 aiPending=1 hp=42", Describe());

    [Fact]
    public void ANullControllerLeavesTheStatesUnknown()
    {
        pointers[Enemy + FieldEnemyState.ControllerOffset] = 0;
        Assert.Equal("aiState=? aiNext=? aiPending=? hp=42", Describe());
    }

    [Fact]
    public void AnUnreadableStatsBlockLeavesTheHpUnknown()
    {
        pointers.Remove(Enemy + FieldEnemyState.StatsOffset);
        Assert.Equal("aiState=8 aiNext=30 aiPending=1 hp=?", Describe());
    }

    [Fact]
    public void ItThrowsOnAMissingReader() =>
        Assert.Throws<ArgumentNullException>(() => FieldEnemyState.Describe(Enemy, null!, _ => 0));
}
