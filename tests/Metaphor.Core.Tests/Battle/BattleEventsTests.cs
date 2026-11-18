using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests.Battle;

/// <summary>The battle events (#251) at their seams: frames of the battle package in, as the package update hook hands
/// them over, a faked battle-state reader beside them, and the events observers hear out.</summary>
[Trait("Category", "Unit")]
public sealed class BattleEventsTests
{
    private const long Package = 0x5a10000;

    private readonly FakeBattleState state = new();
    private readonly List<string> heard = [];
    private readonly BattleEvents events;

    public BattleEventsTests()
    {
        events = new BattleEvents(state);
        events.Observe(new Recording(heard));
    }

    private void Frame(BattlePhase phase, int? enemies = null, long package = Package)
    {
        state.Phases[package] = phase;
        if (enemies is { } count)
        {
            state.Enemies[package] = count;
        }
        events.Frame(package);
    }

    [Fact]
    public void ANewPackageWithEnemiesStartsABattleOnce()
    {
        Frame(BattlePhase.Load, enemies: 3);
        Frame(BattlePhase.Load);
        Frame(BattlePhase.Beginning);
        Frame(BattlePhase.Fight);

        Assert.Equal(["started 3"], heard);
    }

    [Fact]
    public void TheStartWaitsUntilTheEnemyCountIsRead()
    {
        Frame(BattlePhase.Launcher);
        state.Enemies[Package] = 0;
        Frame(BattlePhase.Load);
        Frame(BattlePhase.Load, enemies: 2);

        Assert.Equal(["started 2"], heard);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void AnEnemyCountNoBattleCouldHaveIsNotSpokenAsOne(int misread)
    {
        Frame(BattlePhase.Load, enemies: misread);

        Assert.Empty(heard);
    }

    [Fact]
    public void AFrameWithoutAPackageIsIgnored()
    {
        state.Enemies[0] = 3;
        events.Frame(0);

        Assert.Empty(heard);
    }

    [Fact]
    public void VictoryEndsTheBattleWonWithItsMoneyAndFinalizeAddsNothing()
    {
        Frame(BattlePhase.Load, enemies: 3);
        Frame(BattlePhase.Fight);
        state.Money[Package] = 355;
        Frame(BattlePhase.Victory);
        Frame(BattlePhase.Victory);
        Frame(BattlePhase.Finalize);

        Assert.Equal(["started 3", "ended Won money=355"], heard);
    }

    [Fact]
    public void AllyEscapeEndsTheBattleEscaped()
    {
        Frame(BattlePhase.Load, enemies: 2);
        Frame(BattlePhase.Fight);
        Frame(BattlePhase.AllyEscape);
        Frame(BattlePhase.Interruption);
        Frame(BattlePhase.Finalize);

        Assert.Equal(["started 2", "ended Escaped money=-"], heard);
    }

    [Fact]
    public void AnEndThisModuleCannotNameIsStillAnEnd()
    {
        Frame(BattlePhase.Load, enemies: 1);
        Frame(BattlePhase.Fight);
        Frame(BattlePhase.Finalize);

        Assert.Equal(["started 1", "ended Unknown money=-"], heard);
    }

    [Fact]
    public void ABattleThatNeverStartedNeverEnds()
    {
        Frame(BattlePhase.Victory);

        Assert.Empty(heard);
    }

    [Fact]
    public void EachNewPackageIsANewBattle()
    {
        Frame(BattlePhase.Load, enemies: 3);
        Frame(BattlePhase.Victory);
        Frame(BattlePhase.Load, enemies: 1, package: Package + 0x1000);

        Assert.Equal(["started 3", "ended Won money=-", "started 1"], heard);
    }

    [Fact]
    public void APackageReusedAfterItsBattleEndedStartsTheNextBattle()
    {
        Frame(BattlePhase.Load, enemies: 3);
        Frame(BattlePhase.Victory);
        Frame(BattlePhase.Finalize);
        Frame(BattlePhase.Load, enemies: 2);

        Assert.Equal(["started 3", "ended Won money=-", "started 2"], heard);
    }

    [Fact]
    public void AnObserverThatThrowsNeverStopsTheOthers()
    {
        var events = new BattleEvents(state);
        var after = new List<string>();
        events.Observe(new Throwing());
        events.Observe(new Recording(after));
        state.Phases[Package] = BattlePhase.Load;
        state.Enemies[Package] = 2;

        events.Frame(Package);

        Assert.Equal(["started 2"], after);
    }

    /// <summary>The three fights of capture session 6 as the session's note tells them
    /// (docs/raw-sources/battle-state-session6-2026-11-17.md): the phases each logged in order and the enemy count at
    /// package <c>+0x140</c>, with fight 1's money. One frame per phase stands in for the many the game runs.</summary>
    [Fact]
    public void TheThreeFightsOfSessionSixStartAndEndAsTheyWereNarrated()
    {
        long[] packages = [0x1000, 0x2000, 0x3000];
        Frame(BattlePhase.Load, enemies: 3, package: packages[0]);
        Frame(BattlePhase.Beginning, package: packages[0]);
        Frame(BattlePhase.Fight, enemies: 2, package: packages[0]);
        Frame(BattlePhase.Fight, enemies: 0, package: packages[0]);
        state.Money[packages[0]] = 355;
        Frame(BattlePhase.Victory, package: packages[0]);
        Frame(BattlePhase.Finalize, package: packages[0]);

        Frame(BattlePhase.Load, enemies: 1, package: packages[1]);
        Frame(BattlePhase.Beginning, package: packages[1]);
        Frame(BattlePhase.Fight, package: packages[1]);
        Frame(BattlePhase.Victory, package: packages[1]);
        Frame(BattlePhase.Finalize, package: packages[1]);

        Frame(BattlePhase.Load, enemies: 2, package: packages[2]);
        Frame(BattlePhase.Beginning, package: packages[2]);
        Frame(BattlePhase.Fight, package: packages[2]);
        Frame(BattlePhase.AllyEscape, package: packages[2]);
        Frame(BattlePhase.Interruption, package: packages[2]);
        Frame(BattlePhase.Finalize, package: packages[2]);

        Assert.Equal(
            [
                "started 3", "ended Won money=355",
                "started 1", "ended Won money=-",
                "started 2", "ended Escaped money=-",
            ],
            heard);
    }

    [Theory]
    [InlineData("Load@phase@btl", BattlePhase.Load)]
    [InlineData("Victory@phase@btl", BattlePhase.Victory)]
    [InlineData("AllyEscape@phase@btl", BattlePhase.AllyEscape)]
    [InlineData("Finalize@phase@btl", BattlePhase.Finalize)]
    [InlineData("Package@btl", BattlePhase.Unknown)]
    [InlineData("Phase@btl", BattlePhase.Unknown)]
    [InlineData(null, BattlePhase.Unknown)]
    public void APhaseIsNamedByItsClass(string? className, BattlePhase expected) =>
        Assert.Equal(expected, BattlePhases.FromClassName(className));

    private sealed class FakeBattleState : IBattleState
    {
        public Dictionary<long, BattlePhase> Phases { get; } = [];

        public Dictionary<long, int> Enemies { get; } = [];

        public Dictionary<long, int> Money { get; } = [];

        public BattlePhase Phase(long package) => Phases.GetValueOrDefault(package);

        public int? EnemyCount(long package) => Enemies.TryGetValue(package, out var count) ? count : null;

        public int? MoneyWon(long package) => Money.TryGetValue(package, out var money) ? money : null;
    }

    private sealed class Recording(List<string> heard) : IBattleObserver
    {
        public void Started(BattleStarted battle) => heard.Add($"started {battle.EnemyCount}");

        public void Ended(BattleEnded battle) =>
            heard.Add($"ended {battle.Outcome} money={(battle.Money is { } money ? money.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}");
    }

    private sealed class Throwing : IBattleObserver
    {
        public void Started(BattleStarted battle) => throw new InvalidOperationException("observer bug");
    }
}
