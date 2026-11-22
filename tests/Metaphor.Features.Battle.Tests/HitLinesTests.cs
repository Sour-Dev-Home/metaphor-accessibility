using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Each target a battle action hits is said as it lands, with its target and what it took: "Enemy, 34",
/// "Rowan 20 healed" (#247, #738: the maintainer's call of 2026-11-22, per hit with target, no later summary, no line
/// for a hit with no change). Hits in, announcements out; made-up numbers and names only.</summary>
[Trait("Category", "Unit")]
public sealed class HitLinesTests
{
    private const int Number = 1;
    private const long LongWait = 10_000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly List<BattleSide> cues = [];
    private readonly HitLines lines;

    public HitLinesTests() =>
        lines = new HitLines(announcer, time, unit => unit == 3 ? "Rowan" : null, new Cue(cues));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    private static BattleHit Enemy(long target, int change, int left, int kind = Number) =>
        new(target, change, left, kind, BattleSide.Enemy);

    private static BattleHit Ally(int slot, int unit, int change, int left) =>
        new(0x1000 + slot, change, left, Number, BattleSide.Ally, slot, Unit: unit);

    [Fact]
    public void ALoneEnemysDamageIsSaidWithItsTarget()
    {
        lines.Observe(Enemy(0xa0, -34, 89));

        Assert.Equal(["Enemy, 34"], Said());
    }

    [Fact]
    public void AMembersDamageIsSaidByNameOrSlot()
    {
        lines.Observe(Ally(0, 3, -20, 89));
        lines.Observe(Ally(1, 5, -30, 60));

        Assert.Equal(["Rowan 20", "Second ally, 30"], Said());
    }

    [Fact]
    public void AHitThatTakesAnAllyToZeroSaysKnockedOutAfterItsNumber()
    {
        lines.Observe(Ally(0, 3, -40, 0));
        lines.Observe(Ally(1, 5, -30, 0));

        Assert.Equal(["Rowan 40, knocked out", "Second ally, 30, knocked out"], Said());
    }

    [Fact]
    public void AHitThatTakesAnEnemyToZeroSaysDefeated()
    {
        lines.Observe(Enemy(0xa0, -52, 0));

        Assert.Equal(["Enemy, 52, defeated"], Said());
    }

    [Fact]
    public void AnAllyLeftWithHpIsNotKnockedOut()
    {
        lines.Observe(Ally(0, 3, -40, 1));

        Assert.Equal(["Rowan 40"], Said());
    }

    [Fact]
    public void AGainFromZeroSaysRevived()
    {
        lines.Observe(Ally(0, 3, 25, 25));

        Assert.Equal(["Rowan 25 healed, revived"], Said());
    }

    [Fact]
    public void AGainOnALivingAllyDoesNotSayRevived()
    {
        lines.Observe(Ally(0, 3, 25, 80));

        Assert.Equal(["Rowan 25 healed"], Said());
    }

    [Fact]
    public void AGainIsSaidAsHealed()
    {
        lines.Observe(Ally(0, 3, 20, 120));

        Assert.Equal(["Rowan 20 healed"], Said());
    }

    [Fact]
    public void EachHitQueuesAfterTheLastAndNoSummaryFollows()
    {
        lines.Observe(Enemy(0xa0, -10, 40));
        lines.Observe(Enemy(0xb0, -12, 30));
        time.Advance(LongWait);

        Assert.Equal(["Enemy, 10", "Second enemy, 12"], Said());
        Assert.All(announcer.Said, said => Assert.False(said.Interrupt));
    }

    // Numbered by the order the battle first hit them.
    [Fact]
    public void SeveralEnemiesAreNumberedByTheOrderFirstHit()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 70));
        lines.Observe(Enemy(0xa0, -20, 30));

        Assert.Equal(["Enemy, 10", "Second enemy, 10", "First enemy, 20"], Said());
    }

    [Theory]
    [InlineData(BattleSide.Ally)]
    [InlineData(BattleSide.Enemy)]
    public void AHitWithNoChangeSaysNothing(BattleSide side)
    {
        lines.Observe(new BattleHit(0xa0, 0, 124, Number, side, side == BattleSide.Ally ? 0 : null, Unit: 3));
        time.Advance(LongWait);

        Assert.Empty(Said());
        Assert.Empty(cues);
    }

    // Which number-less kind is a miss, a block or a drain isn't known yet, so none of them is said.
    [Theory]
    [InlineData(4)]
    [InlineData(0x100)]
    [InlineData(0x200)]
    [InlineData(0)]
    public void AKindWithoutANumberSaysNothing(int kind)
    {
        lines.Observe(Enemy(0xa0, -10, 40, kind));

        Assert.Empty(Said());
    }

    [Fact]
    public void TheSameHitToldTwiceAtOnceIsSaidOnce()
    {
        lines.Observe(Enemy(0xa0, -10, 40));
        time.Advance(HitLines.RepeatMilliseconds - 1);
        lines.Observe(Enemy(0xa0, -10, 40));

        Assert.Equal(["Enemy, 10"], Said());
    }

    // The sound-cue hook point: called with the target's side before its line.
    [Fact]
    public void TheCueHearsEachHitsSideFirst()
    {
        lines.Observe(Ally(1, 5, -3, 50));
        lines.Observe(Enemy(0xa0, -9, 1));

        Assert.Equal([BattleSide.Ally, BattleSide.Enemy], cues);
    }

    [Fact]
    public void TheLineNeverSaysTheHpLeft()
    {
        lines.Observe(Enemy(0xa0, -52, 748));

        Assert.Equal(["Enemy, 52"], Said());
    }

    // A hit with no change is still a hit on that enemy: the status and details keys know it.
    [Fact]
    public void TheKnownEnemiesKeepTheirHpLeftAndMax()
    {
        lines.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));
        lines.Observe(Enemy(0xb0, 0, 50));

        Assert.Equal(
            [new KnownEnemy("First enemy", 840, 1200, 0xa0), new KnownEnemy("Second enemy", 50, null, 0xb0)],
            lines.KnownEnemies());
    }

    [Fact]
    public void ALongQuietStartsANewBattleThatForgetsItsEnemies()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 50));
        time.Advance(HitLines.BattleGapMilliseconds + 1);
        lines.Observe(Enemy(0xc0, -10, 30));

        Assert.Equal("Enemy, 10", Said().Last());
        Assert.Equal([new KnownEnemy("Enemy", 30, null, 0xc0)], lines.KnownEnemies());
    }

    // By ear (maintainer, 2026-11-21): "Ally 1 141" sounds like 1141, so a label never puts a digit next to the number.
    [Fact]
    public void ALabelFollowedByANumberNeverPutsTwoDigitRunsTogether()
    {
        var nameless = new HitLines(announcer, time, _ => string.Empty);
        nameless.Observe(Ally(0, 3, -141, 9));
        nameless.Observe(Enemy(0xa0, -52, 9));
        nameless.Observe(Enemy(0xb0, -60, 9));

        Assert.Equal(["First ally, 141", "Enemy, 52", "Second enemy, 60"], Said());
        Assert.All(Said(), line => Assert.DoesNotMatch(@"\d\s+\d", line));
    }

    private sealed class Cue(List<BattleSide> cues) : IHitCue
    {
        public void Before(BattleSide side) => cues.Add(side);
    }
}
