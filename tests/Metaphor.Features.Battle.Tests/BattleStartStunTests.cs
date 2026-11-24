using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Enemies stunned as a battle starts (#772, #626): the battle's enemy list, then its start wipe; each enemy
/// record's condition bit <c>0x40000</c>, as the 2026-11-24 log showed on all three enemies of a battle opened by a
/// field stun and on none of an ambush. Made-up records.</summary>
[Trait("Category", "Unit")]
public sealed class BattleStartStunTests
{
    private const long First = 0xA0;
    private const long Second = 0xB0;
    private const long Third = 0xC0;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ConditionBits conditions = new();
    private readonly BattleStartStun stun;
    private long now = 10_000;

    public BattleStartStunTests() => stun = new BattleStartStun(conditions, announcer, () => now);

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    private void Starts(int animation = 906)
    {
        now += 1_100;
        stun.Played(new LayoutAnimation(BattleStartLine.Layout, animation));
    }

    [Fact]
    public void EveryEnemyStunnedAtTheStartIsSaidOnce()
    {
        conditions[First] = conditions[Second] = conditions[Third] = BattleStartStun.StunBit;
        stun.Enemies(0x5000, [First, Second, Third]);
        Starts();
        Starts();

        Assert.Equal(["All 3 enemies stunned"], Said());
        Assert.False(announcer.Said[0].Interrupt);
    }

    [Fact]
    public void SomeOrOneEnemyStunned()
    {
        conditions[Second] = BattleStartStun.StunBit | 0x4;
        stun.Enemies(0x5000, [First, Second, Third]);
        Starts();
        conditions[First] = BattleStartStun.StunBit;
        stun.Enemies(0x6000, [First]);
        Starts();

        Assert.Equal(["1 of 3 enemies stunned", "Enemy stunned"], Said());
    }

    [Fact]
    public void NoStunAnUnreadRecordAnOldListOrAnotherLayoutSaysNothing()
    {
        conditions[First] = 0x80000;
        stun.Enemies(0x5000, [First, Second]);
        Starts(910);
        conditions[First] = BattleStartStun.StunBit;
        stun.Enemies(0x6000, [First]);
        stun.Played(new LayoutAnimation("TurnInfoLayout@ui@app", 906));
        now += BattleStartStun.ListMilliseconds + 1;
        stun.Played(new LayoutAnimation(BattleStartLine.Layout, 906));

        Assert.Empty(Said());
    }

    private sealed class ConditionBits : Dictionary<long, int>, IBattleUnits
    {
        public int? Hp(long record) => null;

        public int? Mp(long record) => null;

        public int? Conditions(long record) => TryGetValue(record, out var bits) ? bits : null;
    }
}
