using Metaphor.Core.Input;
using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Input;

/// <summary>The per-frame input rules (#301) with stand-in controls: the frame reads the game state, hands each read to
/// the menu controls and then the dialogue controls, and hands the game only input it read. The replays in
/// Metaphor.Input.Tests run it with the real controls.</summary>
[Trait("Category", "Unit")]
public sealed class InputFrameTests
{
    private const int A = 0x1000;
    private const int B = 0x2000;

    private readonly Game game = new();
    private readonly MenuScreens screens = new([]);
    private readonly Controls menus = new();
    private readonly WindowControls dialogue = new();

    private InputFrame Frame() => new(game, screens, menus, dialogue);

    private sealed class Game : IGameInputState
    {
        public long? Window { get; set; }

        public bool Open { get; set; }

        public long? DialogueWindow() => Window;

        public bool MenusOpen() => Open;
    }

    private class Controls : IInputControls
    {
        public Func<int, int> OnPad { get; set; } = buttons => buttons;

        public Func<IReadOnlyList<int>, IReadOnlyList<int>> OnKeys { get; set; } = keys => keys;

        public List<string> Seen { get; } = [];

        public int Pad(int pad, int buttons, bool made)
        {
            Seen.Add($"pad {pad} 0x{buttons:x} {made}");
            return OnPad(buttons);
        }

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
        {
            Seen.Add($"keys {string.Join(",", keys)}");
            return OnKeys(keys);
        }
    }

    private sealed class WindowControls : Controls, IWindowInputControls
    {
        public List<long?> WindowReads { get; } = [];

        public void WindowRead(long? currentMessage) => WindowReads.Add(currentMessage);
    }

    private sealed class Watcher : IKeyWatcher, IPadWatcher
    {
        public List<string> Seen { get; } = [];

        public void Keys(IReadOnlyList<int> keys) => Seen.Add(string.Join(",", keys));

        public void Pad(int pad, int buttons, bool made) => Seen.Add($"pad {pad} 0x{buttons:x} {made}");
    }

    private sealed class Block : IInputBlock
    {
        public bool BlocksPad { get; set; }

        public bool BlocksKeys { get; set; }
    }

    // #754: while the settings menu is open the controls still take each read, so the menu hears its keys, but the
    // game gets nothing, the keyboard's fixed keys included.
    [Fact]
    public void WhileBlockedTheControlsTakeEachReadAndTheGameGetsNothing()
    {
        var block = new Block { BlocksPad = true, BlocksKeys = true };
        var frame = new InputFrame(game, screens, [menus, dialogue], null, null, null, block: block);

        var keys = frame.Keys([0x1B, 0x26]);
        var pad = frame.Pad(0, A | B, made: true);

        Assert.Equal(["keys 27,38", "pad 0 0x3000 True"], menus.Seen);
        Assert.Empty(keys.Keys);
        Assert.True(keys.Blocked);
        Assert.Equal(0, pad.Buttons);
        Assert.Equal(A, frame.Pad(0, A, made: false).Buttons);
        block.BlocksPad = block.BlocksKeys = false;
        Assert.False(frame.Keys([0x1B]).Blocked);
        Assert.Equal(A, frame.Pad(0, A, made: true).Buttons);
    }

    // #597: the mark key works on every screen, so its controls take each read before the menus', and what they drop
    // reaches neither the menus nor the game.
    [Fact]
    public void TheReportControlsTakeEachReadFirst()
    {
        var report = new Controls { OnKeys = keys => [.. keys.Where(key => key != 0x77)], OnPad = buttons => buttons & ~A };
        var frame = new InputFrame(game, screens, menus, dialogue, InputFrame.NoControls, report: report);

        var keys = frame.Keys([0x77, 0x0D]);
        var pad = frame.Pad(0, A | B, made: true);

        Assert.Equal([0x0D], keys.Keys);
        Assert.Equal(["keys 13", "pad 0 0x2000 True"], menus.Seen);
        Assert.Equal(B, pad.Buttons);
    }

