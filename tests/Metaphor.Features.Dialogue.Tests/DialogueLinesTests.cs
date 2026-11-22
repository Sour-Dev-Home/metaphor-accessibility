using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game asks for a line by its label (#56): what the player hears, checked through the recording
/// announcer.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueLinesTests
{
    private const string Scene = "@MES_a\n{\n<NS>Ada<NE>\n<VOICE \"v_e01_001_000_00_c051\">\nThe bridge is out.\n<WAIT>\n}";

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

    // #526: some event scenes put lines in the window without asking for them by label; the window's call carries the
    // label's MD5 (docs/raw-sources/msg-window-show-line-2026-11-20.md).
    [Fact]
    public void ALineShownWithoutARequestIsSpoken()
    {
        messages.Compiled(Scene);

        Lines().Observe(new LineShown(0, MessageLibrary.LabelHash("MES_a")));

        Assert.Equal([new Announcement("Ada: The bridge is out.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ARequestedLineIsSpokenOnceWhenTheWindowShowsIt()
    {
        messages.Compiled(Scene);
        var lines = Lines();

        lines.Observe(new LineRequested(0, "MES_a"));
        lines.Observe(new LineShown(0, MessageLibrary.LabelHash("MES_a")));

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void TheSameLineShownAgainWithoutARequestIsSpokenAgain()
    {
        messages.Compiled(Scene);
        var lines = Lines();

        lines.Observe(new LineRequested(0, "MES_a"));
        lines.Observe(new LineShown(0, MessageLibrary.LabelHash("MES_a")));
        lines.Observe(new LineShown(0, MessageLibrary.LabelHash("MES_a")));

        Assert.Equal(2, announcer.Said.Count);
    }

    // #622: the save screen's "Save complete" pop-up is a system message (kind 1) the window shows with no request
    // (docs/raw-sources/save-complete-popup-session-2026-11-22.md).
    [Fact]
    public void ASystemMessageShownIsSpoken()
    {
        messages.Compiled("@SYS_a\n{\nAll done.\n<WAIT>\n}");

        Lines().Observe(new LineShown(1, MessageLibrary.LabelHash("SYS_a")));

        Assert.Equal([new Announcement("All done.", Interrupt: true)], announcer.Said);
    }

    // Kind 2 (subtitles and narration) stays silent: what it shows is unverified.
    [Fact]
    public void ASubtitleShownSaysNothing()
    {
        messages.Compiled(Scene);

        Lines().Observe(new LineShown(2, MessageLibrary.LabelHash("MES_a")));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AShownHashNoCompiledFileHasSaysNothing()
    {
        Lines().Observe(new LineShown(0, MessageLibrary.LabelHash("MES_missing")));

        Assert.Empty(announcer.Said);
    }
}
