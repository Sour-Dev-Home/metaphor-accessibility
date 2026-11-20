using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The speaker's name is said on a conversation's first line and when the speaker changes (#539). A
/// conversation ends when the dialogue window closes.</summary>
[Trait("Category", "Unit")]
public sealed class SpeakerChangeTests
{
    private const long Message = 0x1315afe20;
    private const int X = 0x8000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueHistory history = new();

    private DialogueAnnouncer Dialogue(VoicedLinesMode mode = VoicedLinesMode.FullLine) =>
        new(announcer, new TextCleaner(TextCleanerRules.None), history, () => mode);

    private IReadOnlyList<string> Said => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void TheFirstLineSaysItsSpeaker()
    {
        Dialogue().Show("Ada", "One.");

        Assert.Equal(["Ada: One."], Said);
    }

    [Fact]
    public void ASpeakerContinuingSaysOnlyTheLine()
    {
        var dialogue = Dialogue();
        dialogue.Show("Ada", "One.");
        dialogue.Show("Ada", "Two.");

        Assert.Equal(["Ada: One.", "Two."], Said);
    }

    [Fact]
    public void ASpeakerChangeSaysTheNewName()
    {
        var dialogue = Dialogue();
        dialogue.Show("Ada", "One.");
        dialogue.Show("Bo", "Two.");
        dialogue.Show("Ada", "Three.");

        Assert.Equal(["Ada: One.", "Bo: Two.", "Ada: Three."], Said);
    }

    [Fact]
    public void ANewConversationSaysTheSpeakerAgain()
    {
        var dialogue = Dialogue();
        dialogue.Show("Ada", "One.");
        dialogue.EndConversation();
        dialogue.Show("Ada", "Two.");

        Assert.Equal(["Ada: One.", "Ada: Two."], Said);
    }

    [Fact]
    public void TheHistoryKeepsTheNameOnEveryLine()
    {
        var dialogue = Dialogue();
        dialogue.Show("Ada", "One.");
        dialogue.Show("Ada", "Two.");

        Assert.Equal(["Ada: One.", "Ada: Two."], history.Lines());
    }

    [Fact]
    public void ReplayAlwaysSaysNameAndLine()
    {
        var dialogue = Dialogue();
        var controls = new DialogueControls(DialogueBindings.Default, history, new DialogueReader(announcer), announcer);
        controls.WindowRead(Message);
        dialogue.Show("Ada", "One.");
        dialogue.Show("Ada", "Two.");

        controls.Pad(0, 0);
        controls.Pad(0, X);

        Assert.Equal(["Ada: One.", "Two.", "Ada: Two."], Said);
    }

    [Fact]
    public void ClosingTheWindowEndsTheConversation()
    {
        var dialogue = Dialogue();
        var controls = new DialogueControls(
            DialogueBindings.Default, history, new DialogueReader(announcer), announcer, dialogue.EndConversation);
        controls.WindowRead(Message);
        dialogue.Show("Ada", "One.");
        controls.WindowRead(0);
        controls.WindowRead(Message);
        dialogue.Show("Ada", "Two.");

        Assert.Equal(["Ada: One.", "Ada: Two."], Said);
    }

    [Fact]
    public void NameOnlyVoicedLineWithTheSameSpeakerSaysNothing()
    {
        var dialogue = Dialogue(VoicedLinesMode.NameOnly);
        dialogue.Show("Ada", "<VOICE \"v_1\">\nOne.");
        dialogue.Show("Ada", "<VOICE \"v_2\">\nTwo.");
        dialogue.Show("Bo", "<VOICE \"v_3\">\nThree.");

        Assert.Equal(["Ada", "Bo"], Said);
    }
}
