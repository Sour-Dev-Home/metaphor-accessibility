using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The details key in a battle (#631, the maintainer's call of 2026-11-22): pressed alone it says the HP each
/// enemy this battle has hit has left, and is hidden from the game; anything else, and the key outside a battle,
/// reaches it. The targeted enemy isn't read yet, so it says every one. Made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleEnemyHpTests : IDisposable
{
    private const int Number = 1;
    private const int R3 = 0x0004;
    private const int KeyF = 0x46;
    private const int A = 0x4000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Scene scene = new() { On = true };
    private readonly List<string> logged = [];
    private readonly HitLines hits;
    private readonly BattleEnemyHp details;

    public BattleEnemyHpTests()
    {
        hits = new HitLines(new SilentAnnouncer(), time, _ => null);
        details = new BattleEnemyHp(R3, KeyF, hits, scene, announcer, logged.Add);
    }

    public void Dispose() => hits.Dispose();

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void InABattleFAloneSaysEachEnemysHpOfItsMaxAndIsHidden()
    {
        hits.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));
        hits.Observe(new BattleHit(0xb0, -10, 50, Number, BattleSide.Enemy));

        Assert.Empty(details.Keys([KeyF]));

        Assert.Equal(
            new Announcement("First enemy, 840 of 1200. Second enemy, 50 left", Interrupt: true), announcer.Said[^1]);
    }

    [Fact]
    public void ThePadsDetailsButtonSaysItToo()
    {
        hits.Observe(new BattleHit(0xa0, -5, 55, Number, BattleSide.Enemy));

        Assert.Equal(0, details.Pad(0, R3, made: true));

        Assert.Equal(["Enemy, 55 left"], Said());
    }

    [Fact]
    public void PartyMembersAreLeftToTheStatusKey()
    {
        hits.Observe(new BattleHit(0x1000, -5, 55, Number, BattleSide.Ally, 0, Unit: 3));

        details.Keys([KeyF]);

        Assert.Equal(["No enemy hit yet"], Said());
    }

    [Fact]
    public void OutsideABattleTheKeyReachesTheGameAndSaysNothing()
    {
        scene.On = false;

        Assert.Equal([KeyF], details.Keys([KeyF]));
        Assert.Equal(R3, details.Pad(0, R3, made: true));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AChordWithTheKeyReachesTheGameAndSaysNothing()
    {
        Assert.Equal(R3 | A, details.Pad(0, R3 | A, made: true));
        Assert.Equal([KeyF, 0x41], details.Keys([KeyF, 0x41]));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AHeldKeyOrASecondPadMirroringItSpeaksOnce()
    {
        details.Pad(0, R3, made: true);
        details.Pad(0, R3, made: true);
        details.Pad(1, R3, made: true);

        Assert.Single(announcer.Said);
    }

    // For the next session's target read: each press logs every known enemy's record, to match against the target.
    [Fact]
    public void EachPressLogsTheKnownEnemiesRecords()
    {
        hits.Observe(new BattleHit(0xa0, -360, 840, Number, BattleSide.Enemy, HpMax: 1200));

        details.Keys([KeyF]);

        Assert.Equal(["enemies=1 Enemy record=0xa0 hp=840 max=1200"], logged);
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
