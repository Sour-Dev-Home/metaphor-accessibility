using Metaphor.Core.Announcements;

namespace Metaphor.Features.Field.Tests;

/// <summary>The stun callout (#772): a field enemy is said once as it becomes stunned in free roam, again only after its
/// stun has ended and a new one lands, at most one check per interval. Made-up positions only.</summary>
[Trait("Category", "Unit")]
public sealed class StunCalloutsTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly StandInField field = new();
    private readonly StunCallouts stuns;
    private long now = 1000;

    public StunCalloutsTests() => stuns = new StunCallouts(field, announcer, () => now);

    private sealed class StandInField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldView? View { get; set; }

        public int Reads { get; private set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read()
        {
            Reads++;
            return View;
        }

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }

    private static FieldView Around(params FieldUnit[] units) => new(0, 0, 0, 0, units);

    private static FieldUnit Enemy(double x, bool stunned) => new(FieldKind.Enemy, (float)x, 0, 300, Stunned: stunned);

    private void Tick(FieldView? view)
    {
        field.View = view;
        now += StunCallouts.CheckIntervalMilliseconds;
        stuns.Check();
    }

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void AStunLandingIsSaidOnceWhileItLasts()
    {
        Tick(Around(Enemy(0, stunned: false)));
        Assert.Empty(announcer.Said);

        Tick(Around(Enemy(0, stunned: true)));
        Tick(Around(Enemy(0, stunned: true)));
        Tick(Around(Enemy(5, stunned: true)));

        Assert.Equal(["Enemy stunned"], Said());
        Assert.False(announcer.Said[0].Interrupt);
    }

    [Fact]
    public void ANewStunAfterTheLastOneEndedIsSaidAgain()
    {
        Tick(Around(Enemy(0, stunned: true)));
        for (var i = 0; i < 15; i++)
        {
            Tick(Around(Enemy(0, stunned: false)));
        }
        Tick(Around(Enemy(0, stunned: true)));

        Assert.Equal(["Enemy stunned", "Enemy stunned"], Said());
    }

    [Fact]
    public void TwoEnemiesStunnedAtOnceAreSaidTogether()
    {
        Tick(Around(Enemy(0, stunned: true), Enemy(500, stunned: true), Enemy(900, stunned: false)));

        Assert.Equal(["Enemy stunned. Enemy stunned"], Said());
    }

    [Fact]
    public void OutsideFreeRoamNothingIsReadAndAStunStillHeldIsSaidAfter()
    {
        field.Roaming = false;
        Tick(Around(Enemy(0, stunned: true)));
        Assert.Equal(0, field.Reads);
        Assert.Empty(announcer.Said);

        field.Roaming = true;
        Tick(Around(Enemy(0, stunned: true)));
        Assert.Equal(["Enemy stunned"], Said());
    }

    [Fact]
    public void ChecksCloserThanTheIntervalReadNothing()
    {
        Tick(Around(Enemy(0, stunned: false)));
        now += 10;
        stuns.Check();

        Assert.Equal(1, field.Reads);
    }

    [Fact]
    public void AReadThatThrowsSaysNothing()
    {
        var throwing = new StunCallouts(new ThrowingField(), announcer, () => now);
        throwing.Check();

        Assert.Empty(announcer.Said);
    }

    private sealed class ThrowingField : IFieldReads
    {
        public bool InFreeRoam() => true;

        public FieldView? Read() => throw new InvalidOperationException("unreadable");

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }
}
