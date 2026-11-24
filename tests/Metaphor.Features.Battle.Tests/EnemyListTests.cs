using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The battle's enemy list from its start (#750, #253; the maintainer's decision of 2026-11-23): hits are
/// numbered by the list, so the first already has its ordinal, and the enemy HP keys step through every enemy, hit or
/// not, from the first in the list, with "n of total" (<c>speech.positions</c>). Behind <c>battleEnemyList</c>.
/// Made-up records and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class EnemyListTests
{
    private const int Number = 1;
    private const long Package = 0x5000;
    private const int KeyJ = 0x4A;
    private const int KeyK = 0x4B;
    private const int KeyL = 0x4C;

    private readonly RecordingAnnouncer lines = new();
    private readonly RecordingAnnouncer keys = new();
    private readonly ManualTime time = new();
    private readonly Units units = new();
    private readonly List<(BattleSide Side, int Index, int Count)> cues = [];
    private bool listOn = true;
    private bool numbersOnly;
    private bool positions = true;
    private bool maxHp = true;
    private readonly HitLines hits;
    private readonly BattleEnemyHp enemyHp;

    public EnemyListTests()
    {
        hits = new HitLines(lines, time, _ => null)
        {
            ListOn = () => listOn,
            NumbersOnly = () => numbersOnly,
            SlotCue = (side, index, count) => cues.Add((side, index, count)),
        };
        enemyHp = new BattleEnemyHp(() => BattleBindings.Default, hits, new Scene(), keys)
        {
            Units = units,
            ListOn = () => listOn,
            SayPositions = () => positions,
            SayMaxHp = () => maxHp,
        };
    }

    private IEnumerable<string> Said() => lines.Said.Select(said => said.Text);

    private string LastKey() => keys.Said[^1].Text;

    private void Press(int key)
    {
        enemyHp.Keyboard.Keys([key]);
        enemyHp.Keyboard.Keys([]);
    }

    private static BattleHit Enemy(long target, int change, int left, int? max = null) =>
        new(target, change, left, Number, BattleSide.Enemy, HpMax: max);

    [Fact]
    public void TheFirstHitAlreadyHasItsPlaceInTheList()
    {
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);

        hits.Observe(Enemy(0xB0, -52, 900));

        Assert.Equal(["Second enemy, 52"], Said());
    }

    [Fact]
    public void ABattleOfOneEnemySaysEnemy()
    {
        hits.Enemies(Package, [0xA0]);

        hits.Observe(Enemy(0xA0, -52, 900));

        Assert.Equal(["Enemy, 52"], Said());
    }

    [Fact]
    public void OffTheListIsIgnoredAndHitsAreNumberedAsTheyCome()
    {
        listOn = false;
        hits.Enemies(Package, [0xA0, 0xB0]);

        hits.Observe(Enemy(0xB0, -52, 900));

        Assert.Equal(["Enemy, 52"], Said());
    }

    [Fact]
    public void ADefeatThatShrinksTheListKeepsEveryOrdinal()
    {
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);
        hits.Enemies(Package, [0xA0, 0xC0]);

        hits.Observe(Enemy(0xC0, -52, 900));

        Assert.Equal(["Third enemy, 52"], Said());
    }

    [Fact]
    public void ANewPackageIsANewBattle()
    {
        hits.Enemies(Package, [0xA0, 0xB0]);
        hits.Enemies(0x6000, [0xB0]);

        hits.Observe(Enemy(0xB0, -52, 900));

        Assert.Equal(["Enemy, 52"], Said());
    }

    [Fact]
    public void AListSeenLongBeforeTheFirstHitIsStillThisBattles()
    {
        hits.Enemies(Package, [0xA0, 0xB0]);
        time.Advance(HitLines.BattleGapMilliseconds / 2);
        hits.Enemies(Package, [0xA0, 0xB0]);
        time.Advance(HitLines.BattleGapMilliseconds / 2 + 1000);

        hits.Observe(Enemy(0xB0, -52, 900));

        Assert.Equal(["Second enemy, 52"], Said());
    }

    [Fact]
    public void NumbersOnlySaysTheNumberAndCuesTheEnemysPlaceInTheList()
    {
        numbersOnly = true;
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);

        hits.Observe(Enemy(0xC0, -52, 0));
        hits.Observe(new BattleHit(0x1001, -30, 70, Number, BattleSide.Ally, Slot: 1));

        Assert.Equal(["52, defeated", "30"], Said());
        Assert.Equal([(BattleSide.Enemy, 2, 3), (BattleSide.Ally, 1, HitLines.PartySlots)], cues);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 3, -0.8)]
    [InlineData(1, 3, 0)]
    [InlineData(2, 3, 0.8)]
    [InlineData(1, 5, -0.4)]
    public void PlacesSoundFromLeftToRightInEvenSteps(int index, int count, double pan) =>
        Assert.Equal(pan, HitLines.Pan(index, count), 6);

    [Fact]
    public void WithoutNumbersOnlyNoSlotCuePlays()
    {
        hits.Enemies(Package, [0xA0, 0xB0]);

        hits.Observe(Enemy(0xB0, -52, 900));

        Assert.Empty(cues);
    }

    [Fact]
    public void TheNextKeyStepsThroughEveryEnemyHitOrNotFromTheFirst()
    {
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);
        units.Hp[0xA0] = 1320;
        hits.Observe(Enemy(0xB0, -360, 840, max: 1200));

        Press(KeyK);
        Press(KeyK);
        Press(KeyK);
        Press(KeyK);

        Assert.Equal(
            ["First enemy, 1320 left, 1 of 3", "Second enemy, 840 of 1200, 2 of 3", "Third enemy, not hit yet, 3 of 3",
                "First enemy, 1320 left, 1 of 3"],
            keys.Said.Select(said => said.Text));
    }

    [Fact]
    public void ThePreviousKeyStartsFromTheLast()
    {
        hits.Enemies(Package, [0xA0, 0xB0]);
        units.Hp[0xB0] = 500;

        Press(KeyJ);

        Assert.Equal("Second enemy, 500 left, 2 of 2", LastKey());
    }

    [Fact]
    public void PositionsAndMaxHpOffLeaveThemOut()
    {
        positions = false;
        maxHp = false;
        hits.Enemies(Package, [0xA0, 0xB0]);
        hits.Observe(Enemy(0xA0, -360, 840, max: 1200));

        Press(KeyK);

        Assert.Equal("First enemy, 840 left", LastKey());
    }

    [Fact]
    public void TheAllEnemiesKeySaysEveryEnemyInTheList()
    {
        hits.Enemies(Package, [0xA0, 0xB0]);
        units.Hp[0xB0] = 500;
        hits.Observe(Enemy(0xA0, -360, 840, max: 1200));

        Press(KeyL);

        Assert.Equal("First enemy, 840 of 1200. Second enemy, 500 left", LastKey());
    }

    [Fact]
    public void OffTheListTheKeysStepOnlyThroughTheEnemiesHit()
    {
        listOn = false;
        hits.Enemies(Package, [0xA0, 0xB0]);
        hits.Observe(Enemy(0xB0, -360, 840, max: 1200));

        Press(KeyK);

        Assert.Equal("Enemy, 840 of 1200", LastKey());
    }

    private sealed class Units : IBattleUnits
    {
        public Dictionary<long, int> Hp { get; } = [];

        int? IBattleUnits.Hp(long record) => Hp.TryGetValue(record, out var hp) ? hp : null;

        public int? Mp(long record) => null;
    }

    private sealed class Scene : IBattleScene
    {
        public bool InBattle() => true;
    }
}
