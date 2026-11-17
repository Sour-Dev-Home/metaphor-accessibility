using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

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
        controls.WindowOpened();

        controls.Pad(0, 0);
        controls.Pad(0, X);

        Assert.Equal([Said("Ada: Two.")], announcer.Said);
    }

    [Fact]
    public void HoldingXRepeatsOnce()
    {
        history.Add("Ada: One.");
        controls.WindowOpened();

        controls.Pad(0, X);
        controls.Pad(0, X);
        controls.Pad(0, X);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void ReleasingAndPressingAgainRepeatsAgain()
    {
        history.Add("Ada: One.");
        controls.WindowOpened();

        controls.Pad(0, X);
        controls.Pad(0, 0);
        controls.Pad(0, X);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void XIsHiddenFromTheGameWhileTheWindowIsOpenAndOtherButtonsAreNot()
    {
        controls.WindowOpened();

        Assert.Equal(A, controls.Pad(0, X | A));
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
        controls.WindowOpened();
        controls.WindowClosed();

        Assert.Equal(X, controls.Pad(0, X));
    }

    [Fact]
    public void AButtonAlreadyHeldWhenTheWindowOpensDoesNotRepeat()
    {
        history.Add("Ada: One.");
        controls.Pad(0, X);
        controls.WindowOpened();

        controls.Pad(0, X);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ASecondPadHoldingXWhileTheFirstAlreadyDoesNotRepeatAgain()
    {
        // Steam Input's virtual pad can report the same press as the physical one (spike-session-2-2026-11-16.md).
        history.Add("Ada: One.");
        controls.WindowOpened();

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
        controls.WindowOpened();

        for (var frame = 0; frame < 100; frame++)
        {
            controls.Pad(1, 0);
        }

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void RepeatingWithNoLineYetSaysSo()
    {
        controls.WindowOpened();

        controls.Pad(0, X);

        Assert.Equal([Said("No dialogue to repeat")], announcer.Said);
    }

    [Fact]
    public void RRepeatsTheLastLineAndIsHiddenFromTheGame()
    {
        history.Add("Ada: One.");
        controls.WindowOpened();

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
        controls.WindowOpened();

        controls.Keys([R]);
        controls.Keys([R]);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void BothBumpersTogetherTurnTheReaderOffAndSayIt()
    {
        controls.WindowOpened();

        controls.Pad(0, LeftBumper);
        controls.Pad(0, LeftBumper | RightBumper);

        Assert.False(reader.On);
        Assert.Equal([Said("Dialogue reader off")], announcer.Said);
    }

    [Fact]
    public void BothBumpersAreHiddenFromTheGameDuringDialogue()
    {
        controls.WindowOpened();

        Assert.Equal(A, controls.Pad(0, LeftBumper | RightBumper | A));
    }

    [Fact]
    public void OneBumperAloneDoesNotTurnTheReaderOff()
    {
        controls.WindowOpened();

        controls.Pad(0, LeftBumper);

        Assert.True(reader.On);
    }

    [Fact]
    public void TTurnsTheReaderOffThenOn()
    {
        controls.WindowOpened();

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
        controls.WindowOpened();
        controls.Keys([T]);

        controls.Keys([R]);

        Assert.Equal(Said("Ada: One."), announcer.Said[^1]);
    }
}
