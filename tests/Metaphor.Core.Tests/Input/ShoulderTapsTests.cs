using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

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
    private ShoulderTapLimits limits = ShoulderTapLimits.Default;
    private readonly ShoulderTaps taps;

    public ShoulderTapsTests()
    {
        taps = new ShoulderTaps([() => held], () => now, () => limits);
    }

    // One made read of pad 0, every button handed on by the controls; a frame is 16 ms.
    private int Read(int buttons, int? handed = null)
    {
        var answer = taps.Pad(0, buttons, handed ?? buttons, made: true);
        now += 16;
        return answer;
    }

    // One made read of a pad at a time in milliseconds.
    private int ReadAt(long at, int buttons, int pad = 0)
    {
        now = at;
        return taps.Pad(pad, buttons, buttons, made: true);
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

    // #835: held alone past the hold delay (300 ms by default), the shoulder is the game's from then until let go, so
    // the game's own held paging works; its release is no tap.
    [Fact]
    public void ALoneHoldPastTheDelayReachesTheGameAsAHoldUntilReleased()
    {
        Assert.Equal(0, ReadAt(1000, LB));
        Assert.Equal(0, ReadAt(1299, LB));
        Assert.Equal(LB, ReadAt(1300, LB));
        Assert.Equal(LB, ReadAt(1800, LB));

        Assert.Equal([0, 0, 0, 0], [ReadAt(1816, 0), ReadAt(1832, 0), ReadAt(1848, 0), ReadAt(1864, 0)]);
    }

    [Fact]
    public void LetGoOnTheReadTheDelayEndsIsATap()
    {
        ReadAt(1000, LB);

        Assert.Equal(LB, ReadAt(1300, 0));
    }

    [Fact]
    public void TheHoldDelayComesFromTheLimits()
    {
        limits = new(500, 50);

        Assert.Equal(0, ReadAt(1000, LB));
        Assert.Equal(0, ReadAt(1499, LB));
        Assert.Equal(LB, ReadAt(1500, LB));
    }

    [Fact]
    public void TheTapLengthComesFromTheLimits()
    {
        limits = new(300, 100);
        Read(LB);
        var handed = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            handed.Add(Read(0));
        }

        // Down from the release read until 100 ms have passed: seven reads at 16 ms.
        Assert.Equal(7, handed.Count(buttons => buttons == LB));
        Assert.Equal(0, handed[^1]);
    }

    [Fact]
    public void AChordBeforeTheDelayKeepsTheShoulderFromTheGamePastIt()
    {
        ReadAt(1000, LB);
        Assert.Equal(0, taps.Pad(0, LB | Down, LB, made: true));

        Assert.Equal(0, ReadAt(1400, LB));
        Assert.Equal(0, ReadAt(1416, 0));
    }

    // Asked again at the hand-over, as at the release: a shoulder whose context has gone stays kept back until let go.
    [Fact]
    public void AShoulderWhoseContextLeftIsNotHandedOver()
    {
        ReadAt(1000, LB);
        held = 0;

        Assert.Equal(0, ReadAt(1300, LB));
        Assert.Equal(0, ReadAt(1316, 0));
    }

    // Once handed over, the shoulders are the game's: the other one goes straight to the game too, and no tap follows.
    [Fact]
    public void TheOtherShoulderDuringAHandOverReachesTheGameAtOnce()
    {
        held = LB | RB;
        ReadAt(1000, LB);
        Assert.Equal(LB, ReadAt(1300, LB));

        Assert.Equal(LB | RB, ReadAt(1316, LB | RB));
        Assert.Equal(RB, ReadAt(1332, RB));
        Assert.Equal(0, ReadAt(1348, 0));
        Assert.Equal(0, ReadAt(1364, 0));
    }

    [Fact]
    public void EachPadTimesItsOwnHold()
    {
        ReadAt(1000, LB, pad: 0);
        ReadAt(1200, LB, pad: 1);

        Assert.Equal(LB, ReadAt(1300, LB, pad: 0));
        Assert.Equal(0, ReadAt(1300, LB, pad: 1));
        Assert.Equal(LB, ReadAt(1500, LB, pad: 1));
    }

    // After the hand-over the frame's controls no longer see the shoulder, so no mod chord on it can form: the D-pad
    // goes to the game with it, as it would without the mod.
    [Fact]
    public void AfterTheHandOverTheControlsDontSeeTheShoulderSoNoChordForms()
    {
        var seen = new List<int>();
        var controls = new SeeingControls(seen, buttons => buttons == (LB | Down) ? LB : buttons);
        var frame = new InputFrame(new NoGame(), new MenuScreens([]), [controls], null, null, null, taps: taps);

        now = 1000;
        Assert.Equal(0, frame.Pad(0, LB, made: true).Buttons);
        now = 1300;
        Assert.Equal(LB, frame.Pad(0, LB, made: true).Buttons);
        now = 1316;
        Assert.Equal(LB | Down, frame.Pad(0, LB | Down, made: true).Buttons);
        now = 1332;
        Assert.Equal(0, frame.Pad(0, 0, made: true).Buttons);

        Assert.Equal([LB, 0, Down, 0], seen);
    }

    [Fact]
    public void TheLimitsComeFromTheSettingsFileWithinRange()
    {
        var parsed = ModSettings.Parse(
            """{ "limits": { "shoulders": { "holdDelay": 5000, "tapLength": 30 } } }""", SettingCatalogue.Empty, _ => { });

        Assert.Equal(new ShoulderTapLimits(1000, 30), ShoulderTapLimits.From(parsed));
        Assert.Equal(new ShoulderTapLimits(300, 50), ShoulderTapLimits.From(ModSettings.Defaults));
    }

    // Ready for a slider in the settings menu (#754): each default is in range and on a step.
    [Fact]
    public void EachLimitIsASlider()
    {
        foreach (var slider in new[] { ShoulderTapLimits.HoldDelay, ShoulderTapLimits.TapLength })
        {
            Assert.InRange(slider.Default, slider.Min, slider.Max);
            Assert.True(slider.Step > 0);
            Assert.Equal(0, (slider.Max - slider.Min) % slider.Step);
            Assert.Equal(0, (slider.Default - slider.Min) % slider.Step);
        }
        Assert.Equal(("shoulders", "holdDelay"), (ShoulderTapLimits.HoldDelay.Group, ShoulderTapLimits.HoldDelay.Name));
        Assert.Equal(("shoulders", "tapLength"), (ShoulderTapLimits.TapLength.Group, ShoulderTapLimits.TapLength.Name));
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
