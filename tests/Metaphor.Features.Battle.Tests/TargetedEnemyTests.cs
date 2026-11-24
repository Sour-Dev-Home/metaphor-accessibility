using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The enemy HP keys from the targeted enemy (#253; the maintainer's decision of 2026-11-23): after the target
/// cursor moves, K or J says the enemy it is on and steps on from there, and L starts from it; with no target in this
/// battle they start from the first. Behind <c>battleEnemyTarget</c> and <c>battleEnemyList</c>. Made-up records and
/// numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class TargetedEnemyTests
{
    private const long Package = 0x5000;
    private const long Cursor = 0x9000;
    private const int KeyJ = 0x4A;
    private const int KeyK = 0x4B;
    private const int KeyL = 0x4C;

    private readonly RecordingAnnouncer keys = new();
    private readonly HitLines hits;
    private readonly TargetedEnemy target;
    private readonly BattleEnemyHp enemyHp;
    private bool targetOn = true;

    public TargetedEnemyTests()
    {
        hits = new HitLines(new RecordingAnnouncer(), new ManualTime(), _ => null) { ListOn = () => true };
        target = new TargetedEnemy(hits);
        enemyHp = new BattleEnemyHp(() => BattleBindings.Default, hits, new Scene(), keys)
        {
            ListOn = () => true,
            SayMaxHp = () => false,
            Target = target,
            TargetOn = () => targetOn,
        };
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);
    }

    private string LastKey() => keys.Said[^1].Text;

    private void Press(int key)
    {
        enemyHp.Keyboard.Keys([key]);
        enemyHp.Keyboard.Keys([]);
    }

    [Fact]
    public void TheNextKeyStartsFromTheTargetedEnemyThenStepsOn()
    {
        target.Focused(Cursor, 1);

        Press(KeyK);
        Assert.Equal("Second enemy, not hit yet, 2 of 3", LastKey());
        Press(KeyK);
        Assert.Equal("Third enemy, not hit yet, 3 of 3", LastKey());
    }

    [Fact]
    public void ThePreviousKeyStartsFromTheTargetedEnemyToo()
    {
        target.Focused(Cursor, 2);

        Press(KeyJ);

        Assert.Equal("Third enemy, not hit yet, 3 of 3", LastKey());
    }

    [Fact]
    public void ACursorMoveAfterAStepStartsAgainFromTheNewTarget()
    {
        target.Focused(Cursor, 0);
        Press(KeyK);
        Press(KeyK);

        target.Focused(Cursor, 2);
        Press(KeyK);

        Assert.Equal("Third enemy, not hit yet, 3 of 3", LastKey());
    }

    [Fact]
    public void TheSameSlotEachFrameDoesNotStopTheSteps()
    {
        target.Focused(Cursor, 0);
        Press(KeyK);
        target.Focused(Cursor, 0);

        Press(KeyK);

        Assert.Equal("Second enemy, not hit yet, 2 of 3", LastKey());
    }

    [Fact]
    public void TheAllEnemiesKeyStartsFromTheTargetedEnemy()
    {
        target.Focused(Cursor, 1);

        Press(KeyL);

        Assert.Equal("Second enemy, not hit yet. Third enemy, not hit yet. First enemy, not hit yet", LastKey());
    }

    [Fact]
    public void WithNoTargetTheKeysStartFromTheFirstEnemy()
    {
        Press(KeyK);

        Assert.Equal("First enemy, not hit yet, 1 of 3", LastKey());
    }

    [Fact]
    public void ASlotPastTheListFallsBackToTheFirstEnemy()
    {
        target.Focused(Cursor, 7);

        Press(KeyK);

        Assert.Equal("First enemy, not hit yet, 1 of 3", LastKey());
    }

    [Fact]
    public void ATargetFromTheBattleBeforeCountsAsNone()
    {
        target.Focused(Cursor, 2);
        hits.Enemies(0x6000, [0xD0, 0xE0, 0xF0]);

        Press(KeyK);

        Assert.Equal("First enemy, not hit yet, 1 of 3", LastKey());
    }

    [Fact]
    public void OffTheSettingTheKeysIgnoreTheTarget()
    {
        targetOn = false;
        target.Focused(Cursor, 2);

        Press(KeyK);

        Assert.Equal("First enemy, not hit yet, 1 of 3", LastKey());
    }

    private sealed class Scene : IBattleScene
    {
        public bool InBattle() => true;
    }
}
