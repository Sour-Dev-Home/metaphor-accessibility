using Metaphor.Core.Menus;

namespace Metaphor.Core.Input;

/// <summary>The game state the input frame asks about on each read (#301), which the hook layer reads from the globals
/// the loader locates (#300).</summary>
public interface IGameInputState
{
    /// <summary>The message the dialogue window holds: non-zero while a line or a choice is up, 0 when it's closed, null
    /// when it's unknown or couldn't be read (#232).</summary>
    long? DialogueWindow();

    /// <summary>Whether the game has its menus open; false when it's unknown or couldn't be read (#268).</summary>
    bool MenusOpen();
}

/// <summary>Controls that take each pad and keyboard read, act on a press, and say what to hand on.</summary>
public interface IInputControls
{
    /// <summary>Takes one read of <paramref name="pad"/>'s buttons that the game reports as made or not (#225), and
    /// returns the buttons to hand on.</summary>
    int Pad(int pad, int buttons, bool made);

    /// <summary>Takes one read of the keys held down, and returns the keys to hand on.</summary>
    IReadOnlyList<int> Keys(IReadOnlyList<int> keys);
}

/// <summary>Two input controls as one (#631): each read goes to the first, and what it hands on to the second.</summary>
public sealed class ChainedControls(IInputControls first, IInputControls second) : IInputControls
{
    public int Pad(int pad, int buttons, bool made) => second.Pad(pad, first.Pad(pad, buttons, made), made);

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => second.Keys(first.Keys(keys));
}

/// <summary>Input controls that act only while the dialogue window holds a message, so take each read of it.</summary>
public interface IWindowInputControls : IInputControls
{
    /// <summary>One read of the message the dialogue window holds, as <see cref="IGameInputState.DialogueWindow"/>.
    /// </summary>
    void WindowRead(long? currentMessage);
}

/// <summary>Watches each keyboard read without changing it, such as the name entry's Enter hint (#585).</summary>
public interface IKeyWatcher
{
    /// <summary>Takes one read of the keys held down, as the game read them.</summary>
    void Keys(IReadOnlyList<int> keys);
}

/// <summary>Watches each pad read without changing it, such as the settings confirm questions (#562).</summary>
public interface IPadWatcher
{
    /// <summary>Takes one read of <paramref name="pad"/>'s buttons as the game read them, made or not (#225).</summary>
    void Pad(int pad, int buttons, bool made);
}

/// <summary>What keeps every read from the game (#754): the settings menu while it is open, and the keys or buttons
/// that closed it until they are let go. Asked after the controls have taken the read, so the read that opens the menu
/// is already kept.</summary>
public interface IInputBlock
{
    /// <summary>Whether the game gets no pad buttons.</summary>
    bool BlocksPad { get; }

    /// <summary>Whether the game gets no keys.</summary>
    bool BlocksKeys { get; }
}

/// <summary>What the frame hands the game for one pad read, and the window read it was decided on.</summary>
public readonly record struct PadAnswer(int Buttons, long? Window);

/// <summary>What the frame hands the game for one keyboard read, and the window read it was decided on.
/// <paramref name="Blocked"/> says every key is kept from the game (#754), the fixed keys too (Escape, the arrows,
/// Enter: the mask the game keeps beside its key list, docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).</summary>
public readonly record struct KeysAnswer(IReadOnlyList<int> Keys, long? Window, bool Blocked = false);

