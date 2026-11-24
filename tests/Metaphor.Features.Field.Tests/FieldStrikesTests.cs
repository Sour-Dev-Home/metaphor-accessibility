using Metaphor.Core.Announcements;

namespace Metaphor.Features.Field.Tests;

/// <summary>The field strike callouts (#696): "Hit" when a field enemy's HP drops, "Defeated" when it reaches 0, in free
/// roam, at most one check per interval, each drop logged with the strike's distance. Made-up positions and HP only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FieldStrikesTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly StandInField field = new();
    private readonly List<string> log = [];
    private readonly FieldStrikes strikes;
    private long now = 1000;

    public FieldStrikesTests() => strikes = new FieldStrikes(field, announcer, () => now, log.Add);

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

    private static FieldUnit Enemy(double x, int? hp) => new(FieldKind.Enemy, (float)x, 0, 300, Hp: hp);

    private void Tick(FieldView? view, long by = FieldStrikes.CheckIntervalMilliseconds)
    {
        field.View = view;
        now += by;
        strikes.Check();
    }

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void ADropIsAHitAndADropToZeroADefeat()
    {
        Tick(Around(Enemy(0, 100)));
        Assert.Empty(announcer.Said);

        Tick(Around(Enemy(20, 60)));
        Tick(Around(Enemy(40, 60)));
        Tick(Around(Enemy(40, 0)));

        Assert.Equal([FieldStrikes.HitText, FieldStrikes.DefeatedText], Said());
        Assert.All(announcer.Said, said => Assert.False(said.Interrupt));
    }

    [Fact]
    public void EachDropIsLoggedWithTheStrikesDistance()
    {
        Tick(Around(Enemy(0, 100)));
        Tick(Around(Enemy(0, 60)));

        Assert.Equal(["strike hp=100->60 distance=300 bearing=0 heightDiff=0 stunned=False at=(0,300) player=(0,0)"], log);
    }

    [Fact]
    public void EachEnemyIsFollowedByWhereItWas()
    {
        Tick(Around(Enemy(0, 100), Enemy(1000, 50)));
        Tick(Around(Enemy(1010, 50), Enemy(10, 100)));

        Assert.Empty(announcer.Said);

        Tick(Around(Enemy(1020, 40), Enemy(20, 100)));

        Assert.Equal([FieldStrikes.HitText], Said());
    }

    [Fact]
    public void ADefeatInTheSameReadAsAHitIsSaidAlone()
    {
        Tick(Around(Enemy(0, 100), Enemy(1000, 50)));
        Tick(Around(Enemy(0, 10), Enemy(1000, 0)));

        Assert.Equal([FieldStrikes.DefeatedText], Said());
    }

    [Fact]
    public void AnEnemyFarFromWhereOneWasIsANewOne()
    {
        Tick(Around(Enemy(0, 100)));
        Tick(Around(Enemy(FieldStrikes.SameEnemyDistance + 1, 10)));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void UnreadHpAndRisingHpSayNothing()
    {
        Tick(Around(Enemy(0, null)));
        Tick(Around(Enemy(0, null)));
        Tick(Around(Enemy(0, 50)));
        Tick(Around(Enemy(0, 80)));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OutOfFreeRoamItReadsNothingAndTheDropIsSaidOnReturn()
    {
        Tick(Around(Enemy(0, 100)));
        field.Roaming = false;
        var reads = field.Reads;
        Tick(Around(Enemy(0, 60)));

        Assert.Equal(reads, field.Reads);
        Assert.Empty(announcer.Said);

        field.Roaming = true;
        Tick(Around(Enemy(0, 60)));

        Assert.Equal([FieldStrikes.HitText], Said());
    }

    [Fact]
    public void ItChecksAtMostOncePerInterval()
    {
        Tick(Around(Enemy(0, 100)));
        var reads = field.Reads;

        Tick(Around(Enemy(0, 60)), by: FieldStrikes.CheckIntervalMilliseconds - 1);

        Assert.Equal(reads, field.Reads);
    }

    [Fact]
    public void AnEnemyGoneSoonAfterAHitIsLogged()
    {
        Tick(Around(Enemy(0, 100)));
        Tick(Around(Enemy(0, 60)));
        Tick(Around(), by: FieldStrikes.LeaveMilliseconds);

        Assert.Contains("strike lost lastHp=60 hitBeforeGone=1000ms", log);
    }

    [Fact]
    public void AnEnemyUnseenLongerThanTheLeaveIsNewWhenItComesBack()
    {
        Tick(Around(Enemy(0, 100)));
        Tick(Around(), by: FieldStrikes.LeaveMilliseconds);
        Tick(Around(Enemy(0, 60)));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AReadThatThrowsSaysNothing()
    {
        var throwing = new FieldStrikes(new ThrowingField(), announcer, () => now);

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
