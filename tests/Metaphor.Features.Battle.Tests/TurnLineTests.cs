using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The turn line (#252): a side's turn starting is said once, from the turn icons read on input reads, as the
/// 2026-11-22 Debug log's icon values ran. Made-up sequences shaped like that log.</summary>
[Trait("Category", "Unit")]
public sealed class TurnLineTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly Icons icons = new();
    private readonly Scene scene = new() { On = true };
    private readonly TurnLine line;

    public TurnLineTests() => line = new TurnLine(icons, scene, announcer, time);

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    // Input reads, one each 50 ms frame, over the given time.
    private void Frames(long milliseconds)
    {
        for (var at = 0L; at < milliseconds; at += 50)
        {
            time.Advance(50);
            line.Keys([]);
            line.Pad(0, 0, made: true);
        }
    }

    [Fact]
    public void APlayerTurnThenAnEnemyTurnAreEachSaidOnceCuttingOffTheQueue()
    {
        icons.Now = new TurnIcons(0, 4, 0);
        Frames(300);
        icons.Now = new TurnIcons(1, 3, 0);
        Frames(300);
        icons.Now = new TurnIcons(1, 2, 0);
        Frames(300);

        Assert.Equal(["Your turn, 4 actions", "Enemy turn"], Said());
        // #252, by ear 2026-11-23: queued, "Your turn" came behind 17 hit lines.
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void ADebugLogGetsEachReadThatDiffersFromTheOneBefore()
    {
        // #254: by ear on 2026-11-23 a mid-turn gain was never said; the log shows what the icons read.
        var logged = new List<string>();
        var logging = new TurnLine(icons, scene, announcer, time) { Log = logged.Add };
        icons.Now = new TurnIcons(0, 3, 2);
        for (var frame = 0; frame < 6; frame++)
        {
            time.Advance(50);
            logging.Keys([]);
        }
        icons.Now = new TurnIcons(0, 2, 3);
        time.Advance(150);
        logging.Keys([]);

        Assert.Equal(["icons side=0 full=3 half=2", "icons side=0 full=2 half=3"], logged);
    }

    [Fact]
    public void AChangeInThePlayersIconsMidTurnSaysTheNewCountOnceSettled()
    {
        icons.Now = new TurnIcons(0, 3, 2);
        Frames(300);
        // One full icon spent turns half, and a skill gains two (#695, by ear 2026-11-23): read in two steps.
        icons.Now = new TurnIcons(0, 2, 3);
        Frames(100);
        icons.Now = new TurnIcons(0, 2, 5);
        Frames(TurnLine.SettleMilliseconds + 100);

        Assert.Equal(["Your turn, 3 actions, 2 half", "2 actions, 5 half"], Said());
        Assert.False(announcer.Said[^1].Interrupt);
    }

    [Fact]
    public void ACountThatComesBackBeforeItSettlesSaysNothing()
    {
        icons.Now = new TurnIcons(0, 3, 1);
        Frames(300);
        icons.Now = new TurnIcons(0, 3, 0);
        Frames(100);
        icons.Now = new TurnIcons(0, 3, 1);
        Frames(TurnLine.SettleMilliseconds + 100);

        Assert.Equal(["Your turn, 3 actions, 1 half"], Said());
    }

    [Fact]
    public void TheEnemysIconsChangingSaysNothing()
    {
        icons.Now = new TurnIcons(1, 3, 0);
        Frames(300);
        icons.Now = new TurnIcons(1, 2, 1);
        Frames(TurnLine.SettleMilliseconds + 100);

        Assert.Equal(["Enemy turn"], Said());
    }

    [Theory]
    [InlineData(1, 0, "1 action")]
    [InlineData(3, 3, "3 actions, 3 half")]
    [InlineData(0, 2, "0 actions, 2 half")]
    public void TheCountSaysActionsThenHalves(int full, int half, string expected) =>
        Assert.Equal(expected, TurnLine.Count(new TurnIcons(0, full, half)));

    [Fact]
    public void ARefillOnTheSameSideIsANewTurn()
    {
        icons.Now = new TurnIcons(0, 4, 0);
        Frames(200);
        icons.Now = new TurnIcons(0, 0, 0);
        Frames(200);
        icons.Now = new TurnIcons(0, 3, 0);
        Frames(200);

        Assert.Equal(["Your turn, 4 actions", "Your turn, 3 actions"], Said());
    }

    [Fact]
    public void NothingIsReadOrSaidOutOfBattle()
    {
        scene.On = false;
        icons.Now = new TurnIcons(0, 4, 0);
        Frames(3000);

        Assert.Empty(Said());
        Assert.Equal(0, icons.Reads);
    }

    [Fact]
    public void TheIconsAreReadAtMostEveryTenthOfASecond()
    {
        icons.Now = new TurnIcons(1, 3, 0);
        Frames(1000);

        Assert.InRange(icons.Reads, 1, (int)(1000 / TurnLine.ReadEveryMilliseconds));
    }

    [Fact]
    public void AnUnreadableTurnSaysNothing()
    {
        icons.Now = null;
        Frames(500);

        Assert.Empty(Said());
    }

    [Theory]
    [InlineData(0, 4, 0, "Your turn, 4 actions")]
    [InlineData(0, 1, 0, "Your turn, 1 action")]
    [InlineData(0, 3, 1, "Your turn, 3 actions, 1 half")]
    [InlineData(1, 3, 0, "Enemy turn")]
    public void EachTurnSaysItsSideAndActions(int side, int full, int half, string expected) =>
        Assert.Equal(expected, TurnLine.Says(new TurnIcons(side, full, half)));

    private sealed class Icons : IBattleTurnIcons
    {
        public TurnIcons? Now { get; set; }

        public int Reads { get; private set; }

        public TurnIcons? Read()
        {
            Reads++;
            return Now;
        }
    }

    private sealed class Scene : IBattleScene
    {
        public bool On { get; set; }

        public bool InBattle() => On;
    }
}
