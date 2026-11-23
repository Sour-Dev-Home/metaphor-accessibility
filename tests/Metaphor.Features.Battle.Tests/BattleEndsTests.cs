using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Enemy numbering starts afresh each battle (#750): by ear on 2026-11-22 the second battle went on from the
/// first's count ("Seventh enemy" onwards). Leaving the battle scene ends the battle; input reads drive the check.
/// Made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleEndsTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Scene scene = new() { On = true };
    private readonly HitLines lines;
    private readonly BattleEnds ends;

    public BattleEndsTests()
    {
        lines = new HitLines(announcer, time, _ => null);
        ends = new BattleEnds(lines, scene, time);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    private static BattleHit Enemy(long target, int change, int left) =>
        new(target, change, left, 1, BattleSide.Enemy);

    // Input reads, one each frame, over the given time.
    private void Frames(long milliseconds)
    {
        for (var at = 0L; at < milliseconds; at += 50)
        {
            time.Advance(50);
            ends.Keys([]);
            ends.Pad(0, 0, made: true);
        }
    }

    [Fact]
    public void ANewBattleNumbersItsEnemiesFromTheFirst()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 50));
        Frames(3_000);
        scene.On = false;
        Frames(5_000);
        scene.On = true;
        Frames(1_000);

        lines.Observe(Enemy(0xc0, -20, 40));
        lines.Observe(Enemy(0xd0, -20, 40));
        lines.Observe(Enemy(0xc0, -5, 35));

        Assert.Equal(
            ["Enemy, 10", "Second enemy, 10", "Enemy, 20", "Second enemy, 20", "First enemy, 5"], Said());
    }

    // One read that says no battle, as a load or a failed read might, is no battle's end.
    [Fact]
    public void AMomentOutOfTheBattleSceneKeepsTheNumbers()
    {
        lines.Observe(Enemy(0xa0, -10, 50));
        lines.Observe(Enemy(0xb0, -10, 50));
        scene.On = false;
        Frames(BattleEnds.AskEveryMilliseconds);
        scene.On = true;
        Frames(3_000);

        lines.Observe(Enemy(0xa0, -5, 45));

        Assert.Equal("First enemy, 5", Said().Last());
    }

    // The scene is asked only while the hit lines hold a battle's enemies, and at most once a second.
    [Fact]
    public void TheSceneIsAskedRarelyAndOnlyAfterAHit()
    {
        Frames(5_000);
        Assert.Equal(0, scene.Asked);

        lines.Observe(Enemy(0xa0, -10, 50));
        Frames(5_000);

        Assert.InRange(scene.Asked, 4, 6);
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public int Asked { get; private set; }

        public bool InBattle()
        {
            Asked++;
            return On;
        }
    }
}
