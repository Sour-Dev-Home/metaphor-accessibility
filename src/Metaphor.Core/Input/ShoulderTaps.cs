namespace Metaphor.Core.Input;

/// <summary>LB and RB hold-or-tap (#811, the maintainer's design of 2026-11-23). The game acts on a shoulder button as
/// it goes down (LB opens Tactics in a battle and the popularity ranking in the field, RB the map), before a mod chord
/// on it can form. So while a hold (each feature's, <see cref="Features.FeatureContext.AddShoulderHold"/>) says the
/// mod's chords use a shoulder, that shoulder is kept from the game from the read it goes down. Another button going
/// down while it's held, the other shoulder too, makes it the mod's chord, and the game never sees the shoulder. Let go
/// with no chord, it reaches the game as a tap: held down from the release read for at least <see cref="TapReads"/>
/// reads and <see cref="TapMilliseconds"/>, then let go. The holds are asked at the press, which decides, and again at
/// the release, so a tap is dropped rather than handed to a screen it wasn't pressed on. Each pad keeps its own state
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

    /// <summary>The least time a tap is held down for.</summary>
    public const long TapMilliseconds = 50;

    private readonly Func<int>[] holds;
    private readonly Func<long> milliseconds;
    private readonly Lock gate = new();
    private readonly Dictionary<int, PadState> pads = [];

    /// <summary>Taps over <paramref name="holds"/>, each saying which shoulders its chords use right now (0 for none),
    /// timed by <paramref name="milliseconds"/>.</summary>
    public ShoulderTaps(IEnumerable<Func<int>> holds, Func<long> milliseconds)
    {
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(milliseconds);
        this.holds = [.. holds];
        this.milliseconds = milliseconds;
    }

    /// <summary>Takes one read of <paramref name="pad"/>: <paramref name="buttons"/> as the game read them and
    /// <paramref name="handed"/> as the controls hand them on, and returns what to hand the game: the held-back
    /// shoulders taken out, a tap being replayed put in. A read not made comes back as <paramref name="handed"/>.
    /// </summary>
    public int Pad(int pad, int buttons, int handed, bool made)
    {
        if (!made || holds.Length == 0)
        {
            return handed;
        }
        lock (gate)
        {
            if (!pads.TryGetValue(pad, out var state))
            {
                state = new PadState();
                pads.Add(pad, state);
            }
            return Read(state, buttons, handed);
        }
    }

    private int Read(PadState state, int buttons, int handed)
    {
        var down = buttons & ~state.Previous;
        state.Previous = buttons;
        var now = milliseconds();
        if (state.Tap != 0)
        {
            // A new press of the tapped shoulder ends the tap, and is held back in turn below.
            var over = state.TapReads >= TapReads && now - state.TapFrom >= TapMilliseconds;
            if (over || (down & state.Tap) != 0)
            {
                state.Tap = 0;
            }
            else
            {
                state.TapReads++;
            }
        }
        var shouldersDown = down & Shoulders;
        if (state.Held == 0)
        {
            var back = shouldersDown == 0 ? 0 : shouldersDown & Holds();
            if (back != 0)
            {
                state.Held = back;
                state.Pressed = back;
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
        return (handed & ~state.Held) | state.Tap;
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
    }
}
