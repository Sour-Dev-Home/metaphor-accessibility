using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The ambushed start (#626) and the battle's end (#166), from the layout animations the 2026-11-22 and
/// 2026-11-23 Debug logs recorded.</summary>
[Trait("Category", "Unit")]
public sealed class BattleLayoutLinesTests
{
    private readonly RecordingAnnouncer announcer = new();

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void AnEnemyFirstStartIsSaidAndAPlainOneIsNot()
    {
        var start = new BattleStartLine(announcer);

        start.Played(new LayoutAnimation("BattleCutInWipeLayout@ui@app", 906));
        start.Played(new LayoutAnimation("BattleCutInWipeLayout@ui@app", 910));

        Assert.Equal(["Ambushed"], Said());
        Assert.False(announcer.Said[0].Interrupt);
    }

    [Theory]
    [InlineData(2572, "Victory")]
    [InlineData(2570, "Escaped")]
    [InlineData(2571, "Defeated")]
    public void EachEndingIsSaid(int animation, string expected)
    {
        new BattleEndLine(announcer).Played(new LayoutAnimation("TurnInfoLayout@ui@app", animation));

        Assert.Equal([expected], Said());
    }

    [Fact]
    public void OtherAnimationsAndLayoutsSayNothing()
    {
        var end = new BattleEndLine(announcer);
        var start = new BattleStartLine(announcer);

        end.Played(new LayoutAnimation("TurnInfoLayout@ui@app", 2597));
        end.Played(new LayoutAnimation("BattleNoticeLayout@ui@app", 2572));
        start.Played(new LayoutAnimation("BattleNoticeLayout@ui@app", 910));

        Assert.Empty(Said());
    }

    [Fact]
    public void EachAsksOnlyForItsLayout()
    {
        Assert.Equal(["BattleCutInWipeLayout@ui@app"], new BattleStartLine(announcer).Layouts);
        Assert.Equal(["TurnInfoLayout@ui@app"], new BattleEndLine(announcer).Layouts);
    }
}
