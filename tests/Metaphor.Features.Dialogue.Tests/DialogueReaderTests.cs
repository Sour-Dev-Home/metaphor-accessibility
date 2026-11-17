using Metaphor.Core.Announcements;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The reader on/off switch (#58): spoken dialogue passes through while it's on and is dropped while it's off.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DialogueReaderTests
{
    private readonly RecordingAnnouncer announcer = new();

    [Fact]
    public void ItStartsOnAndPassesDialogueThrough()
    {
        var reader = new DialogueReader(announcer);

        reader.Announce(new Announcement("Ada: One.", Interrupt: true));

        Assert.True(reader.On);
        Assert.Equal([new Announcement("Ada: One.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void WhileOffDialogueIsDropped()
    {
        var reader = new DialogueReader(announcer);
        reader.Toggle();

        reader.Announce(new Announcement("Ada: One.", Interrupt: true));

        Assert.Equal([new Announcement("Dialogue reader off", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TogglingTwiceTurnsItBackOnAndSaysEachState()
    {
        var reader = new DialogueReader(announcer);

        reader.Toggle();
        reader.Toggle();

        Assert.True(reader.On);
        Assert.Equal(
            [new Announcement("Dialogue reader off", Interrupt: true), new Announcement("Dialogue reader on", Interrupt: true)],
            announcer.Said);
    }
}
