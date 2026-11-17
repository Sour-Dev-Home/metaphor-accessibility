using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game asks for a line by its label (#56): what the player hears, checked through the recording
/// announcer.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueLinesTests
{
    private const string Scene = "@MES_a\n{\n<NS>Ada<NE>\n<VOICE \"v_a\">\nThe bridge is out.\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();

    private DialogueLines Lines() =>
        new(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), new DialogueHistory()));

    [Fact]
    public void ARequestedLineIsSpokenWithItsSpeaker()
    {
        messages.Compiled(Scene);

        Lines().Observe(new LineRequested(0, "MES_a"));

        Assert.Equal([new Announcement("Ada: The bridge is out.", Interrupt: true)], announcer.Said);
    }

    // Kinds 1 and 2 come from the system message, subtitle and narration wrappers
    // (docs/raw-sources/msg-request-functions-2026-11-16.md); what they show is unverified, so they stay silent.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OnlyKindZeroIsSpoken(int kind)
    {
        messages.Compiled(Scene);

        Lines().Observe(new LineRequested(kind, "MES_a"));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ALabelNoCompiledFileHasSaysNothing()
    {
        Lines().Observe(new LineRequested(0, "MES_missing"));

        Assert.Empty(announcer.Said);
    }
}
