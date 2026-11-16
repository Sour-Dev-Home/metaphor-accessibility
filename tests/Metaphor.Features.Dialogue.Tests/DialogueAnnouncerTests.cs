using Metaphor.Core.Announcements;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>Spoken dialogue (#52): what the player hears when a line appears, checked through the recording announcer.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueAnnouncerTests
{
    private readonly RecordingAnnouncer announcer = new();

    private DialogueAnnouncer Dialogue() => new(announcer, new TextCleaner(TextCleanerRules.None));

    [Fact]
    public void ALineWithASpeakerIsSpokenAsNameThenLineAndInterrupts()
    {
        Dialogue().Show("Ada", "The bridge is out.");

        Assert.Equal([new Announcement("Ada: The bridge is out.", Interrupt: true)], announcer.Said);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ALineWithoutASpeakerIsSpokenAlone(string? speaker)
    {
        Dialogue().Show(speaker, "The wind picks up.");

        Assert.Equal([new Announcement("The wind picks up.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ASpeakerMadeOnlyOfTagsIsLeftOut()
    {
        Dialogue().Show("<PORTRAIT 4>", "The wind picks up.");

        Assert.Equal([new Announcement("The wind picks up.", Interrupt: true)], announcer.Said);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n ")]
    [InlineData("<PAUSE>")]
    public void AnEmptyLineSaysNothing(string line)
    {
        Dialogue().Show("Ada", line);

        Assert.Empty(announcer.Said);
    }
}
