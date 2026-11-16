using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game asks for a line by its label (#56): what the player hears, checked through the recording
/// announcer.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueLinesTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();

    private DialogueLines Lines() =>
        new(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None)));

    [Fact]
    public void ARequestedLineIsSpokenWithItsSpeaker()
    {
        messages.Compiled("@MES_a\n{\n<NS>Ada<NE>\n<VOICE \"v_a\">\nThe bridge is out.\n<WAIT>\n}");

        Lines().Requested("MES_a");

        Assert.Equal([new Announcement("Ada: The bridge is out.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ALabelNoCompiledFileHasSaysNothing()
    {
        Lines().Requested("MES_missing");

        Assert.Empty(announcer.Said);
    }
}
