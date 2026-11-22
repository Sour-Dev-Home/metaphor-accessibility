using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Each target a battle action hits is said by its number alone, or "n healed", and once the action's hits stop,
/// one summary of what each target that changed took (#247, the maintainer's wording of 2026-11-21 and 2026-11-22).
/// Hits in, announcements out; made-up
/// numbers and names only.</summary>
[Trait("Category", "Unit")]
public sealed class HitLinesTests : IDisposable
{
    private const int Number = 1;
    private const long Gap = HitLines.ActionGapMilliseconds;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly List<BattleSide> cues = [];
    private readonly HitLines lines;

    public HitLinesTests() =>
        lines = new HitLines(announcer, time, unit => unit == 3 ? "Rowan" : null, new Cue(cues));

    public void Dispose() => lines.Dispose();

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    private static BattleHit Enemy(long target, int change, int left, int kind = Number) =>
        new(target, change, left, kind, BattleSide.Enemy);

    private static BattleHit Ally(int slot, int unit, int change, int left) =>
        new(0x1000 + slot, change, left, Number, BattleSide.Ally, slot, Unit: unit);

    [Fact]
    public void DamageIsSaidAsItsNumberAlone()
    {
        lines.Observe(Enemy(0xa0, -34, 89));

        Assert.Equal(["34"], Said());
    }

    [Fact]
    public void AGainIsSaidAsHealed()
    {
        lines.Observe(Ally(0, 3, 20, 120));

        Assert.Equal(["20 healed"], Said());
    }

    [Fact]
    public void EachHitQueuesAfterTheLast()
    {
        lines.Observe(Enemy(0xa0, -10, 40));
        lines.Observe(Enemy(0xb0, -12, 30));

        Assert.Equal(["10", "12"], Said());
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
        lines.Observe(Enemy(0xa0, -10, 40, kind));
        time.Advance(Gap);

        Assert.Empty(Said());
    }

    [Fact]
    public void TheSameHitToldTwiceAtOnceIsSaidOnce()
    {
        lines.Observe(Enemy(0xa0, -10, 40));
        time.Advance(HitLines.RepeatMilliseconds - 1);
        lines.Observe(Enemy(0xa0, -10, 40));

        Assert.Equal(["10"], Said().Take(1));
        Assert.Single(Said(), said => said == "10");
    }

    // The sound-cue hook point: called with the target's side before its number.
    [Fact]
    public void TheCueHearsEachHitsSideFirst()
    {
        lines.Observe(Ally(1, 5, -3, 50));
        lines.Observe(Enemy(0xa0, -9, 1));

        Assert.Equal([BattleSide.Ally, BattleSide.Enemy], cues);
    }

    [Fact]
    public void AfterTheActionOneSummarySaysEachHurtMembersDamageByNameOrSlot()
    {
        lines.Observe(Ally(1, 5, -30, 60));
        lines.Observe(Ally(0, 3, -20, 89));
        time.Advance(Gap - 1);
        Assert.Equal(["30", "20"], Said());

        time.Advance(1);

        Assert.Equal(["30", "20", "Rowan 20, Second ally, 30"], Said());
    }

    [Fact]
    public void ALoneEnemyIsSaidWithoutANumber()
    {
        lines.Observe(Enemy(0xa0, -34, 89));
        time.Advance(Gap);

        Assert.Equal("Enemy, 34", Said().Last());
    }

    // Numbered by the order the battle first hit them; each once, with all the damage the action did it.
    [Fact]
    public void SeveralEnemiesAreNumberedAndEachSaidOnceWithItsDamage()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 70));
        lines.Observe(Enemy(0xa0, -10, 40));
        time.Advance(Gap);

        Assert.Equal("First enemy, 20. Second enemy, 10", Said().Last());
    }

    [Fact]
    public void OnlyWhoChangedIsInTheSummary()
    {
        lines.Observe(Enemy(0xa0, 0, 50));
        lines.Observe(Enemy(0xb0, -5, 20));
        time.Advance(Gap);

        Assert.Equal(["0", "5", "Second enemy, 5"], Said());
    }

    // The maintainer's call of 2026-11-22 (#631): the summary says what each target took, never the HP it has left,
    // which moves to the details key.
    [Fact]
    public void TheSummaryNeverSaysTheHpLeft()
    {
        lines.Observe(Enemy(0xa0, -52, 748));
        time.Advance(Gap);

        Assert.Equal(["52", "Enemy, 52"], Said());
        Assert.DoesNotContain(Said(), said => said.Contains("748", StringComparison.Ordinal));
    }

    [Fact]
    public void AHealInTheSummaryIsSaidAsHealed()
    {
        lines.Observe(Ally(0, 3, 20, 120));
        time.Advance(Gap);

        Assert.Equal("Rowan 20 healed", Said().Last());
    }

    [Fact]
    public void TheKnownEnemiesKeepTheirHpLeftAndMax()
    {
        lines.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));
        lines.Observe(Enemy(0xb0, -10, 50));

        Assert.Equal(
            [new KnownEnemy("First enemy", 840, 1200, 0xa0), new KnownEnemy("Second enemy", 50, null, 0xb0)],
            lines.KnownEnemies());
    }

    [Fact]
    public void EachActionHasItsOwnSummary()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        time.Advance(Gap);
        lines.Observe(Ally(0, 3, -7, 80));
        time.Advance(Gap);

        Assert.Equal(["10", "Enemy, 10", "7", "Rowan 7"], Said());
    }

    [Fact]
    public void ALongQuietStartsANewBattleThatForgetsItsEnemies()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 50));
        time.Advance(HitLines.BattleGapMilliseconds + 1);
        lines.Observe(Enemy(0xc0, -10, 30));
        time.Advance(Gap);

        Assert.Equal("Enemy, 10", Said().Last());
        Assert.Equal([new KnownEnemy("Enemy", 30, null, 0xc0)], lines.KnownEnemies());
    }

    // By ear (maintainer, 2026-11-21): "Ally 1 141" sounds like 1141, so a label never puts a digit next to the number.
    [Fact]
    public void ALabelFollowedByANumberNeverPutsTwoDigitRunsTogether()
    {
        using var nameless = new HitLines(announcer, time, _ => string.Empty);
        nameless.Observe(Ally(0, 3, -141, 9));
        nameless.Observe(Enemy(0xa0, -52, 9));
        nameless.Observe(Enemy(0xb0, -60, 9));
        time.Advance(Gap);

        var summary = Said().Last();

        Assert.Equal("First ally, 141. First enemy, 52. Second enemy, 60", summary);
        Assert.DoesNotMatch(@"\d\s+\d", summary);
    }

    // The summary runs on the timer's thread: a name lookup that throws must not escape it.
    [Fact]
    public void ANameLookupThatThrowsLeavesTheSummaryUnsaidWithoutEscaping()
    {
        using var throwing = new HitLines(announcer, time, _ => throw new InvalidOperationException("made-up failure"));
        throwing.Observe(Ally(0, 3, -5, 10));

        time.Advance(Gap);

        Assert.DoesNotContain(Said(), said => said.Contains("10", StringComparison.Ordinal));
    }

    private sealed class Cue(List<BattleSide> cues) : IHitCue
    {
        public void Before(BattleSide side) => cues.Add(side);
    }
}
