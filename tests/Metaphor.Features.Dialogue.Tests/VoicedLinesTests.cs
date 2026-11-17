using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>What is said for a voiced line under each "Voiced lines" mode (#57, #104). A line is voiced when its
/// message carries a VOICE tag (docs/raw-sources/spike-session-1-2026-11-16.md).</summary>
[Trait("Category", "Unit")]
public sealed class VoicedLinesTests
{
    private const string Voiced = "<VOICE \"v_a\">\nThe bridge is out.";

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueHistory history = new();

    private DialogueAnnouncer Dialogue(VoicedLinesMode mode, VoiceLanguage language = VoiceLanguage.Unknown) =>
        new(announcer, new TextCleaner(TextCleanerRules.None), history, () => mode, () => language);

    private string? Said() => announcer.Said.SingleOrDefault()?.Text;

    [Theory]
    [InlineData(VoicedLinesMode.Auto)]
    [InlineData(VoicedLinesMode.NameOnly)]
    [InlineData(VoicedLinesMode.FullLine)]
    [InlineData(VoicedLinesMode.Nothing)]
    public void AnUnvoicedLineIsAlwaysSpokenInFull(VoicedLinesMode mode)
    {
        Dialogue(mode).Show("Ada", "The bridge is out.");

        Assert.Equal("Ada: The bridge is out.", Said());
    }

    [Fact]
    public void NameOnlySaysJustTheName()
    {
        Dialogue(VoicedLinesMode.NameOnly).Show("Ada", Voiced);

        Assert.Equal("Ada", Said());
    }

    [Fact]
    public void NameOnlyIsSilentWithoutASpeaker()
    {
        Dialogue(VoicedLinesMode.NameOnly).Show(null, Voiced);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void FullLineSaysEverything()
    {
        Dialogue(VoicedLinesMode.FullLine).Show("Ada", Voiced);

        Assert.Equal("Ada: The bridge is out.", Said());
    }

    [Fact]
    public void NothingIsSilent()
    {
        Dialogue(VoicedLinesMode.Nothing).Show("Ada", Voiced);

        Assert.Empty(announcer.Said);
    }

    [Theory]
    [InlineData(VoiceLanguage.Unknown, "Ada: The bridge is out.")]
    [InlineData(VoiceLanguage.Japanese, "Ada: The bridge is out.")]
    [InlineData(VoiceLanguage.English, "Ada")]
    public void AutoDependsOnTheVoiceLanguage(VoiceLanguage language, string expected)
    {
        Dialogue(VoicedLinesMode.Auto, language).Show("Ada", Voiced);

        Assert.Equal(expected, Said());
    }

    [Fact]
    public void ModeIsReadOnEveryLineSoAChangeAppliesAtOnce()
    {
        var mode = VoicedLinesMode.FullLine;
        var dialogue = new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), history, () => mode);

        dialogue.Show("Ada", Voiced);
        mode = VoicedLinesMode.Nothing;
        dialogue.Show("Ada", Voiced);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void WithoutSettingsEveryLineIsSpokenInFull()
    {
        new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), history).Show("Ada", Voiced);

        Assert.Equal("Ada: The bridge is out.", Said());
    }

    [Theory]
    [InlineData(VoicedLinesMode.NameOnly)]
    [InlineData(VoicedLinesMode.Nothing)]
    public void TheHistoryKeepsTheFullLineWhateverIsSpoken(VoicedLinesMode mode)
    {
        Dialogue(mode).Show("Ada", Voiced);

        Assert.Equal(["Ada: The bridge is out."], history.Lines());
    }
}
