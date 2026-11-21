using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Each target a battle action hits is said as its HP change and the HP it has left (#247), from what
/// <c>BattleHitResult</c> wrote (docs/findings/battle-hit-result-hp-left-static.md). Hits in, announcements out; made-up
/// numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class HitLinesTests
{
    private const int Number = 1;

    private readonly RecordingAnnouncer announcer = new();
    private long now = 10_000;
    private readonly HitLines lines;

    public HitLinesTests() => lines = new HitLines(announcer, () => now);

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void ANegativeChangeIsSaidAsDamageWithTheHpLeft()
    {
        lines.Observe(new BattleHit(0xa0, HpChange: -35, HpLeft: 89, Number));

        Assert.Equal(["35 damage, 89 HP left"], Said());
    }

    [Fact]
    public void APositiveChangeIsSaidAsHealing()
    {
        lines.Observe(new BattleHit(0xa0, HpChange: 20, HpLeft: 120, 0x400));

        Assert.Equal(["20 healed, 120 HP left"], Said());
    }

    [Fact]
    public void NoChangeIsSaidAsNoDamage()
    {
        lines.Observe(new BattleHit(0xa0, HpChange: 0, HpLeft: 50, 0x1000));

        Assert.Equal(["no damage, 50 HP left"], Said());
    }

    // Outcomes queue, so a fast run of hits loses none (spec #247, story 19).
    [Fact]
    public void EachHitQueuesAfterTheLast()
    {
        lines.Observe(new BattleHit(0xa0, -10, 40, Number));
        lines.Observe(new BattleHit(0xb0, -12, 30, Number));

        Assert.Equal(["10 damage, 40 HP left", "12 damage, 30 HP left"], Said());
        Assert.All(announcer.Said, said => Assert.False(said.Interrupt));
    }

    // Which number-less kind is a miss, a block or a drain isn't known yet, so none of them is said.
    [Theory]
    [InlineData(4)]
    [InlineData(0x100)]
    [InlineData(0x200)]
    [InlineData(0)]
    public void AKindWithoutANumberSaysNothing(int kind)
    {
        lines.Observe(new BattleHit(0xa0, -10, 40, kind));

        Assert.Empty(Said());
    }

    [Fact]
    public void TheSameHitToldTwiceAtOnceIsSaidOnce()
    {
        lines.Observe(new BattleHit(0xa0, -10, 40, Number));
        now += HitLines.RepeatMilliseconds - 1;
        lines.Observe(new BattleHit(0xa0, -10, 40, Number));

        Assert.Equal(["10 damage, 40 HP left"], Said());
    }

    [Fact]
    public void TheSameNumbersLaterAreANewHit()
    {
        lines.Observe(new BattleHit(0xa0, -10, 0, Number));
        now += HitLines.RepeatMilliseconds;
        lines.Observe(new BattleHit(0xa0, -10, 0, Number));

        Assert.Equal(["10 damage, 0 HP left", "10 damage, 0 HP left"], Said());
    }
}
