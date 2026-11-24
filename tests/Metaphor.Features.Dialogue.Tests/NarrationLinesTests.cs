using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>Kind 2 lines (#908): the day count and arrival narration, as the session of 2026-11-21 logged them
/// (<c>MsgWindowShowLine kind=2</c> at 460014, 484850, 496535, 497199 and 497479 ms; hashes resolved to labels in
/// docs/findings/chapter1-silent-text-audit-logged.md). Message text is made up, keyed by the real label.</summary>
[Trait("Category", "Unit")]
public sealed class NarrationLinesTests
{
    private const string Files =
        "@Daily_Message_00_7_00\n{\n<NS>More<NE>\n<VOICE \"v_co_51307_c051\">\nOne day remains.\n<WAIT>\n}\n" +
        "@Daily_Message_00_8\n{\n<NS>More<NE>\nThe carriage arrives.\n<WAIT>\n}\n" +
        "@MES_Narration_Arr_Gra_Fort_00\n{\n<NS>More<NE>\nThe Northern Border Fort.\n<WAIT>\n}\n" +
        "@MES_Narration_Arr_Gra_Fort_01\n{\n<NS>More<NE>\nA tower.\n<WAIT>\n}\n" +
        "@MES_GAMEOVER_000_000\n{\n<NS>More<NE>\nFantasy is dead.\n<WAIT>\n}\n" +
        "@MES_a\n{\n<NS>Ada<NE>\nThe bridge is out.\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();
    private readonly FakeClock clock = new();
    private bool narrationOn = true;

    public NarrationLinesTests() => messages.Compiled(Files);

    private DialogueLines Lines() =>
        new(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), new DialogueHistory()),
            clock, () => narrationOn);

    private static LineShown Shown(string label) => new(2, MessageLibrary.LabelHash(label));

    [Fact]
    public void TheDayCountAndNarrationAreSpokenInOrderWithNoSpeakerAndQueued()
    {
        var lines = Lines();

        lines.Observe(Shown("Daily_Message_00_7_00"));
        clock.Advance(TimeSpan.FromMilliseconds(24_836));
        lines.Observe(Shown("Daily_Message_00_8"));
        clock.Advance(TimeSpan.FromMilliseconds(11_685));
        lines.Observe(Shown("MES_Narration_Arr_Gra_Fort_00"));
        clock.Advance(TimeSpan.FromMilliseconds(664));
        lines.Observe(Shown("MES_Narration_Arr_Gra_Fort_01"));

        Assert.Equal(
            [
                new Announcement("One day remains.", Interrupt: false),
                new Announcement("The carriage arrives.", Interrupt: false),
                new Announcement("The Northern Border Fort.", Interrupt: false),
                new Announcement("A tower.", Interrupt: false),
            ],
            announcer.Said);
    }

    [Fact]
    public void WithTheSettingOffKindTwoStaysSilent()
    {
        narrationOn = false;

        Lines().Observe(Shown("Daily_Message_00_7_00"));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AGameOverLineIsLeftToTheGameOverScreen()
    {
        Lines().Observe(Shown("MES_GAMEOVER_000_000"));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ADialogueLineStillInterruptsAfterNarration()
    {
        var lines = Lines();

        lines.Observe(Shown("Daily_Message_00_8"));
        lines.Observe(new LineShown(0, MessageLibrary.LabelHash("MES_a")));

        Assert.Equal([false, true], announcer.Said.Select(said => said.Interrupt));
        Assert.Equal("Ada: The bridge is out.", announcer.Said[1].Text);
    }
}
