using Metaphor.Core.Announcements;
using Metaphor.Core.Input;

namespace Metaphor.Features.Field.Tests;

/// <summary>Auto-walk (#184): it pushes only the game's left stick toward the guidance beep's aim, never a button and
/// never the position, and stops on any input of the player's, on leaving free roam, on a new map and when the beep
/// ends.</summary>
[Trait("Category", "Unit")]
public sealed class AutoWalkTests
{
    private const int LB = 0x0400;
    private const int Y = 0x1000;
    private const int A = 0x4000;
    private const int KeyH = 0x48;
    private const int KeyW = 0x57;

    private readonly WalkField field = new();
    private readonly RecordingAnnouncer announcer = new();
    private double? aim = 0;
    private bool enabled = true;
    private readonly AutoWalk walk;

    public AutoWalkTests()
    {
        walk = new AutoWalk(field, () => aim, announcer, () => enabled, () => new WalkBindings(KeyH, LB, Y));
    }

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    private PadSticks Read() => walk.Sticks(0, 0, PadSticks.Rest);

    private void Start()
    {
        walk.Keys([KeyH]);
        walk.Keys([]);
    }

    [Theory]
    [InlineData(0, 128, 1)]
    [InlineData(Math.PI / 2, 1, 128)]
    [InlineData(-Math.PI / 2, 255, 128)]
    [InlineData(Math.PI, 128, 255)]
    public void ItPushesTheLeftStickTowardTheBeepsAim(double bearing, int x, int y)
    {
        aim = bearing;
        Start();

        Assert.Equal(PadSticks.Rest with { LeftX = x, LeftY = y }, Read());
        Assert.Equal(["Auto-walk on"], Said());
        Assert.True(walk.Walking);
    }

    [Fact]
    public void WithoutGuidanceItSaysSoAndDoesNothing()
    {
        aim = null;
        Start();

        Assert.Equal([AutoWalk.NoTargetText], Said());
        Assert.Equal(PadSticks.Rest, Read());
    }

    [Fact]
    public void ThePadChordStartsItAndItsButtonIsHidden()
    {
        Assert.Equal(LB, walk.Pad(0, LB | Y, made: true));

        Assert.True(walk.Walking);
        Assert.Equal(LB, walk.Pad(0, LB | Y, made: true));
        Assert.True(walk.Walking);
    }

    [Fact]
    public void ItNeverPressesAButton()
    {
        Start();

        Assert.Equal(0, walk.Pad(0, 0, made: true));
        Assert.Equal(PadSticks.Rest.RightX, Read().RightX);
    }

    [Fact]
    public void AButtonThePlayerPressesStopsItAndReachesTheGame()
    {
        Start();

        Assert.Equal(A, walk.Pad(0, A, made: true));

        Assert.False(walk.Walking);
        Assert.Equal(["Auto-walk on", "Auto-walk off"], Said());
        Assert.Equal(PadSticks.Rest, Read());
    }

    [Fact]
    public void AKeyThePlayerPressesStopsIt()
    {
        Start();

        Assert.Equal([KeyW], walk.Keys([KeyW]));

        Assert.False(walk.Walking);
    }

    [Fact]
    public void AStickThePlayerPushesStopsItAndReachesTheGame()
    {
        Start();
        var pushed = PadSticks.Rest with { RightX = 200 };

        Assert.Equal(pushed, walk.Sticks(0, 0, pushed));

        Assert.False(walk.Walking);
        Assert.Equal("Auto-walk off", Said()[^1]);
    }

    [Fact]
    public void TheWalkKeyAgainStopsIt()
    {
        Start();
        Start();

        Assert.False(walk.Walking);
        Assert.Equal(["Auto-walk on", "Auto-walk off"], Said());
    }

    [Fact]
    public void LeavingFreeRoamStopsItSilently()
    {
        Start();
        field.Roaming = false;

        Assert.Equal(PadSticks.Rest, Read());
        field.Roaming = true;
        Assert.Equal(PadSticks.Rest, Read());
        Assert.Equal(["Auto-walk on"], Said());
    }

    [Fact]
    public void ANewMapStopsIt()
    {
        Start();
        field.Map = 2;

        Assert.Equal(PadSticks.Rest, Read());
        Assert.False(walk.Walking);
    }

    [Fact]
    public void TheBeepEndingStopsIt()
    {
        Start();
        aim = null;

        Assert.Equal(PadSticks.Rest, Read());
        aim = 0;
        Assert.Equal(PadSticks.Rest, Read());
    }

    [Fact]
    public void TurnedOffItTakesNoKey()
    {
        enabled = false;

        Assert.Equal([KeyH], walk.Keys([KeyH]));
        Assert.False(walk.Walking);
    }

    private sealed class WalkField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public int? Map { get; set; } = 1;

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => null;

        public int? MapId() => Map;

        public FieldPose? ReadPose() => null;
    }
}
