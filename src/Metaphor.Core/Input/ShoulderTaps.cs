namespace Metaphor.Core.Input;

/// <summary>LB and RB hold-or-tap (#811, the maintainer's design of 2026-11-23). The game acts on a shoulder button as
/// it goes down (LB opens Tactics in a battle and the popularity ranking in the field, RB the map), before a mod chord
/// on it can form. So while a hold (each feature's, <see cref="Features.FeatureContext.AddShoulderHold"/>) says the
/// mod's chords use a shoulder, that shoulder is kept from the game from the read it goes down. Another button going
/// down while it's held, the other shoulder too, makes it the mod's chord, and the game never sees the shoulder. Let go
/// with no chord, it reaches the game as a tap: held down from the release read for at least <see cref="TapReads"/>
/// reads and the tap length, then let go. Held alone for the hold delay (#835, both in <see cref="ShoulderTapLimits"/>),
/// it is handed to the game as a hold from that read until let go, so the game's held paging works; from then the
/// controls no longer see it, so no mod chord forms on it, and every press reaches the game until it is let go. The
/// holds are asked at the press, which decides, and again at the release or the hand-over, so a shoulder is dropped
/// rather than handed to a screen it wasn't pressed on. Each pad keeps its own state
/// (Steam Input's second pad reads apart, #225), and a read the game reports as not made changes nothing. Safe to call
/// from any of the game's threads; the holds are asked under its lock, so they must not call back into it.</summary>
/// <remarks>How many reads the game needs to see a button down to act on it is <c>[NEEDS VERIFICATION]</c>: the pad is
/// read about once a frame (docs/raw-sources/input-route-2026-11-16.md, itself unverified), and a tap of three reads and
/// 50 ms (three frames at 60 fps, about a quick press by hand) is the guess the by-ear check on #811 settles.</remarks>
public sealed class ShoulderTaps
{
    /// <summary>LB in the game's pad mask, <c>0x0400</c> on game build 548ddc95… (Steam build 18330018):
    /// docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.</summary>
    public const int LeftShoulder = 0x0400;

    /// <summary>RB in the game's pad mask, <c>0x0800</c>, same source.</summary>
    public const int RightShoulder = 0x0800;

    /// <summary>The only buttons ever held back.</summary>
    public const int Shoulders = LeftShoulder | RightShoulder;

    /// <summary>The fewest reads a tap is held down for.</summary>
    public const int TapReads = 3;

    private readonly Func<int>[] holds;
    private readonly Func<long> milliseconds;
    private readonly Func<ShoulderTapLimits> limits;
    private readonly Lock gate = new();
    private readonly Dictionary<int, PadState> pads = [];

    /// <summary>Taps over <paramref name="holds"/>, each saying which shoulders its chords use right now (0 for none),
    /// timed by <paramref name="milliseconds"/>, with the default limits.</summary>
    public ShoulderTaps(IEnumerable<Func<int>> holds, Func<long> milliseconds)
        : this(holds, milliseconds, () => ShoulderTapLimits.Default)
    {
    }

    /// <summary>Taps as above, timed by the <paramref name="limits"/> in force, read on each pad read (#835).</summary>
    public ShoulderTaps(IEnumerable<Func<int>> holds, Func<long> milliseconds, Func<ShoulderTapLimits> limits)
    {
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(limits);
        this.holds = [.. holds];
        this.milliseconds = milliseconds;
        this.limits = limits;
    }

    /// <summary>Takes one read of <paramref name="pad"/>: <paramref name="buttons"/> as the game read them and
    /// <paramref name="handed"/> as the controls hand them on, and returns what to hand the game (<see cref="Read"/>
    /// then <see cref="ShoulderRead.Game"/>).</summary>
    public int Pad(int pad, int buttons, int handed, bool made) => Read(pad, buttons, made).Game(handed);

