using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The targeted enemy said as the target cursor moves (#253 follow-up): "Second enemy, 840 of 1320, 2 of 3",
/// cutting off the line before, once per move however often the game reports the slot, from the slot the cursor's
/// vtable swap already reports. Made-up records and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class TargetLineTests
{
    private const long Package = 0x5000;
    private const long Cursor = 0x9000;
    private const int Number = 1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly ManualTime time = new();
    private readonly HitLines hits;
    private readonly TargetLine line;
    private bool on = true;
    private bool positions = true;

    public TargetLineTests()
    {
        hits = new HitLines(new SilentAnnouncer(), time, _ => null) { ListOn = () => true };
        line = new TargetLine(hits, announcer, time)
        {
            On = () => on,
            SayPositions = () => positions,
        };
        hits.Enemies(Package, [0xA0, 0xB0, 0xC0]);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void AMoveSaysTheEnemyItsHpAndItsPlaceCuttingOffTheLineBefore()
    {
        hits.Observe(new BattleHit(0xB0, -480, 840, Number, BattleSide.Enemy, HpMax: 1320));

        line.Focused(Cursor, 1);

        var said = Assert.Single(announcer.Said);
        Assert.Equal("Second enemy, 840 of 1320, 2 of 3", said.Text);
        Assert.True(said.Interrupt);
    }

    [Fact]
    public void WithoutPositionsTheLineEndsAtTheHp()
    {
        positions = false;
        hits.Observe(new BattleHit(0xB0, -480, 840, Number, BattleSide.Enemy, HpMax: 1320));

        line.Focused(Cursor, 1);

        Assert.Equal(["Second enemy, 840 of 1320"], Said());
    }

    [Fact]
    public void AnEnemyNotHitYetSaysSo()
    {
        line.Focused(Cursor, 2);

        Assert.Equal(["Third enemy, not hit yet, 3 of 3"], Said());
    }

    [Fact]
    public void AnEnemyNotHitYetSaysTheHpItsRecordHolds()
    {
        var read = new TargetLine(hits, announcer, time) { Units = new Units(), SayPositions = () => false };

        read.Focused(Cursor, 0);

        Assert.Equal(["First enemy, 77 left"], Said());
    }

    [Fact]
    public void TheSameSlotReportedEachFrameIsSaidOnce()
    {
        line.Focused(Cursor, 0);
        time.Advance(16);
        line.Focused(Cursor, 0);
        time.Advance(16);
        line.Focused(Cursor, 0);

        Assert.Equal(["First enemy, not hit yet, 1 of 3"], Said());
    }

    [Fact]
    public void EachMoveIsSaidAndMovingBackSaysTheFirstAgain()
    {
        line.Focused(Cursor, 0);
        line.Focused(Cursor, 1);
        line.Focused(Cursor, 0);

        Assert.Equal(
            ["First enemy, not hit yet, 1 of 3", "Second enemy, not hit yet, 2 of 3", "First enemy, not hit yet, 1 of 3"],
            Said());
    }

    [Fact]
    public void TheSameSlotAfterAQuietGapIsACursorOpenedAgainAndIsSaid()
    {
        line.Focused(Cursor, 0);
        time.Advance(TargetLine.QuietMilliseconds);

        line.Focused(Cursor, 0);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void AnotherCursorOnTheSameSlotIsSaid()
    {
        line.Focused(Cursor, 0);

        line.Focused(Cursor + 0x100, 0);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void TheSameSlotInTheNextBattleIsSaid()
    {
        line.Focused(Cursor, 0);
        hits.BattleOver();
        hits.Enemies(0x6000, [0xD0, 0xE0]);

        line.Focused(Cursor, 0);

        Assert.Equal(["First enemy, not hit yet, 1 of 3", "First enemy, not hit yet, 1 of 2"], Said());
    }

    [Fact]
    public void ASlotPastTheListSaysNothing()
    {
        line.Focused(Cursor, 3);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OffTheSettingAMoveSaysNothingAndIsNotRemembered()
    {
        on = false;
        line.Focused(Cursor, 1);
        on = true;

        line.Focused(Cursor, 1);

        Assert.Equal(["Second enemy, not hit yet, 2 of 3"], Said());
    }

    [Fact]
    public void TheTargetedEnemyPassesEachReportOnToTheLine()
    {
        var targeted = new TargetedEnemy(hits) { Then = line };

        targeted.Focused(Cursor, 1);

        Assert.Equal(["Second enemy, not hit yet, 2 of 3"], Said());
        Assert.Equal((1, 1L), targeted.In(3));
    }

    // Built for the target line: new behaviour that waits for its by-ear check ships off.
    [Fact]
    public void TheTargetLineIsStillTesting() =>
        Assert.True(SettingKey.Of(BattleFeature.BattleTargetSpeech).Testing);

    private sealed class Units : IBattleUnits
    {
        public int? Hp(long record) => record == 0xA0 ? 77 : null;

        public int? Mp(long record) => null;
    }
}
