using Metaphor.Core.Input;
using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Input;

/// <summary>LB and RB hold-or-tap (#811, the maintainer's design of 2026-11-23): a shoulder the mod holds for a chord is
/// kept from the game while held; another press while it's held is the mod's chord and the game never sees the shoulder;
/// let go with no chord, it reaches the game as a tap of a few reads.</summary>
[Trait("Category", "Unit")]
public sealed class ShoulderTapsTests
{
    private const int LB = ShoulderTaps.LeftShoulder;
    private const int RB = ShoulderTaps.RightShoulder;
    private const int A = 0x4000;
    private const int Down = 0x0040;

    private int held = LB;
    private long now = 1000;
    private readonly ShoulderTaps taps;

    public ShoulderTapsTests()
    {
        taps = new ShoulderTaps([() => held], () => now);
    }

    // One made read of pad 0, every button handed on by the controls; a frame is 16 ms.
    private int Read(int buttons, int? handed = null)
    {
        var answer = taps.Pad(0, buttons, handed ?? buttons, made: true);
        now += 16;
        return answer;
    }

    [Fact]
    public void AHeldShoulderIsKeptFromTheGameWhileHeld()
    {
        Assert.Equal(0, Read(LB));
        Assert.Equal(0, Read(LB));
        Assert.Equal(A, Read(LB | A));
    }

    [Fact]
    public void ALoneTapReachesTheGameOnReleaseForAtLeastThreeReadsAndFiftyMilliseconds()
    {
        Read(LB);
        Read(LB);

        // Released: the tap goes down on the release read and stays for 50 ms (four reads at 16 ms), then lets go.
        Assert.Equal([LB, LB, LB, LB, 0, 0], [Read(0), Read(0), Read(0), Read(0), Read(0), Read(0)]);
    }

    [Fact]
    public void AtAHighFrameRateTheTapLastsFiftyMilliseconds()
    {
        Read(LB);
        var handed = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            handed.Add(taps.Pad(0, 0, 0, made: true));
            now += 7;
        }