    /// <summary>Takes one read of <paramref name="pad"/>, <paramref name="buttons"/> as the game read them, before the
    /// controls see it, and says what the controls see and what the game gets. A read not made changes nothing.
    /// </summary>
    public ShoulderRead Read(int pad, int buttons, bool made)
    {
        if (!made || holds.Length == 0)
        {
            return ShoulderRead.None;
        }
        lock (gate)
        {
            if (!pads.TryGetValue(pad, out var state))
            {
                state = new PadState();
                pads.Add(pad, state);
            }
            return Step(state, buttons);
        }
    }

    private ShoulderRead Step(PadState state, int buttons)
    {
        var down = buttons & ~state.Previous;
        state.Previous = buttons;
        var now = milliseconds();
        var timing = limits();
        if (state.Tap != 0)
        {
            // A new press of the tapped shoulder ends the tap, and is held back in turn below.
            var over = state.TapReads >= TapReads && now - state.TapFrom >= timing.TapMilliseconds;
            if (over || (down & state.Tap) != 0)
            {
                state.Tap = 0;
            }
            else
            {
                state.TapReads++;
            }
        }
        // A handed-over shoulder is the game's until let go, and while one is, so is every other press.
        state.Over &= buttons;
        var shouldersDown = down & Shoulders;
        if (state.Over != 0)
        {
            return new(0, state.Tap | state.Over, state.Over);
        }
        if (state.Held == 0)
        {
            var back = shouldersDown == 0 ? 0 : shouldersDown & Holds();
            if (back != 0)
            {
                state.Held = back;
                state.Pressed = back;
                state.HeldFrom = now;
                // Both shoulders at once, or another button on the same read, is a chord already.
                state.Chord = back != down || back == Shoulders;
            }
        }
        else
        {
            if (down != 0)
            {
                state.Chord = true;
            }
            var more = shouldersDown & ~state.Held;
            if (more != 0)
            {
                more &= Holds();
                state.Held |= more;
                state.Pressed |= more;
            }
            state.Held &= buttons;
            if (state.Held == 0)
            {
                var tap = state.Chord ? 0 : state.Pressed & Holds();
                if (tap != 0)
                {
                    state.Tap = tap;
                    state.TapReads = 1;
                    state.TapFrom = now;
                }
                state.Pressed = 0;
                state.Chord = false;
            }
        }
        if (state.Held != 0 && !state.Chord && now - state.HeldFrom >= timing.HoldDelayMilliseconds)
        {
            // Held alone past the delay: the game's from this read, asked again as at a release.
            var over = state.Held & Holds();
            if (over != 0)
            {
                state.Over = over;
                state.Held = 0;
                state.Pressed = 0;
                return new(0, state.Tap | over, over);
            }
        }
        return new(state.Held, state.Tap, 0);
    }

    private int Holds()
    {
        var held = 0;
        foreach (var hold in holds)
        {
            held |= hold();
        }
        return held & Shoulders;
    }

    private sealed class PadState
    {
        public int Previous { get; set; }

        public int Held { get; set; }

        public int Pressed { get; set; }

        public bool Chord { get; set; }

        public int Tap { get; set; }

        public int TapReads { get; set; }

        public long TapFrom { get; set; }

        public long HeldFrom { get; set; }

        public int Over { get; set; }
    }
}

/// <summary>What one pad read of <see cref="ShoulderTaps"/> decided: the shoulders kept from the game
/// (<see cref="HeldBack"/>), those put in for it (<see cref="Added"/>: a tap being replayed, or a shoulder handed over
/// as a hold), and those handed over (<see cref="HandedOver"/>), which the controls no longer see, so no chord forms on
/// them (#835).</summary>
public readonly record struct ShoulderRead(int HeldBack, int Added, int HandedOver)
{
    /// <summary>A read that changes nothing.</summary>
    public static ShoulderRead None { get; } = new(0, 0, 0);

    /// <summary>The buttons the controls see of <paramref name="buttons"/>.</summary>
    public int Controls(int buttons) => buttons & ~HandedOver;

    /// <summary>What the game gets, from <paramref name="handed"/>, the buttons the controls handed on.</summary>
    public int Game(int handed) => (handed & ~HeldBack) | Added;
}