    // #784: the feature modules' controls as one ordered list: each read reaches them in turn, each handed what the
    // one before left, and every control that takes window reads gets each one.
    [Fact]
    public void AListOfControlsTakesEachReadInItsOrder()
    {
        var first = new Controls { OnKeys = keys => [.. keys.Where(key => key != 1)], OnPad = buttons => buttons & ~A };
        var second = new WindowControls();
        var third = new WindowControls { OnKeys = keys => [.. keys.Where(key => key != 2)] };
        game.Window = 7;
        var frame = new InputFrame(game, screens, [first, second, third], null, null, null);

        var keys = frame.Keys([1, 2, 3]);
        var pad = frame.Pad(0, A | B, made: true);

        Assert.Equal([3], keys.Keys);
        Assert.Equal(["keys 2,3", "pad 0 0x2000 True"], second.Seen);
        Assert.Equal(["keys 2,3", "pad 0 0x2000 True"], third.Seen);
        Assert.Equal(B, pad.Buttons);
        Assert.Equal([7L, 7L], second.WindowReads);
        Assert.Equal([7L, 7L], third.WindowReads);
    }

    // #807: typing a name at new game fired the reader (T) and repeat (R) keys. While the game takes typed text, the
    // keys that type a character reach no control and still reach the game; F8 (the mark key) works as before.
    [Fact]
    public void WhileTextIsTypedTheCharacterKeysReachNoControlButReachTheGame()
    {
        const int T = 0x54, R = 0x52, Digit = 0x31, Bracket = 0xDB, F8 = 0x77;
        var controls = new Controls { OnKeys = keys => [.. keys.Where(key => key != F8)] };
        var frame = new InputFrame(game, screens, [controls], null, null, null, typing: () => true);

        var keys = frame.Keys([T, R, Digit, Bracket, F8]);

        Assert.Equal(["keys 119"], controls.Seen);
        Assert.Equal([T, R, Digit, Bracket], keys.Keys);
    }

    // #585: Ctrl+R reads the typed name back, so while text is typed a Ctrl chord isn't typing: the letters reach the
    // controls, and the game gets what they hand on (R taken by the control is not typed).
    [Theory]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    public void WhileTextIsTypedACtrlChordStillReachesTheControls(int ctrl)
    {
        const int R = 0x52;
        var controls = new Controls { OnKeys = keys => [.. keys.Where(key => key != R)] };
        var frame = new InputFrame(game, screens, [controls], null, null, null, typing: () => true);

        var keys = frame.Keys([ctrl, R]);

        Assert.Equal([$"keys {ctrl},{R}"], controls.Seen);
        Assert.Equal([ctrl], keys.Keys);
    }

    [Fact]
    public void OnceTheTextIsDoneTheCharacterKeysReachTheControlsAgain()
    {
        var typing = true;
        var controls = new Controls { OnKeys = keys => [] };
        var frame = new InputFrame(game, screens, [controls], null, null, null, typing: () => typing);

        frame.Keys([0x54]);
        typing = false;
        var keys = frame.Keys([0x54]);

        Assert.Equal(["keys ", "keys 84"], controls.Seen);
        Assert.Empty(keys.Keys);
    }

    [Fact]
    public void WhileTextIsTypedAReadOfOnlyCharacterKeysIsHandedBackAsItIs()
    {
        var read = new[] { 0x41, 0x42 };
        var frame = new InputFrame(game, screens, [new Controls()], null, null, null, typing: () => true);

        Assert.Same(read, frame.Keys(read).Keys);
    }

    [Fact]
    public void AFrameWithNoControlsHandsEveryReadOn()
    {
        var frame = new InputFrame(game, screens, [], null, null, null);

        Assert.Equal([1, 2], frame.Keys([1, 2]).Keys);
        Assert.Equal(A | B, frame.Pad(0, A | B, made: true).Buttons);
    }

    // #562: the settings confirm questions see each pad read as the game read it, before the menu controls hide their
    // buttons, made or not, and change nothing.
    [Fact]
    public void APadWatcherSeesTheGamesReadAndChangesNothing()
    {
        var watcher = new Watcher();
        menus.OnPad = buttons => buttons & ~A;
        var frame = new InputFrame(game, screens, menus, dialogue, InputFrame.NoControls, padWatcher: watcher);

        var answer = frame.Pad(1, A | B, made: true);
        frame.Pad(0, B, made: false);

        Assert.Equal(["pad 1 0x3000 True", "pad 0 0x2000 False"], watcher.Seen);
        Assert.Equal(B, answer.Buttons);
    }