        Assert.Equal(8, handed.Count(buttons => buttons == LB));
        Assert.Equal(0, handed[^1]);
    }

    [Fact]
    public void AtALowFrameRateTheTapLastsThreeReads()
    {
        Read(LB);
        var handed = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            handed.Add(taps.Pad(0, 0, 0, made: true));
            now += 40;
        }

        Assert.Equal([LB, LB, LB, 0, 0], handed);
    }

    [Fact]
    public void AHoldWithNoChordIsStillATapOnRelease()
    {
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(0, Read(LB));
        }

        Assert.Equal(LB, Read(0));
    }

    [Fact]
    public void AnotherPressWhileHeldIsAChordAndTheGameNeverSeesTheShoulder()
    {
        Assert.Equal(0, Read(LB));
        // The chord's control hides the D-pad button; the shoulder is kept back by the taps.
        Assert.Equal(0, Read(LB | Down, handed: LB));
        Assert.Equal(0, Read(LB, handed: LB));

        Assert.Equal([0, 0, 0, 0, 0], [Read(0), Read(0), Read(0), Read(0), Read(0)]);
    }

    [Fact]
    public void BothShouldersHeldBackTogetherAreAChordOpeningNothing()
    {
        held = LB | RB;

        Assert.Equal(0, Read(LB));
        Assert.Equal(0, Read(LB | RB));
        Assert.Equal(0, Read(RB));

        Assert.Equal([0, 0, 0, 0], [Read(0), Read(0), Read(0), Read(0)]);
    }

    [Fact]
    public void BothShouldersPressedOnTheSameReadAreAChord()
    {
        held = LB | RB;

        Assert.Equal(0, Read(LB | RB));

        Assert.Equal([0, 0, 0, 0], [Read(0), Read(0), Read(0), Read(0)]);
    }

    [Fact]
    public void ALoneRightShoulderTapReachesTheGameAsRb()
    {
        held = LB | RB;

        Assert.Equal(0, Read(RB));

        Assert.Equal(RB, Read(0));
    }

    [Fact]
    public void AShoulderNotHeldBackReachesTheGameAtOnce()
    {
        held = 0;

        Assert.Equal(LB, Read(LB));
        Assert.Equal(0, Read(0));
        Assert.Equal(0, Read(0));
    }

    // Asked at the press: leaving the context mid-hold doesn't hand the shoulder over; and a tap is dropped when the
    // context has gone by the release, so it never opens something on a screen it wasn't pressed for.
    [Fact]
    public void ThePressDecidesAndALeftContextDropsTheTap()
    {
        Assert.Equal(0, Read(LB));
        held = 0;
        Assert.Equal(0, Read(LB));

        Assert.Equal(0, Read(0));
    }

    [Fact]
    public void OnlyTheShouldersAreEverHeldBack()
    {
        held = A | LB;

        Assert.Equal(A, Read(A));
        Assert.Equal(A, Read(A));
    }

    [Fact]
    public void AButtonAlreadyHeldWhenTheShoulderGoesDownIsNoChord()
    {
        Read(Down);

        Assert.Equal(Down, Read(Down | LB));
        Assert.Equal(Down | LB, Read(Down));
    }

    [Fact]
    public void ANewPressEndsATapAndIsHeldBackInTurn()
    {
        Read(LB);
        Assert.Equal(LB, Read(0));

        Assert.Equal(0, Read(LB));
        Assert.Equal(LB, Read(0));
    }

    // #225: a read the game reports as not made changes nothing and moves nothing on.
    [Fact]
    public void AReadNotMadeIsHandedBackAsItIsAndCountsForNothing()
    {
        Assert.Equal(0x410e, taps.Pad(1, 0x410e, 0x410e, made: false));

        Read(LB);
        Assert.Equal(LB, Read(0));
        Assert.Equal(0, taps.Pad(0, 0, 0, made: false));
        Assert.Equal([LB, LB, LB, 0], [Read(0), Read(0), Read(0), Read(0)]);
    }

    // Steam Input's second pad reads apart from the first: each pad holds its own shoulder.
    [Fact]
    public void EachPadHoldsItsOwn()
    {
        Assert.Equal(0, taps.Pad(0, LB, LB, made: true));
        Assert.Equal(0, taps.Pad(1, A, A, made: true) & LB);
        Assert.Equal(0, taps.Pad(0, LB, LB, made: true));

        Assert.Equal(LB, taps.Pad(0, 0, 0, made: true));
    }

    [Fact]
    public void WithNoHoldsTheTapsChangeNothing()
    {
        var none = new ShoulderTaps([], () => now);

        Assert.Equal(LB | A, none.Pad(0, LB | A, LB | A, made: true));
        Assert.Equal(0, none.Pad(0, 0, 0, made: true));
    }

    // The frame applies the taps after every control, so the chord's control still sees the shoulder held.
    [Fact]
    public void TheFrameHandsItsControlsTheShoulderAndTheGameTheTap()
    {
        var seen = new List<int>();
        var controls = new SeeingControls(seen, buttons => buttons & ~Down);
        var frame = new InputFrame(new NoGame(), new MenuScreens([]), [controls], null, null, null, taps: taps);

        Assert.Equal(0, frame.Pad(0, LB, made: true).Buttons);
        Assert.Equal(0, frame.Pad(0, LB | Down, made: true).Buttons);
        Assert.Equal(0, frame.Pad(0, 0, made: true).Buttons);
        Assert.Equal([LB, LB | Down, 0], seen);

        Assert.Equal(0, frame.Pad(0, LB, made: true).Buttons);
        Assert.Equal(LB, frame.Pad(0, 0, made: true).Buttons);
    }

    private sealed class NoGame : IGameInputState
    {
        public long? DialogueWindow() => null;

        public bool MenusOpen() => false;
    }

    private sealed class SeeingControls(List<int> seen, Func<int, int> pad) : IInputControls
    {
        public int Pad(int padNumber, int buttons, bool made)
        {
            seen.Add(buttons);
            return pad(buttons);
        }

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => keys;
    }
}
