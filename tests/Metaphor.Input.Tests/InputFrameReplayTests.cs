using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Menus;

namespace Metaphor.Input.Tests;

/// <summary>The input frame (#301) replayed from recorded sessions, with the menu and dialogue controls wired as the mod
/// wires them and a fake game state that the fixtures' menus-open and window reads set. Each pad read goes through
/// either the frame's pad path or, translated to the keys bound to the same controls, its keyboard path, so both paths
/// meet the same sessions. A keyboard read has no "made" flag (#225), so the keyboard path skips pad reads not made and
/// any pad but 0, whose reads the keyboard has no counterpart for.</summary>
[Trait("Category", "Unit")]
public sealed class InputFrameReplayTests
{
    private const int R3 = 0x0004;
    private const int X = 0x8000;
    private const int LeftBumper = 0x0400;
    private const int LbRb = 0x0400 | 0x0800;
    private const int KeyF = 0x46;
    private const int KeyR = 0x52;
    private const int KeyT = 0x54;
    private const long OpenWindow = 0x1315afe20;

    private readonly FakeGame game = new();
    private readonly CursorEvents cursors = new();
    private readonly DialogueHistory history = new();
    private readonly RecordingAnnouncer menuSpeech = new();
    private readonly CountingReader menuReader;
    private readonly RecordingAnnouncer dialogueSpeech = new();
    private readonly DialogueReader dialogueReader;
    private readonly InputFrame frame;

    public InputFrameReplayTests()
    {
        var screens = new MenuScreens(KnownScreens.All);
        cursors.Observe(screens);
        menuReader = new CountingReader(menuSpeech);
        var menus = new MenuControls(
            MenuBindings.Default with
            {
                ReaderButtons = DialogueBindings.Default.ReaderButtons,
                ReaderKey = DialogueBindings.Default.ReaderKey,
            },
            screens, menuReader, []);
        screens.Observe(menus);
        dialogueReader = new DialogueReader(dialogueSpeech);
        var dialogue = new DialogueControls(DialogueBindings.Default, history, dialogueReader, dialogueSpeech);
        frame = new InputFrame(game, screens, menus, dialogue);
    }

    public enum Route
    {
        Pad,
        Keyboard,
    }

    [Theory]
    [InlineData(Route.Pad)]
    [InlineData(Route.Keyboard)]
    public void MenusTakeTheReaderBindingFirstAndTheDialogueTakesItEverywhereElse(Route route)
    {
        // Session 6's R3 presses, made the reader binding instead, with the dialogue window open throughout.
        game.Window = OpenWindow;

        var reads = Replay("menus-closed-session6.txt", route, window: false, remap: buttons => buttons == R3 ? LbRb : buttons);

        // The title, the main menu and the title again are the menus'; the field, after the menu, a battle and a
        // dialogue's choice are the dialogue's. Each press toggles once, and every one is hidden from the game.
        Assert.Equal(3, menuReader.Toggles);
        Assert.Equal(
            ["Dialogue reader off", "Dialogue reader on", "Dialogue reader off", "Dialogue reader on"],
            dialogueSpeech.Said.Select(said => said.Text));
        Assert.All(reads.Where(read => read.Buttons == LbRb), read => Assert.Equal(0, read.Handed));
    }

    [Theory]
    [InlineData(Route.Pad)]
    [InlineData(Route.Keyboard)]
    public void TheDetailsBindingIsTakenOnlyWhileTheMenusReadOpen(Route route)
    {
        var reads = Replay("menus-closed-session6.txt", route);

        Assert.Equal([0, R3, 0, R3, R3, R3, 0], reads.Where(read => read.Buttons == R3).Select(read => read.Handed));
        Assert.Equal(["No more details", "No more details", "No more details"], menuSpeech.Said.Select(said => said.Text));
    }

    [Theory]
    [InlineData(Route.Pad)]
    [InlineData(Route.Keyboard)]
    public void MenusUnknownHandEveryPressToTheGame(Route route)
    {
        var reads = Replay("menus-closed-session6.txt", route, menus: false);

        Assert.All(reads, read => Assert.Equal(read.Buttons, read.Handed));
        Assert.Empty(menuSpeech.Said);
        Assert.Equal(0, menuReader.Toggles);
    }

    [Fact]
    public void PartOfTheReaderChordReachesTheGameInAMenu()
    {
        var reads = Replay("menus-closed-session6.txt", Route.Pad, remap: buttons => buttons == R3 ? LeftBumper : buttons);

        Assert.Equal(7, reads.Count(read => read.Buttons == LeftBumper));
        Assert.All(reads, read => Assert.Equal(read.Buttons, read.Handed));
        Assert.Equal(0, menuReader.Toggles);
    }

    [Theory]
    [InlineData(Route.Pad)]
    [InlineData(Route.Keyboard)]
    public void WithTheWindowReadTheOneXInDialogueRepeatsTheLineAndEveryOtherPressReachesTheGame(Route route)
    {
        var reads = Replay("window-pointer-field-talk.txt", route);

        var x = Assert.Single(reads, read => read.Open && read.Buttons == X);
        Assert.Equal(0, x.Handed);
        Assert.All(reads.Where(read => !read.Open), read => Assert.Equal(read.Buttons, read.Handed));
        Assert.Equal([new Announcement("Made up line.", Interrupt: true)], dialogueSpeech.Said);
    }