    // #585: the name entry's Enter hint sees each keyboard read as the game read it, before the menu controls drop
    // their keys, and changes nothing.
    [Fact]
    public void AKeyWatcherSeesTheGamesReadAndChangesNothing()
    {
        var watcher = new Watcher();
        menus.OnKeys = keys => [.. keys.Where(key => key != 0x09)];
        var frame = new InputFrame(game, screens, menus, dialogue, InputFrame.NoControls, watcher);

        var answer = frame.Keys([0x09, 0x0D]);

        Assert.Equal(["9,13"], watcher.Seen);
        Assert.Equal([0x0D], answer.Keys);
    }

    // #623: the frame tells the last-device tracker what the game read, before the controls change it.
    [Fact]
    public void TheLastDeviceFollowsTheReads()
    {
        var device = new LastInputDevice();
        menus.OnKeys = _ => [];
        var frame = new InputFrame(
            game, screens, menus, dialogue, InputFrame.NoControls, lastDevice: device);

        frame.Keys([0x20]);
        Assert.Equal(InputDevice.Keyboard, device.Current);

        frame.Pad(0, A, made: true);
        Assert.Equal(InputDevice.Controller, device.Current);
    }

    [Fact]
    public void MissingPartsAreRefused()
    {
        Assert.Throws<ArgumentNullException>("game", () => new InputFrame(null!, screens, menus, dialogue));
        Assert.Throws<ArgumentNullException>("screens", () => new InputFrame(game, null!, menus, dialogue));
        Assert.Throws<ArgumentNullException>("menus", () => new InputFrame(game, screens, null!, dialogue));
        Assert.Throws<ArgumentNullException>("dialogue", () => new InputFrame(game, screens, menus, null!));
        Assert.Throws<ArgumentNullException>("keys", () => Frame().Keys(null!));
    }

    [Fact]
    public void EachReadHandsOnTheGameStateItWasDecidedOn()
    {
        var frame = Frame();
        game.Window = 0x55;
        game.Open = true;

        var pad = frame.Pad(0, A, made: true);

        Assert.Equal(0x55, pad.Window);
        Assert.Equal(new long?[] { 0x55 }, dialogue.WindowReads);
        Assert.True(screens.MenusOpen);

        game.Window = null;
        game.Open = false;

        var keys = frame.Keys([1]);

        Assert.Null(keys.Window);
        Assert.Equal(new long?[] { 0x55, null }, dialogue.WindowReads);
        Assert.False(screens.MenusOpen);
    }

    [Fact]
    public void TheDialogueControlsGetWhatTheMenuControlsLeave()
    {
        menus.OnPad = buttons => buttons & ~A;
        menus.OnKeys = keys => [.. keys.Where(key => key != 1)];

        var frame = Frame();
        var pad = frame.Pad(2, A | B, made: false);
        var keys = frame.Keys([1, 2]);

        Assert.Equal(B, pad.Buttons);
        Assert.Equal([2], keys.Keys);
        Assert.Equal(["pad 2 0x3000 False", "keys 1,2"], menus.Seen);
        Assert.Equal(["pad 2 0x2000 False", "keys 2"], dialogue.Seen);
    }

    // #181 with #585: a frame with field controls still lets its key watcher see the game's read.
    [Fact]
    public void AKeyWatcherSeesTheReadBeforeTheFieldControlsDropKeys()
    {
        var watcher = new Watcher();
        var field = new Controls { OnKeys = keys => [.. keys.Where(key => key != 2)] };
        var frame = new InputFrame(game, screens, menus, dialogue, field, watcher);

        var answer = frame.Keys([1, 2]);

        Assert.Equal(["1,2"], watcher.Seen);
        Assert.Equal([1], answer.Keys);
    }

    [Fact]
    public void TheFieldControlsGetWhatTheDialogueControlsLeave()
    {
        var field = new Controls { OnPad = buttons => buttons & ~B, OnKeys = keys => [.. keys.Where(key => key != 2)] };
        dialogue.OnPad = buttons => buttons & ~A;
        dialogue.OnKeys = keys => [.. keys.Where(key => key != 1)];

        var frame = new InputFrame(game, screens, menus, dialogue, field);
        var pad = frame.Pad(1, A | B | 0x4, made: true);
        var keys = frame.Keys([1, 2, 3]);

        Assert.Equal(0x4, pad.Buttons);
        Assert.Equal([3], keys.Keys);
        Assert.Equal(["pad 1 0x2004 True", "keys 2,3"], field.Seen);
        Assert.Throws<ArgumentNullException>("field", () => new InputFrame(game, screens, menus, dialogue, null!));
    }

