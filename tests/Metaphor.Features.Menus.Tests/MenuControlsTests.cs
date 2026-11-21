using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The menu controls (#214): the details key asks the current screen's feature for the rest of the focus's
/// panel, the press is hidden from the game, and the reader toggle silences menus and on turning on speaks the screen
/// and focus. Hook events in (cursor inputs carrying a screen class, pad and keyboard reads), announcements out through
/// the recording announcer; made-up screens and text only.</summary>
[Trait("Category", "Unit")]
public sealed class MenuControlsTests
{
    private const string ItemClass = "ItemChoice@test";
    private const string PlainClass = "PlainChoice@test";
    private const int Settle = 0x1;
    private const int R3 = 0x0004;
    private const int A = 0x4000;
    private const int LbRb = 0x400 | 0x800;
    private const int KeyF = 0x46;
    private const int KeyT = 0x54;

    private static readonly Screen Items = new("Items", ItemClass);
    private static readonly Screen Plain = new("Plain", PlainClass);

    private readonly RecordingAnnouncer announcer = new();
    private readonly SwitchedReader reader;
    private readonly CursorEvents cursors = new();
    private readonly MenuScreens screens = new([Items, Plain]);
    private readonly MenuControls controls;

    public MenuControlsTests()
    {
        reader = new SwitchedReader(announcer);
        // The game reads its menus as open unless a test closes them (#268).
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new SpeaksFocus(reader));
        var bindings = MenuBindings.Default with { ReaderButtons = LbRb, ReaderKey = KeyT };
        controls = new MenuControls(bindings, screens, reader, [new ItemDetails()]);
        screens.Observe(controls);
    }

    private void Cursor(string? screenClass, int row) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: 0, Row: row, Flags: 0, screenClass));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheDetailsKeyOnAScreenThatAnswersSpeaksItsDetailsForTheFocus()
    {
        Cursor(ItemClass, row: 2);

        controls.Pad(0, R3);

        Assert.Equal(new Announcement("Details of item 3", Interrupt: true), announcer.Said[^1]);
    }

    [Fact]
    public void APadReadNotMadeFiresNothingAndHandsItsButtonsBack()
    {
        Cursor(ItemClass, row: 2);
        var before = announcer.Said.Count;

        Assert.Equal(R3 | LbRb, controls.Pad(1, R3 | LbRb, made: false));
        Assert.Equal(before, announcer.Said.Count);
    }

    [Fact]
    public void TheDetailsKeyOnTheKeyboardSpeaksTheDetailsOfTheCurrentFocus()
    {
        Cursor(ItemClass, row: 0);
        Cursor(ItemClass, row: 1);

        controls.Keys([KeyF]);

        Assert.Equal("Details of item 2", Said().Last());
    }

    [Theory]
    [InlineData(PlainClass)]
    [InlineData("UnknownChoice@test")]
    public void TheDetailsKeyOnAScreenThatDoesNotAnswerSaysThereAreNoMoreDetails(string screenClass)
    {
        Cursor(screenClass, row: 0);

        controls.Pad(0, R3);

        Assert.Equal(new Announcement("No more details", Interrupt: true), announcer.Said[^1]);
    }

    [Fact]
    public void TheDetailsPressDoesNotReachTheGameInAMenu()
    {
        Cursor(ItemClass, row: 0);

        Assert.Equal(A, controls.Pad(0, R3 | A));
        Assert.Equal([0x41], controls.Keys([KeyF, 0x41]));
    }

    // Tab was the details key and was hidden from the game in menus, but it's the game's exit-game key on PC (maintainer,
    // 2026-11-20), so the details key moved to F and Tab always reaches the game.
    [Fact]
    public void TabReachesTheGameInAMenu()
    {
        Cursor(ItemClass, row: 0);

        Assert.Equal([0x09], controls.Keys([0x09]));
        Assert.DoesNotContain(announcer.Said, said => said.Text.StartsWith("Details", StringComparison.Ordinal));
    }

    [Fact]
    public void BeforeAnyScreenIsEnteredTheDetailsKeyDoesNothingAndReachesTheGame()
    {
        Assert.Equal(R3, controls.Pad(0, R3));
        Assert.Equal([KeyF], controls.Keys([KeyF]));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AfterIdlingOnAScreenForFiveSecondsTheDetailsKeyStillReadsDetails()
    {
        Cursor(ItemClass, row: 1);
        // Five seconds of frames at 60 a second: the same cursor input and an empty pad read each frame.
        for (var frame = 0; frame < 300; frame++)
        {
            Cursor(ItemClass, row: 1);
            controls.Pad(0, 0);
        }

        var handed = controls.Pad(0, R3);

        Assert.Equal("Details of item 2", Said().Last());
        Assert.Equal(0, handed);
    }

    [Fact]
    public void MovingToAnotherScreenReadsThatScreensDetails()
    {
        Cursor(ItemClass, row: 0);
        Cursor(PlainClass, row: 0);

        controls.Pad(0, R3);

        Assert.Equal("No more details", Said().Last());
    }

    [Fact]
    public void AHeldDetailsKeyOrASecondPadMirroringItSpeaksOnce()
    {
        Cursor(ItemClass, row: 0);
        var before = announcer.Said.Count;

        controls.Pad(0, R3);
        controls.Pad(1, R3);
        controls.Pad(0, R3);

        Assert.Equal(before + 1, announcer.Said.Count);
    }

    [Fact]
    public void WithTheReaderOffFocusMovesAndTheDetailsKeyAreSilent()
    {
        Cursor(ItemClass, row: 0);
        controls.Keys([KeyT]);
        controls.Keys([]);

        Cursor(ItemClass, row: 1);
        controls.Pad(0, R3);

        Assert.Equal(["Items, 1", "Reader off"], Said());
    }

    [Fact]
    public void TurningTheReaderBackOnSpeaksTheScreenAndFocus()
    {
        Cursor(ItemClass, row: 0);
        controls.Pad(0, LbRb);
        controls.Pad(0, 0);
        Cursor(ItemClass, row: 3);

        var handed = controls.Pad(0, LbRb);

        Assert.Equal(["Items, 1", "Reader off", "Reader on", "Items, 4"], Said());
        Assert.Equal(0, handed);
    }

    [Fact]
    public void OnceTheMenusCloseTheDetailsKeyAndTheReaderBindingReachTheGameAndSayNothing()
    {
        Cursor(ItemClass, row: 2);
        var before = announcer.Said.Count;

        screens.MenusRead(open: false);

        Assert.Equal(R3 | LbRb, controls.Pad(0, R3 | LbRb));
        Assert.Equal([KeyF, KeyT], controls.Keys([KeyF, KeyT]));
        Assert.Equal(before, announcer.Said.Count);
        Assert.True(reader.IsOn);
    }

    [Fact]
    public void ACursorThatSettlesWhileTheMenusAreClosedDoesNotTakeTheDetailsKey()
    {
        screens.MenusRead(open: false);

        Cursor(ItemClass, row: 0);

        Assert.Equal(R3, controls.Pad(0, R3));
        Assert.Equal([KeyF], controls.Keys([KeyF]));
    }

    [Fact]
    public void AScreensFeatureThatThrowsLeavesTheDetailsToTheNext()
    {
        var guarded = new MenuControls(new MenuBindings(A, KeyT), screens, reader, [new Throws(), new ItemDetails()]);
        screens.Observe(guarded);
        Cursor(ItemClass, row: 1);

        Assert.Equal(0, guarded.Pad(0, A));
        Assert.Equal("Details of item 2", Said().Last());
    }

    [Fact]
    public void PartOfABindingsChordReachesTheGameAndTheWholeChordIsHidden()
    {
        var chord = new MenuControls(new MenuBindings(R3 | A, KeyF), screens, reader, [new ItemDetails()]);
        screens.Observe(chord);
        Cursor(ItemClass, row: 0);

        // While a menu is open: part of the details chord is the game's, the whole chord is hidden.
        Assert.Equal(R3, chord.Pad(0, R3));
        Assert.Equal(A, chord.Pad(0, A));
        Assert.Equal(0, chord.Pad(0, R3 | A));
    }

    [Fact]
    public void ACursorSettledWhileTheMenusWereClosedLeavesNoFocusForTheNextMenuVisit()
    {
        // A dialogue's choice settles in the field; then the menus open, before any of their screens is entered.
        screens.MenusRead(open: false);
        Cursor(ItemClass, row: 0);
        screens.MenusRead(open: true);
        var before = announcer.Said.Count;

        Assert.Equal(R3, controls.Pad(0, R3));
        Assert.Equal([KeyF], controls.Keys([KeyF]));
        Assert.Equal(before, announcer.Said.Count);
    }

    [Fact]
    public void AfterTheMenusReopenTheDetailsKeyWaitsForAScreenToBeEnteredAgain()
    {
        Cursor(ItemClass, row: 2);
        screens.MenusRead(open: false);
        screens.MenusRead(open: true);

        Assert.Equal(R3, controls.Pad(0, R3));
        controls.Pad(0, 0);
        Cursor(ItemClass, row: 1);

        Assert.Equal(0, controls.Pad(0, R3));
        Assert.Equal("Details of item 2", Said().Last());
    }

    [Fact]
    public void ADetailsKeyHeldAcrossTheCloseReachesTheGameAndDoesNotSpeakWhenTheMenusReopen()
    {
        Cursor(ItemClass, row: 0);
        controls.Pad(0, R3);
        var before = announcer.Said.Count;
        screens.MenusRead(open: false);

        Assert.Equal(R3, controls.Pad(0, R3));
        screens.MenusRead(open: true);
        Cursor(ItemClass, row: 1);
        controls.Pad(0, R3);

        // Only the screen's entry: the still-held key is no new press.
        Assert.Equal(["Items, 2"], Said().Skip(before));
    }

    /// <summary>A screen's feature that answers the details key for <see cref="Items"/>.</summary>
    private sealed class ItemDetails : IScreenDetails
    {
        public string? Details(ScreenEvent focus) =>
            focus.Screen == Items ? $"Details of item {focus.Index + 1}" : null;
    }

    /// <summary>A screen's feature whose details always fail.</summary>
    private sealed class Throws : IScreenDetails
    {
        public string? Details(ScreenEvent focus) => throw new InvalidOperationException("made-up failure");
    }

    /// <summary>A screen's feature that speaks every screen's name on entry and its position on each focus move.</summary>
    private sealed class SpeaksFocus(IAnnouncer announcer) : IScreenObserver
    {
        public void Observe(ScreenEvent change)
        {
            if (change.Change == ScreenChange.Entered)
            {
                announcer.Announce(new Announcement($"{change.Screen?.Name}, {change.Index + 1}", Interrupt: true));
            }
            else if (change.Change == ScreenChange.FocusMoved)
            {
                announcer.Announce(new Announcement($"{change.Index + 1}", Interrupt: true));
            }
        }
    }

    /// <summary>The reader switch as the dialogue feature's is: drops what it's handed while off.</summary>
    private sealed class SwitchedReader(IAnnouncer announcer) : IReaderSwitch
    {
        public bool IsOn { get; private set; } = true;

        public void Announce(Announcement announcement)
        {
            if (IsOn)
            {
                announcer.Announce(announcement);
            }
        }

        public void Toggle()
        {
            IsOn = !IsOn;
            announcer.Announce(new Announcement(IsOn ? "Reader on" : "Reader off", Interrupt: true));
        }
    }
}