/// <summary>The per-frame input rules (#301), behind the pad and keyboard hooks: on each read it hands the dialogue
/// controls a read of the window and the menu screens a read of whether the menus are open, then the read to the menu
/// controls first and what they leave to the dialogue controls, so in a menu the reader binding toggles once, through
/// them. A pad read the game reports as not made (#225) fires nothing and comes back unchanged. The answer only ever
/// clears bits or drops keys from the read, so the hook's write-back can't add input the game didn't see, with one
/// exception: the shoulder taps (#811, <see cref="ShoulderTaps"/>), applied after every control, hand on a tap of LB or
/// RB for a few reads after the player let go of it, a press the game held back but did see. The sticks are their own
/// answer (<see cref="Sticks"/>): a control may hand the game a stick at rest, or the left stick pushed for auto-walk
/// (#184), and never a button. It keeps no state of its
/// own; the controls and the taps lock theirs, so any of the game's threads may call it.</summary>
public sealed class InputFrame
{
    private readonly IGameInputState game;
    private readonly MenuScreens screens;
    private readonly IInputControls[] controls;
    private readonly IWindowInputControls[] windowControls;
    private readonly IStickControls[] stickControls;
    private readonly IKeyWatcher? watcher;
    private readonly IPadWatcher? padWatcher;
    private readonly LastInputDevice? lastDevice;
    private readonly Func<bool>? typing;
    private readonly ShoulderTaps? taps;
    private readonly IInputBlock? block;

    public InputFrame(IGameInputState game, MenuScreens screens, IInputControls menus, IWindowInputControls dialogue)
        : this(game, screens, menus, dialogue, NoControls)
    {
    }

    /// <summary>Controls that hand every read on unchanged, for a frame without field controls.</summary>
    public static IInputControls NoControls => PassThrough.Instance;

    /// <summary>A frame whose <paramref name="field"/> controls (#181) get what the dialogue controls leave;
    /// <paramref name="watcher"/>, when given, sees each keyboard read first, as the game read it, and changes
    /// nothing; <paramref name="padWatcher"/>, when given, sees each pad read the same way. <paramref name="report"/>,
    /// when given, takes each read before the menu controls: the mark key (#597), which works on every screen.</summary>
    public InputFrame(
        IGameInputState game, MenuScreens screens, IInputControls menus, IWindowInputControls dialogue,
        IInputControls field, IKeyWatcher? watcher = null, IPadWatcher? padWatcher = null,
        IInputControls? report = null, LastInputDevice? lastDevice = null)
        : this(game, screens, Ordered(report ?? NoControls, menus, dialogue, field), watcher, padWatcher, lastDevice)
    {
    }

    /// <summary>A frame whose <paramref name="controls"/> take each read in their order, each handed what the one
    /// before left (#784, the feature modules' order); those that take window reads get each one first. The watchers
    /// are as above. While <paramref name="typing"/> says the game takes typed text (the name entry screen), the keys
    /// that type a character reach no control and always reach the game, so a name with a T in it doesn't turn the
    /// reader off (#807); other keys, such as F8, go to the controls as usual. <paramref name="taps"/>, when given, takes
    /// each pad read after the controls: they see a held shoulder, the game a tap of it on release (#811), and once it
    /// is handed to the game as a hold the controls stop seeing it (#835). While <paramref name="block"/> says so, the
    /// game gets nothing from a read, after the controls have taken it (#754).</summary>
    public InputFrame(
        IGameInputState game, MenuScreens screens, IReadOnlyList<IInputControls> controls, IKeyWatcher? watcher,
        IPadWatcher? padWatcher, LastInputDevice? lastDevice, Func<bool>? typing = null, ShoulderTaps? taps = null,
        IInputBlock? block = null)
    {
        this.typing = typing;
        this.taps = taps;
        this.block = block;
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(controls);
        this.game = game;
        this.screens = screens;
        this.controls = [.. controls];
        windowControls = [.. this.controls.OfType<IWindowInputControls>()];
        stickControls = [.. this.controls.OfType<IStickControls>()];
        this.watcher = watcher;
        this.padWatcher = padWatcher;
        this.lastDevice = lastDevice;
    }

    /// <summary>Takes one read of <paramref name="pad"/>'s 16-bit <paramref name="buttons"/>, made or not, and returns
    /// the buttons to hand the game: a subset of them, but for a shoulder tap being replayed (#811).</summary>
    public PadAnswer Pad(int pad, int buttons, bool made)
    {
        var window = ReadState();
        padWatcher?.Pad(pad, buttons, made);
        lastDevice?.Pad(buttons);
        var shoulders = taps?.Read(pad, buttons, made) ?? ShoulderRead.None;
        var handed = buttons;
        foreach (var control in controls)
        {
            handed = control.Pad(pad, handed, made);
        }
        handed &= buttons;
        return made && block?.BlocksPad == true ? new(0, window) : new(shoulders.Game(handed), window);
    }