    [Theory]
    [InlineData(Route.Pad)]
    [InlineData(Route.Keyboard)]
    public void WithTheWindowUnknownTheDialogueBindingsAreInert(Route route)
    {
        var reads = Replay("window-pointer-field-talk.txt", route, window: false);

        Assert.Contains(reads, read => read.Buttons == X);
        Assert.All(reads, read => Assert.Equal(read.Buttons, read.Handed));
        Assert.Empty(dialogueSpeech.Said);
        Assert.True(dialogueReader.On);
    }

    [Fact]
    public void AReadNotMadeFiresNothingAndChangesNothing()
    {
        // The menus and the window both read open, so any read that got through could fire and be hidden.
        game.Window = OpenWindow;
        game.Menus = true;

        var reads = Replay("steam-input-pad1-noise.txt", Route.Pad, menus: false, window: false);

        Assert.Contains(reads, read => !read.Made && (read.Buttons & X) != 0);
        Assert.All(reads.Where(read => !read.Made), read => Assert.Equal(read.Buttons, read.Handed));
        Assert.Empty(dialogueSpeech.Said);
        Assert.Empty(menuSpeech.Said);
    }

    [Fact]
    public void TheAnswerOnlyClearsBitsAndDropsKeys()
    {
        var adding = new AddingControls();
        var greedy = new InputFrame(game, new MenuScreens(KnownScreens.All), adding, adding);

        Assert.Equal(new PadAnswer(0x0010, null), greedy.Pad(0, 0x0010, made: true));
        Assert.Equal([KeyR], greedy.Keys([KeyR]).Keys);
    }

    [Fact]
    public void TheAnswerCarriesTheWindowReadItWasDecidedOn()
    {
        game.Window = OpenWindow;

        Assert.Equal(OpenWindow, frame.Pad(0, 0, made: true).Window);
        Assert.Equal(OpenWindow, frame.Keys([]).Window);
    }

    private List<Read> Replay(
        string scenario, Route route, bool menus = true, bool window = true, Func<int, int>? remap = null)
    {
        var reads = new List<Read>();
        SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario))).Replay(
            new MessageEvents(), cursors,
            pad: read =>
            {
                var buttons = remap?.Invoke(read.Buttons) ?? read.Buttons;
                var made = read.Returned != 0;
                var open = game.Window is { } message && message != 0;
                if (route == Route.Pad)
                {
                    reads.Add(new(buttons, frame.Pad(read.Pad, buttons, made).Buttons, made, open));
                }
                else if (made && read.Pad == 0)
                {
                    reads.Add(new(buttons, AsButtons(buttons, frame.Keys(AsKeys(buttons)).Keys), made, open));
                }
            },
            menus: menus ? open => Idle(route, () => game.Menus = open) : null,
            window: window ? current => WindowRead(route, current) : null);
        return reads;
    }

    // The fixtures have no line records: each window shows a made-up line as it opens, since repeat speaks only a line
    // from the window it is pressed in (#526).
    private void WindowRead(Route route, long? current)
    {
        if (current is not (null or 0) && game.Window is null or 0)
        {
            history.Add("Made up line.");
        }
        Idle(route, () => game.Window = current);
    }

    /// <summary>Changes the game state, then makes one read with nothing held, as the game's next frame would before
    /// any cursor input: the frame hands the state on only with a read.</summary>
    private void Idle(Route route, Action change)
    {
        change();
        if (route == Route.Pad)
        {
            frame.Pad(0, 0, made: true);
        }
        else
        {
            frame.Keys([]);
        }
    }

    /// <summary>The keys bound to the same controls as <paramref name="buttons"/>' bindings.</summary>
    private static List<int> AsKeys(int buttons)
    {
        var keys = new List<int>();
        if ((buttons & R3) != 0)
        {
            keys.Add(KeyF);
        }
        if ((buttons & X) != 0)
        {
            keys.Add(KeyR);
        }
        if ((buttons & LbRb) == LbRb)
        {
            keys.Add(KeyT);
        }
        return keys;
    }

    /// <summary><paramref name="buttons"/> with the bindings <see cref="AsKeys"/> turned into keys replaced by the ones
    /// <paramref name="handed"/> kept.</summary>
    private static int AsButtons(int buttons, IReadOnlyList<int> handed)
    {
        var translated = (buttons & (R3 | X)) | ((buttons & LbRb) == LbRb ? LbRb : 0);
        var kept = (handed.Contains(KeyF) ? R3 : 0) | (handed.Contains(KeyR) ? X : 0) | (handed.Contains(KeyT) ? LbRb : 0);
        return (buttons & ~translated) | kept;
    }

    private sealed record Read(int Buttons, int Handed, bool Made, bool Open);

    private sealed class FakeGame : IGameInputState
    {
        public long? Window { get; set; }

        public bool Menus { get; set; }

        public long? DialogueWindow() => Window;

        public bool MenusOpen() => Menus;
    }

    /// <summary>A reader that is always on and counts its toggles.</summary>
    private sealed class CountingReader(IAnnouncer announcer) : IReaderSwitch
    {
        public int Toggles { get; private set; }

        public bool IsOn => true;

        public void Announce(Announcement announcement) => announcer.Announce(announcement);

        public void Toggle() => Toggles++;
    }

    /// <summary>Controls that hand on more than they were given: a bit and a key that weren't held.</summary>
    private sealed class AddingControls : IWindowInputControls
    {
        public int Pad(int pad, int buttons, bool made) => buttons | 0x0001;

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => [.. keys, KeyF];

        public void WindowRead(long? currentMessage)
        {
        }
    }
}
