using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The dialogue controls (#58): the repeat and reader on/off presses, seen through what the game is handed back
/// and what the player hears.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueControlsTests
{
    private const int X = 0x8000;
    private const int A = 0x4000;
    private const int LeftBumper = 0x400;
    private const int RightBumper = 0x800;
    private const int R = 0x52;
    private const int T = 0x54;
    private const int Enter = 0x0D;

    // A message pointer the window held in session 5 (dialogue-window-flag-session5-2026-11-17.md); any non-zero value
    // means a message is on screen.
    private const long Message = 0x1315afe20;

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueHistory history = new();
    private readonly DialogueReader reader;
    private readonly DialogueControls controls;

    public DialogueControlsTests()
    {
        reader = new DialogueReader(announcer);
        controls = new DialogueControls(DialogueBindings.Default, history, reader, announcer);
    }

    private static Announcement Said(string text) => new(text, Interrupt: true);

    [Fact]
    public void PressingXDuringDialogueRepeatsTheLastLine()
    {
        history.Add("Ada: One.");
        history.Add("Ada: Two.");
        controls.WindowRead(Message);

        controls.Pad(0, 0);
        controls.Pad(0, X);

        Assert.Equal([Said("Ada: Two.")], announcer.Said);
    }

    // #526: a scene whose lines weren't read replayed the opening's last line, from the window before.
    [Fact]
    public void AfterTheWindowClosesRepeatDoesNotSpeakThePreviousWindowsLine()
    {
        history.Add("More: Last line of the opening.");
        controls.WindowRead(Message);
        controls.WindowRead(0);
        controls.WindowRead(Message + 0x10);

        controls.Pad(0, X);

        Assert.Equal([Said("No dialogue to repeat")], announcer.Said);
    }

    [Fact]
    public void AfterTheWindowClosesRepeatSpeaksTheNewWindowsLine()
    {
        history.Add("More: Last line of the opening.");
        controls.WindowRead(Message);
        controls.WindowRead(null);
        history.Add("Gallica: A new line.");
        controls.WindowRead(Message);

        controls.Pad(0, X);

        Assert.Equal([Said("Gallica: A new line.")], announcer.Said);
    }

    [Fact]
    public void HoldingXRepeatsOnce()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        controls.Pad(0, X);
        controls.Pad(0, X);
        controls.Pad(0, X);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void ReleasingAndPressingAgainRepeatsAgain()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        controls.Pad(0, X);
        controls.Pad(0, 0);
        controls.Pad(0, X);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void XIsHiddenFromTheGameWhileTheWindowIsOpenAndOtherButtonsAreNot()
    {
        controls.WindowRead(Message);

        Assert.Equal(A, controls.Pad(0, X | A));
    }

    [Fact]
    public void AReadNotMadeIsHandedBackUnchangedAndRepeatsNothing()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        Assert.Equal(X | A, controls.Pad(1, X | A, made: false));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OutsideDialogueXKeepsItsJobAndRepeatsNothing()
    {
        history.Add("Ada: One.");

        Assert.Equal(X, controls.Pad(0, X));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AfterTheWindowClosesXIsHandedToTheGameAgain()
    {
        controls.WindowRead(Message);
        controls.WindowRead(0);

        Assert.Equal(X, controls.Pad(0, X));
    }

    [Fact]
    public void AButtonAlreadyHeldWhenTheWindowOpensDoesNotRepeat()
    {
        history.Add("Ada: One.");
        controls.Pad(0, X);
        controls.WindowRead(Message);

        controls.Pad(0, X);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ASecondPadHoldingXWhileTheFirstAlreadyDoesNotRepeatAgain()
    {
        // Steam Input's virtual pad can report the same press as the physical one (spike-session-2-2026-11-16.md).
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        controls.Pad(0, X);
        controls.Pad(1, X);
        controls.Pad(0, X);
        controls.Pad(1, X);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void APadThatOnlyChangesItsSticksNeverRepeats()
    {
        // The game's mask holds buttons only (input-route-2026-11-16.md); a noisy pad repeats the same mask.
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        for (var frame = 0; frame < 100; frame++)
        {
            controls.Pad(1, 0);
        }

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void RepeatingWithNoLineYetSaysSo()
    {
        controls.WindowRead(Message);

        controls.Pad(0, X);

        Assert.Equal([Said("No dialogue to repeat")], announcer.Said);
    }

    [Fact]
    public void RRepeatsTheLastLineAndIsHiddenFromTheGame()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        var handed = controls.Keys([Enter, R]);

        Assert.Equal([Enter], handed);
        Assert.Equal([Said("Ada: One.")], announcer.Said);
    }

    [Fact]
    public void OutsideDialogueRIsHandedToTheGame()
    {
        Assert.Equal([R], controls.Keys([R]));
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void HoldingRRepeatsOnce()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);

        controls.Keys([R]);
        controls.Keys([R]);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void BothBumpersTogetherTurnTheReaderOffAndSayIt()
    {
        controls.WindowRead(Message);

        controls.Pad(0, LeftBumper);
        controls.Pad(0, LeftBumper | RightBumper);

        Assert.False(reader.On);
        Assert.Equal([Said("Dialogue reader off")], announcer.Said);
    }

    [Fact]
    public void BothBumpersAreHiddenFromTheGameDuringDialogue()
    {
        controls.WindowRead(Message);

        Assert.Equal(A, controls.Pad(0, LeftBumper | RightBumper | A));
    }

    [Fact]
    public void OneBumperAloneDoesNotTurnTheReaderOff()
    {
        controls.WindowRead(Message);

        controls.Pad(0, LeftBumper);

        Assert.True(reader.On);
    }

    [Fact]
    public void TTurnsTheReaderOffThenOn()
    {
        controls.WindowRead(Message);

        controls.Keys([T]);
        controls.Keys([]);
        controls.Keys([T]);

        Assert.True(reader.On);
        Assert.Equal([Said("Dialogue reader off"), Said("Dialogue reader on")], announcer.Said);
    }

    [Fact]
    public void TheRepeatIsSpokenWhileTheReaderIsOff()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);
        controls.Keys([T]);

        controls.Keys([R]);

        Assert.Equal(Said("Ada: One."), announcer.Said[^1]);
    }

    // #206, #232: with no reading of the window, the keys do nothing and the game keeps them.

    [Fact]
    public void BeforeTheWindowIsReadTheKeysDoNothingAndTheGameKeepsThem()
    {
        history.Add("Ada: One.");

        Assert.Equal(X | A, controls.Pad(0, X | A));
        Assert.Equal([R, Enter], controls.Keys([R, Enter]));
        Assert.Equal(LeftBumper | RightBumper, controls.Pad(0, LeftBumper | RightBumper));
        Assert.True(reader.On);
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnUnreadableWindowLeavesTheKeysToTheGame()
    {
        history.Add("Ada: One.");
        controls.WindowRead(Message);
        controls.WindowRead(null);

        Assert.Equal(X, controls.Pad(0, X));
        Assert.Equal([T], controls.Keys([T]));
        Assert.True(reader.On);
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnUnreadableWindowThatBecomesReadableAgainActs()
    {
        history.Add("Ada: One.");
        controls.WindowRead(null);
        controls.WindowRead(Message);

        Assert.Equal(0, controls.Pad(0, X));
        Assert.Equal([Said("Ada: One.")], announcer.Said);
    }

    // #661: the settings file rebinds the dialogue keys, applied at the next read.
    [Fact]
    public void AKeyReboundInTheFileAppliesAtTheNextRead()
    {
        var bindings = DialogueBindings.Default;
        var rebound = new DialogueControls(() => bindings, history, reader, announcer);
        history.Add("Ada: One.");
        rebound.WindowRead(Message);
        bindings = DialogueBindings.From(ModSettings.Parse("""{ "keys": { "dialogue": { "repeatKey": "Q" } } }""", _ => { }));

        Assert.Equal([R], rebound.Keys([R]));
        Assert.Empty(rebound.Keys([0x51]));
        Assert.Equal([Said("Ada: One.")], announcer.Said);
    }

    [Fact]
    public void TheFileNamesEveryDialogueBinding()
    {
        var settings = ModSettings.Parse(
            """
            { "keys": { "dialogue": { "repeatButtons": "Y", "readerButtons": "LT+RT", "repeatKey": "F3",
              "readerKey": "none" } } }
            """, _ => { });

        Assert.Equal(new DialogueBindings(0x1000, 0x0300, 0x72, 0), DialogueBindings.From(settings));
        Assert.Equal(DialogueBindings.Default, DialogueBindings.From(ModSettings.Defaults));
    }
}