    /// <summary>Takes one read of <paramref name="pad"/>'s <paramref name="sticks"/>, after <see cref="Pad"/> took its
    /// <paramref name="buttons"/> (as the game read them), and returns the sticks to hand the game (#701, #184): each
    /// control that takes sticks (<see cref="IStickControls"/>) gets them in the controls' order, handed what the one
    /// before left. A stick a control changed while a shoulder is held back makes that hold a chord, so the shoulder
    /// isn't tapped to the game on release (<see cref="ShoulderTaps.Chord"/>). A read the game reports as not made, or
    /// one while the block keeps the pad from the game (#754), comes back as the game read it, so nothing is added to
    /// it.</summary>
    public PadSticks Sticks(int pad, int buttons, PadSticks sticks, bool made)
    {
        if (!made)
        {
            return sticks;
        }
        var handed = sticks;
        foreach (var control in stickControls)
        {
            handed = control.Sticks(pad, buttons, handed);
        }
        if (block?.BlocksPad == true)
        {
            return sticks;
        }
        if (handed != sticks)
        {
            taps?.Chord(pad);
        }
        return handed;
    }

    /// <summary>Takes one read of the keys held down and returns the keys to hand the game: <paramref name="keys"/>
    /// itself when none is dropped, or the ones kept, in their order.</summary>
    public KeysAnswer Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var window = ReadState();
        watcher?.Keys(keys);
        lastDevice?.Keys(keys);
        // A Ctrl chord isn't typing (#585's Ctrl+R): with Ctrl held the letters reach the controls, and the game gets
        // only what they hand on.
        var typed = typing?.Invoke() == true && !keys.Any(IsControl);
        var handed = typed && keys.Any(TypesACharacter)
            ? keys.Where(key => !TypesACharacter(key)).ToList()
            : keys;
        foreach (var control in controls)
        {
            handed = control.Keys(handed);
        }
        return block?.BlocksKeys == true ? new([], window, Blocked: true) : new(Kept(keys, handed, typed), window);
    }

    /// <summary>Whether a key types a character in a name: the letters, the digits and the brackets, the bindable keys
    /// that do (the settings file's key names, <see cref="Settings.BindingNames"/>, #661). F1 to F12 don't.</summary>
    // Either Ctrl's virtual-key code: the game's key list holds the left and right ones (0xA2, 0xA3) and skips 0x11
    // (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md, build 548ddc95).
    private static bool IsControl(int key) => key is 0x11 or 0xA2 or 0xA3;

    private static bool TypesACharacter(int key) =>
        key is (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) or 0xDB or 0xDD;

    private static IInputControls[] Ordered(
        IInputControls report, IInputControls menus, IWindowInputControls dialogue, IInputControls field)
    {
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(field);
        return [report, menus, dialogue, field];
    }

    /// <summary>Controls that hand every read on unchanged, for a frame without field controls.</summary>
    private sealed class PassThrough : IInputControls
    {
        public static PassThrough Instance { get; } = new();

        public int Pad(int pad, int buttons, bool made) => buttons;

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => keys;
    }

    private long? ReadState()
    {
        var window = game.DialogueWindow();
        foreach (var control in windowControls)
        {
            control.WindowRead(window);
        }
        screens.MenusRead(game.MenusOpen());
        return window;
    }

    // While text is typed, the character keys were kept from the controls, so are kept for the game.
    private static IReadOnlyList<int> Kept(IReadOnlyList<int> keys, IReadOnlyList<int> handed, bool typed)
    {
        if (ReferenceEquals(keys, handed))
        {
            return keys;
        }
        var kept = keys.Where(key => (typed && TypesACharacter(key)) || handed.Contains(key)).ToList();
        return kept.Count == keys.Count ? keys : kept;
    }
}
