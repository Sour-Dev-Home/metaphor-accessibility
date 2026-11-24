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
    }

    // #811, by ear 2026-11-23: LB with the D-pad still reached the game as a D-pad press (quick save, the memorandum).
    // The chord's other button is kept from the game until let go, even when no control hid it, and even past the
    // shoulder's release; pressed again on its own, it reaches the game.
    [Fact]
    public void AChordsOtherButtonIsKeptFromTheGameUntilLetGo()
    {
        Assert.Equal(0, Read(LB));
        Assert.Equal(0, Read(LB | Down));
        Assert.Equal(0, Read(Down));
        Assert.Equal(0, Read(Down));

        Assert.Equal(0, Read(0));
        Assert.Equal(Down, Read(Down));
    }

    [Fact]
    public void AButtonPressedOnTheShouldersReadIsKeptFromTheGameToo()
    {
        Assert.Equal(0, Read(LB | Down));
        Assert.Equal(0, Read(Down));
        Assert.Equal(0, Read(0));
    }

    // #835, the maintainer's decision of 2026-11-23: while a menu is open (battle Items, where LB and RB page the tabs),
    // the shoulders reach the game at once, with no hold and no delay.
    [Fact]
    public void WhileAMenuIsOpenTheShouldersReachTheGameAtOnce()
    {
        var menu = true;
        var inMenu = new ShoulderTaps([() => held], () => now, () => limits, () => menu);

        Assert.Equal(LB, inMenu.Pad(0, LB, LB, made: true));
        Assert.Equal(LB | Down, inMenu.Pad(0, LB | Down, LB | Down, made: true));
        Assert.Equal(0, inMenu.Pad(0, 0, 0, made: true));

        menu = false;
        Assert.Equal(0, inMenu.Pad(0, LB, LB, made: true));
    }

    // A battle menu shows while the game's menus read closed; its cursor, called every frame, says it is open, and it
    // closes once the cursor goes quiet. The battle's command buttons have no cursor, so there the shoulders are held.
    [Fact]
    public void AMenuIsOpenWhileTheMenusReadOpenOrABattleMenusCursorIsCalled()
    {
        var screens = new MenuScreens([]);
        var cursors = new CursorEvents(() => now);
        var menuOpen = ShoulderTaps.MenuOpen(screens, cursors);
        Assert.False(menuOpen());

        cursors.Handled(new CursorInput(0xa0, 0x100, 1, 1, 0, 0, 0x46, "BTL_ItemSelect@ope@ui@app"));
        Assert.True(menuOpen());
        now += CursorEvents.QuietMilliseconds;
        Assert.False(menuOpen());

        cursors.Handled(new CursorInput(0xb0, 0x100, 1, 1, 0, 0, 0x46, "YesNoChoiceSample@ope@ui@app"));
        Assert.False(menuOpen());
        screens.MenusRead(true);
        Assert.True(menuOpen());
    }

    // #835: once a chord has formed, the shoulder is never handed over until it's let go, however long it's held, so
    // LB can stay down while the D-pad steps through the enemies.
    [Fact]
    public void AChordIsNeverHandedOverHoweverLongItsHeld()
    {
        ReadAt(1000, LB);
        ReadAt(1100, LB | Down);
        var handed = new List<int>();
        for (var at = 1200L; at < 5000; at += 100)
        {
            handed.Add(ReadAt(at, (at / 100) % 4 == 0 ? LB | Down : LB));
        }

        Assert.All(handed, buttons => Assert.Equal(0, buttons));
        Assert.Equal([0, 0, 0, 0], [ReadAt(5000, 0), ReadAt(5016, 0), ReadAt(5032, 0), ReadAt(5048, 0)]);
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

    // #835, the maintainer's decision of 2026-11-23: where a chord can happen, a held shoulder is never handed to the
    // game however long it's held alone; let go with no chord, it is a tap, as after a short press.
    [Fact]
    public void ALoneShoulderHeldForAnyTimeIsKeptFromTheGameAndATapOnRelease()
    {
        var handed = new List<int>();
        for (var at = 1000L; at < 10000; at += 100)
        {
            handed.Add(ReadAt(at, LB));
        }

        Assert.All(handed, buttons => Assert.Equal(0, buttons));
        Assert.Equal([LB, LB, LB, LB, 0], [ReadAt(10000, 0), ReadAt(10016, 0), ReadAt(10032, 0), ReadAt(10048, 0),
            ReadAt(10064, 0)]);
    }

    // With no hand-over the controls see the shoulder for as long as it's held, so a chord can still form after a long
    // lone hold, and then no tap follows.
    [Fact]
    public void AChordFormsHoweverLongTheShoulderWasHeldAlone()
    {
        var seen = new List<int>();
        var controls = new SeeingControls(seen, buttons => buttons == (LB | Down) ? LB : buttons);
        var frame = new InputFrame(new NoGame(), new MenuScreens([]), [controls], null, null, null, taps: taps);

        now = 1000;
        Assert.Equal(0, frame.Pad(0, LB, made: true).Buttons);
        now = 5000;
        Assert.Equal(0, frame.Pad(0, LB, made: true).Buttons);
        now = 5016;
        Assert.Equal(0, frame.Pad(0, LB | Down, made: true).Buttons);
        now = 5032;
        Assert.Equal(0, frame.Pad(0, 0, made: true).Buttons);
        now = 5048;
        Assert.Equal(0, frame.Pad(0, 0, made: true).Buttons);

        Assert.Equal([LB, LB, LB | Down, 0, 0], seen);
    }

    [Fact]
    public void TheTapLengthComesFromTheLimits()
    {
        limits = new(100);
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

    // Asked again at the release: a shoulder whose context has gone during a long hold is no tap.
    [Fact]
    public void ALongHoldWhoseContextLeftIsNoTap()
    {
        ReadAt(1000, LB);
        held = 0;

        Assert.Equal(0, ReadAt(3000, LB));
        Assert.Equal(0, ReadAt(3016, 0));
    }

    // The old hold delay's key (#835) is no longer read: a settings file that still has it changes nothing.
    [Fact]
    public void TheTapLengthComesFromTheSettingsFileWithinRange()
    {
        var parsed = ModSettings.Parse(
            """{ "limits": { "shoulders": { "holdDelay": 100, "tapLength": 500 } } }""", SettingCatalogue.Empty, _ => { });

        Assert.Equal(new ShoulderTapLimits(200), ShoulderTapLimits.From(parsed));
        Assert.Equal(new ShoulderTapLimits(50), ShoulderTapLimits.From(ModSettings.Defaults));
    }

    // Ready for a slider in the settings menu (#754): its default is in range and on a step.
    [Fact]
    public void TheTapLengthIsASlider()
    {
        var slider = ShoulderTapLimits.TapLength;

        Assert.InRange(slider.Default, slider.Min, slider.Max);
        Assert.True(slider.Step > 0);
        Assert.Equal(0, (slider.Max - slider.Min) % slider.Step);
        Assert.Equal(0, (slider.Default - slider.Min) % slider.Step);
        Assert.Equal(("shoulders", "tapLength"), (slider.Group, slider.Name));
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
