using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>Closing the dialogue window stops its line still being spoken (#676), wired as the hook layer wires it: lines
/// through the reader, the window read on each pad read, and a fake speech output that counts its stops.</summary>
[Trait("Category", "Unit")]
public sealed class WindowCloseSpeechTests
{
    // A message pointer the window held in session 5 (dialogue-window-flag-session5-2026-11-17.md).
    private const long Message = 0x1315afe20;

    private readonly RecordingAnnouncer output = new();
    private readonly StoppableAnnouncer speech;
    private readonly SpeechSource window;
    private readonly DialogueAnnouncer dialogue;
    private readonly DialogueReader reader;
    private readonly DialogueControls controls;
    private int stops;

    public WindowCloseSpeechTests()
    {
        speech = new StoppableAnnouncer(output, () => stops++);
        window = speech.Source();
        var history = new DialogueHistory();
        reader = new DialogueReader(speech);
        dialogue = new DialogueAnnouncer(window.Through(reader), new TextCleaner(TextCleanerRules.None), history);
        controls = new DialogueControls(
            DialogueBindings.Default, history, reader, window.Through(speech), dialogue.EndConversation, window);
    }

    [Fact]
    public void ClosingTheWindowMidLineStopsIt()
    {
        dialogue.Show("Ada", "A long line.");
        controls.WindowRead(Message);

        controls.WindowRead(0);

        Assert.Equal(1, stops);
    }

    [Fact]
    public void ClosingTheWindowStopsNothingWhileTheSettingIsOff()
    {
        var history = new DialogueHistory();
        var off = new DialogueControls(
            DialogueBindings.Default, history, reader, window.Through(speech), windowSpeech: window, stopOn: () => false);
        dialogue.Show("Ada", "A long line.");
        off.WindowRead(Message);

        off.WindowRead(0);

        Assert.Equal(0, stops);
    }

    [Fact]
    public void ClosingTheWindowMidChoiceStopsTheOptions()
    {
        controls.WindowRead(Message);
        dialogue.Show("Ada", "Well?");
        dialogue.Offer("Yes\nNo"); // Queued after the line.
        controls.WindowRead(Message);

        controls.WindowRead(0);

        Assert.Equal(1, stops);
    }

    [Fact]
    public void SpeechQueuedByTheNextScreenBeforeTheCloseIsNotStopped()
    {
        dialogue.Show("Ada", "A long line.");
        controls.WindowRead(Message);
        speech.Announce(new Announcement("Main menu", Interrupt: false));

        controls.WindowRead(0);

        Assert.Equal(0, stops);
    }

    [Fact]
    public void TheNextScreensAnnouncementAfterTheCloseIsSaid()
    {
        dialogue.Show("Ada", "A long line.");
        controls.WindowRead(Message);
        controls.WindowRead(0);

        speech.Announce(new Announcement("Main menu", Interrupt: false));
        controls.WindowRead(0);

        Assert.Equal(1, stops);
        Assert.Equal("Main menu", output.Said[^1].Text);
    }

    [Fact]
    public void ALineRequestedAsTheWindowClosesIsLeftToPlay()
    {
        dialogue.Show("Ada", "One.");
        controls.WindowRead(Message);
        dialogue.Show("Ada", "Two."); // The next line, before the pad read that sees the old window close.

        controls.WindowRead(0);

        Assert.Equal(0, stops);
    }

    [Fact]
    public void ARepeatedLineIsStoppedToo()
    {
        dialogue.Show("Ada", "One.");
        controls.WindowRead(Message);
        controls.Pad(0, 0);
        controls.Pad(0, DialogueBindings.Default.RepeatButtons);
        controls.WindowRead(Message);

        controls.WindowRead(0);

        Assert.Equal(1, stops);
    }

    [Fact]
    public void TheReaderSwitchedOffLeavesNothingToStop()
    {
        dialogue.Show("Ada", "One.");
        controls.WindowRead(Message);
        reader.Toggle(); // Says "Dialogue reader off", which isn't the window's.
        dialogue.Show("Ada", "Two.");
        controls.WindowRead(Message);

        controls.WindowRead(0);

        Assert.Equal(0, stops);
    }

    [Fact]
    public void AWindowThatWasNeverReadOpenStopsNothing()
    {
        dialogue.Show("Ada", "One.");

        controls.WindowRead(0);
        controls.WindowRead(null);

        Assert.Equal(0, stops);
    }
}
