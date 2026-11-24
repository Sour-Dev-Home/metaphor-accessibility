using System.Numerics;
using Metaphor.Core.Menus;

namespace Metaphor.Core.Input;

/// <summary>LB and RB hold-or-tap (#811, the maintainer's design of 2026-11-23). The game acts on a shoulder button as
/// it goes down (LB opens Tactics in a battle and the popularity ranking in the field, RB the map), before a mod chord
/// on it can form. So while a hold (each feature's, <see cref="Features.FeatureContext.AddShoulderHold"/>) says the
/// mod's chords use a shoulder, that shoulder is kept from the game from the read it goes down, unless a menu is open,
/// where the shoulders page the game's tabs and reach it at once (#835). Another button going down while it's held, the
/// other shoulder too, makes it the mod's chord: the game never sees the shoulder, nor that button until it is let go
/// (#811). Let go with no chord, it reaches the game as a tap: held down from the release read for at least
/// <see cref="TapReads"/> reads and the tap length (<see cref="ShoulderTapLimits"/>), then let go. A held shoulder is
/// never handed to the game while held, however long it's held alone (#835, the maintainer's decision of 2026-11-23),
/// so it can stay down while a chord's other button is pressed again and again. The holds are asked at the press, which
/// decides, and again at the release, so a tap is dropped rather than handed to a screen it wasn't pressed on. Each pad
/// keeps its own state (Steam Input's second pad reads apart, #225), and a read the game reports as not made changes
/// nothing. LT and RT work the same way for the settings menu's chord (#754, <see cref="Triggers"/>), but are held back
/// in menus too, since that menu opens anywhere; a trigger the game wants held then reaches it only as a tap on release,
/// a by-ear question on #754. L3 works the same way for the battle's status read (#925, <see cref="LeftThumb"/>),
/// whose hold lets a long press go as the mod's by saying nothing at the release. Safe to call from any of the game's threads; the holds are asked under its lock, so they
/// must not call back into it.</summary>
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

    /// <summary>LB and RB, which pass at once while a menu is open (#835).</summary>
    public const int Shoulders = LeftShoulder | RightShoulder;

    /// <summary>LT and RT in the game's pad mask, <c>0x0100</c> and <c>0x0200</c> (each counts as held past 30), same
    /// source. Held back the same way for the settings menu's chord (#754, the maintainer's decision of 2026-11-23:
    /// "triggers held back while the chord forms; a lone tap passes through"), on every screen, menus included.
    /// </summary>
    public const int Triggers = 0x0100 | 0x0200;

    /// <summary>L3 in the game's pad mask, <c>0x0002</c>, same source. Held back the same way for the battle's status
    /// read (#925, the maintainer's decision of 2026-11-24: "L3 tap goes to the game (Retry); L3 hold goes to the mod"),
    /// on every screen while its hold says so, menus included.</summary>
    public const int LeftThumb = 0x0002;

    /// <summary>The only buttons held back for a hold; another is kept from the game only when it makes a chord with
    /// one.</summary>
    public const int Holdable = Shoulders | Triggers | LeftThumb;

    /// <summary>The fewest reads a tap is held down for.</summary>
    public const int TapReads = 3;

    /// <summary>What a battle menu's cursor class starts with: each command's screen is a <c>BTL_</c> list
    /// (<c>BTL_SkillSelect</c>, <c>BTL_ItemSelect</c>, <c>BTL_OrderTop</c>), while the command buttons themselves have no
    /// shared cursor, on build 548ddc95… (docs/findings/battle-command-panel-static.md). That every battle menu's class
    /// starts so is <c>[NEEDS VERIFICATION]</c>; the by-ear log of 2026-11-23 showed no other in a battle.</summary>
    public const string BattleMenuPrefix = "BTL_";

    /// <summary>Whether a menu is open for the shoulders (#835): the game's menus read open
    /// (<see cref="MenuScreens.MenusOpen"/>), or a battle menu's cursor was called just now
    /// (<see cref="CursorEvents.AnyCalledRecently"/>, <see cref="BattleMenuPrefix"/>), as battle's menus show while the
    /// menus read closed.</summary>
    public static Func<bool> MenuOpen(MenuScreens screens, CursorEvents cursors)
    {
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(cursors);
        return () => screens.MenusOpen ||
            cursors.AnyCalledRecently(screenClass => screenClass.StartsWith(BattleMenuPrefix, StringComparison.Ordinal));
    }

    private readonly Func<int>[] holds;
    private readonly Func<long> milliseconds;
    private readonly Func<ShoulderTapLimits> limits;
    private readonly Func<bool> menuOpen;
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
        : this(holds, milliseconds, limits, () => false)
    {
    }

    /// <summary>Taps as above that hold nothing back while <paramref name="menuOpen"/> says a menu is open, asked at
    /// each shoulder press: there LB and RB page the game's tabs, so they reach it at once (#835, the maintainer's
    /// decision of 2026-11-23).</summary>
    public ShoulderTaps(
        IEnumerable<Func<int>> holds, Func<long> milliseconds, Func<ShoulderTapLimits> limits, Func<bool> menuOpen)
    {
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(milliseconds);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(menuOpen);
        this.holds = [.. holds];
        this.milliseconds = milliseconds;
        this.limits = limits;
        this.menuOpen = menuOpen;
    }

    /// <summary>Takes one read of <paramref name="pad"/>: <paramref name="buttons"/> as the game read them and
    /// <paramref name="handed"/> as the controls hand them on, and returns what to hand the game (<see cref="Read"/>
    /// then <see cref="ShoulderRead.Game"/>).</summary>
    public int Pad(int pad, int buttons, int handed, bool made) => Read(pad, buttons, made).Game(handed);

    /// <summary>Takes one read of <paramref name="pad"/>, <paramref name="buttons"/> as the game read them, before the
    /// controls see it, and says what the game gets. A read not made changes nothing.
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
        // A chord's other button is kept from the game until it is let go, even after the shoulder (#811).
        state.Swallowed &= buttons;
        var shouldersDown = down & Holdable;
        if (state.Held == 0)
        {
            var back = shouldersDown == 0 ? 0 : shouldersDown & Holds();
            if ((back & Shoulders) != 0 && menuOpen())
            {
                back &= ~Shoulders;
            }
            if (back != 0)
            {
                state.Held = back;
                state.Pressed = back;
                // Two held buttons at once (both shoulders, both triggers), or another button on the same read, is a
                // chord already.
                state.Chord = back != down || BitOperations.PopCount((uint)back) > 1;
                state.Swallowed |= down & ~Shoulders;
            }
        }
        else
        {
            if (down != 0)
            {
                // A chord: the button that made it never reaches the game (#811), which would act on the D-pad alone
                // (quick save, the memorandum).
                state.Chord = true;
                state.Swallowed |= down & ~Shoulders;
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
        return new(state.Held | state.Swallowed, state.Tap);
    }

    private int Holds()
    {
        var held = 0;
        foreach (var hold in holds)
        {
            held |= hold();
        }
        return held & Holdable;
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

        public int Swallowed { get; set; }
    }
}

/// <summary>What one pad read of <see cref="ShoulderTaps"/> decided: the buttons kept from the game
/// (<see cref="HeldBack"/>: a held shoulder, and a chord's other button, #811) and those put in for it
/// (<see cref="Added"/>: a tap being replayed).</summary>
public readonly record struct ShoulderRead(int HeldBack, int Added)
{
    /// <summary>A read that changes nothing.</summary>
    public static ShoulderRead None { get; } = new(0, 0);

    /// <summary>What the game gets, from <paramref name="handed"/>, the buttons the controls handed on.</summary>
    public int Game(int handed) => (handed & ~HeldBack) | Added;
}
