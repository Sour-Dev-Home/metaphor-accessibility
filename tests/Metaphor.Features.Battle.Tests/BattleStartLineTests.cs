using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The battle-start line (#251) end to end at its seams: battle package frames in, as the package update hook
/// hands them over, with a faked battle-state reader, and what the recording announcer was asked to say out.</summary>
[Trait("Category", "Unit")]
public sealed class BattleStartLineTests
{
    private readonly FakeBattleState state = new();
    private readonly RecordingAnnouncer announcer = new();
    private readonly BattleEvents events;

    public BattleStartLineTests()
    {
        events = new BattleEvents(state);
        events.Observe(new BattleStartLine(announcer));
    }

    private void Frame(long package, BattlePhase phase, int? enemies = null)
    {
        state.Phases[package] = phase;
        if (enemies is { } count)
        {
            state.Enemies[package] = count;
        }
        events.Frame(package);
    }

    [Theory]
    [InlineData(3, "Battle: 3 enemies")]
    [InlineData(2, "Battle: 2 enemies")]
    [InlineData(1, "Battle: 1 enemy")]
    public void EnteringAFightSaysHowManyEnemies(int enemies, string line)
    {
        Frame(0x1000, BattlePhase.Load, enemies);
        Frame(0x1000, BattlePhase.Beginning);
        Frame(0x1000, BattlePhase.Fight);

        Assert.Equal([new Announcement(line, Interrupt: false)], announcer.Said);
    }

    [Fact]
    public void DefeatsAndTheEndAddNothing()
    {
        Frame(0x1000, BattlePhase.Load, enemies: 3);
        Frame(0x1000, BattlePhase.Fight, enemies: 2);
        Frame(0x1000, BattlePhase.Victory, enemies: 0);
        Frame(0x1000, BattlePhase.Finalize);

        Assert.Equal(["Battle: 3 enemies"], announcer.Said.Select(said => said.Text));
    }

    [Fact]
    public void EachFightOfSessionSixIsAnnouncedOnce()
    {
        // The three fights of docs/raw-sources/battle-state-session6-2026-11-17.md, one frame per logged phase.
        Frame(0x1000, BattlePhase.Load, enemies: 3);
        Frame(0x1000, BattlePhase.Beginning);
        Frame(0x1000, BattlePhase.Fight);
        Frame(0x1000, BattlePhase.Victory);
        Frame(0x1000, BattlePhase.Finalize);
        Frame(0x2000, BattlePhase.Load, enemies: 1);
        Frame(0x2000, BattlePhase.Fight);
        Frame(0x2000, BattlePhase.Victory);
        Frame(0x3000, BattlePhase.Load, enemies: 2);
        Frame(0x3000, BattlePhase.Fight);
        Frame(0x3000, BattlePhase.AllyEscape);
        Frame(0x3000, BattlePhase.Finalize);

        Assert.Equal(
            ["Battle: 3 enemies", "Battle: 1 enemy", "Battle: 2 enemies"], announcer.Said.Select(said => said.Text));
    }

    private sealed class FakeBattleState : IBattleState
    {
        public Dictionary<long, BattlePhase> Phases { get; } = [];

        public Dictionary<long, int> Enemies { get; } = [];

        public BattlePhase Phase(long package) => Phases.GetValueOrDefault(package);

        public int? EnemyCount(long package) => Enemies.TryGetValue(package, out var count) ? count : null;

        public int? MoneyWon(long package) => null;
    }
}
