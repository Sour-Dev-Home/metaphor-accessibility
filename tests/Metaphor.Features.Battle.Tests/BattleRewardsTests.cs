using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The results screen's money and MAG (#166): the purse at the battle's start wipe against the purse after
/// "Victory", said once it settles. A fake purse and the layout animations the 2026-11-22 and 2026-11-23 Debug logs
/// recorded in, announcements out; the amounts are made up.</summary>
[Trait("Category", "Unit")]
public sealed class BattleRewardsTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly FakePurse purse = new() { MoneyNow = 1000, MagNow = 50 };
    private readonly BattleRewards rewards;

    public BattleRewardsTests() => rewards = new BattleRewards(purse, announcer, time);

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    private void Start() => rewards.Played(new LayoutAnimation(BattleStartLine.Layout, 906));

    private void Won() =>
        rewards.Played(new LayoutAnimation(BattleEndLine.Layout, BattleEndLine.VictoryAnimation));

    private void Wait(long milliseconds)
    {
        for (var waited = 0L; waited < milliseconds; waited += 50)
        {
            time.Advance(50);
            rewards.Keys([]);
        }
    }

    [Fact]
    public void AWinSaysTheMoneyAndMagGainedOnceTheyHold()
    {
        Start();
        Won();
        Wait(500);
        purse.MoneyNow = 1240;
        Wait(100);
        purse.MagNow = 80;
        Wait(BattleRewards.SettleMilliseconds + 200);

        Assert.Equal(["Gained 240 money and 30 MAG"], Said());
        Assert.False(announcer.Said[0].Interrupt);
    }

    [Fact]
    public void WithRewardExpOnTheTablesExpIsSaidAtVictoryAndThePurseAfter()
    {
        var table = ((int Exp, int ArchetypeExp, int Money, int Magla)?)(160, 60, 125, 350);
        var withExp = new BattleRewards(purse, announcer, time)
        {
            TableRewards = () => table,
            ExpOn = () => true,
        };
        withExp.Played(new LayoutAnimation(BattleStartLine.Layout, 906));
        withExp.Played(new LayoutAnimation(BattleEndLine.Layout, BattleEndLine.VictoryAnimation));
        // #921: the purse rose 43 s after "Victory", as the results closed; the EXP is said as they open.
        Assert.Equal(["Gained 160 EXP and 60 archetype EXP"], Said());
        Assert.False(announcer.Said[0].Interrupt);
        table = null;
        purse.MoneyNow = 1240;
        purse.MagNow = 80;
        for (var waited = 0L; waited < BattleRewards.SettleMilliseconds * 3; waited += 50)
        {
            time.Advance(50);
            withExp.Keys([]);
        }

        Assert.Equal(["Gained 160 EXP and 60 archetype EXP", "Gained 240 money and 30 MAG"], Said());
    }

    [Fact]
    public void RewardExpOffOrUnknownSaysNoExpLine()
    {
        var off = new BattleRewards(purse, announcer, time)
        {
            TableRewards = () => (160, 60, 125, 350),
            ExpOn = () => false,
        };
        var unknown = new BattleRewards(purse, announcer, time)
        {
            TableRewards = () => null,
            ExpOn = () => true,
        };
        foreach (var each in new[] { off, unknown })
        {
            each.Played(new LayoutAnimation(BattleStartLine.Layout, 906));
            each.Played(new LayoutAnimation(BattleEndLine.Layout, BattleEndLine.VictoryAnimation));
        }

        Assert.Empty(Said());
    }

    [Fact]
    public void OnlyWhatRoseIsSaidAndOnlyOnce()
    {
        Start();
        Won();
        purse.MagNow = 62;
        Wait(BattleRewards.SettleMilliseconds * 3);
        purse.MagNow = 70;
        Wait(BattleRewards.SettleMilliseconds * 3);

        Assert.Equal(["Gained 12 MAG"], Said());
    }

    [Fact]
    public void AnEscapeOrALossSaysNothing()
    {
        Start();
        rewards.Played(new LayoutAnimation(BattleEndLine.Layout, BattleEndLine.EscapedAnimation));
        purse.MoneyNow = 900;
        Wait(BattleRewards.SettleMilliseconds * 3);

        Assert.Empty(Said());
    }

    [Fact]
    public void AWinWithNoChangeForTheWholeWaitSaysNothingAfter()
    {
        Start();
        Won();
        Wait(BattleRewards.WaitMilliseconds + 100);
        purse.MoneyNow = 2000;
        Wait(BattleRewards.SettleMilliseconds * 3);

        Assert.Empty(Said());
    }

    [Fact]
    public void AWinWithNoStartSeenOrAnUnreadPurseSaysNothing()
    {
        Won();
        purse.MoneyNow = 2000;
        Wait(BattleRewards.SettleMilliseconds * 3);
        purse.MoneyNow = null;
        Start();
        purse.MoneyNow = 1000;
        Won();
        purse.MoneyNow = 3000;
        Wait(BattleRewards.SettleMilliseconds * 3);

        Assert.Empty(Said());
    }

    private sealed class FakePurse : IBattlePurse
    {
        public int? MoneyNow { get; set; }

        public int? MagNow { get; set; }

        public int? Money() => MoneyNow;

        public int? Mag() => MagNow;
    }
}