    [Fact]
    public void ControlsCannotAddButtonsOrKeysTheGameDidNotRead()
    {
        dialogue.OnPad = buttons => buttons | B;
        dialogue.OnKeys = keys => [3, .. keys, 4];

        var frame = Frame();

        Assert.Equal(A, frame.Pad(0, A, made: true).Buttons);
        Assert.Equal([1, 2], frame.Keys([1, 2]).Keys);
    }

    [Fact]
    public void KeysKeptAreInTheOrderTheGameReadThem()
    {
        dialogue.OnKeys = keys => [.. keys.Where(key => key != 2).Reverse()];

        Assert.Equal([1, 3], Frame().Keys([1, 2, 3]).Keys);
    }

    [Fact]
    public void WhenNoKeyIsDroppedTheReadItselfIsHandedBack()
    {
        IReadOnlyList<int> read = [1, 2];
        dialogue.OnKeys = keys => [.. keys];

        Assert.Same(read, Frame().Keys(read).Keys);
    }

    // #701, #184: the sticks of a made read go to the controls that take them, in order, each handed what the one before
    // left; a read not made comes back as the game read it.
    [Fact]
    public void TheSticksGoToTheStickControlsInOrder()
    {
        var first = new StickControls(sticks => sticks with { LeftX = 1 });
        var second = new StickControls(sticks => sticks with { LeftY = sticks.LeftX + 1 });
        var frame = new InputFrame(game, screens, [first, new Controls(), second], null, null, null);

        var handed = frame.Sticks(0, A, PadSticks.Rest, made: true);

        Assert.Equal(PadSticks.Rest with { LeftX = 1, LeftY = 2 }, handed);
        Assert.Equal(["0 0x1000 128"], first.Sticked);
        Assert.Equal(PadSticks.Rest, frame.Sticks(0, A, PadSticks.Rest, made: false));
        Assert.Single(first.Sticked);
    }

    // #754: while the settings menu keeps the pad from the game, no stick a control changed reaches it.
    [Fact]
    public void WhileBlockedTheGameGetsTheSticksAsRead()
    {
        var block = new Block { BlocksPad = true };
        var controls = new StickControls(sticks => sticks with { LeftX = 1 });
        var frame = new InputFrame(game, screens, [controls], null, null, null, block: block);

        Assert.Equal(PadSticks.Rest, frame.Sticks(0, 0, PadSticks.Rest, made: true));
        Assert.Single(controls.Sticked);
    }

    // #701: a stick taken while a shoulder is held makes the hold a chord, so the shoulder isn't tapped on release; a
    // stick left alone doesn't.
    [Fact]
    public void AStickTakenWhileAShoulderIsHeldMakesAChord()
    {
        const int RB = ShoulderTaps.RightShoulder;
        var taps = new ShoulderTaps([() => RB], () => 0);
        var take = true;
        var controls = new StickControls(sticks => take ? sticks.RightAtRest() : sticks);
        var frame = new InputFrame(game, screens, [controls], null, null, null, taps: taps);
        var pushed = PadSticks.Rest with { RightX = 255 };

        frame.Pad(0, RB, made: true);
        frame.Sticks(0, RB, pushed, made: true);
        Assert.Equal(0, frame.Pad(0, 0, made: true).Buttons);

        take = false;
        frame.Pad(0, RB, made: true);
        frame.Sticks(0, RB, pushed, made: true);
        Assert.Equal(RB, frame.Pad(0, 0, made: true).Buttons);
    }

    private sealed class StickControls(Func<PadSticks, PadSticks> onSticks) : Controls, IStickControls
    {
        public List<string> Sticked { get; } = [];

        public PadSticks Sticks(int pad, int buttons, PadSticks sticks)
        {
            Sticked.Add($"{pad} 0x{buttons:x} {sticks.LeftX}");
            return onSticks(sticks);
        }
    }
}
