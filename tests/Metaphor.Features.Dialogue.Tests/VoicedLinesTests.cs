using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>What is said for a voiced line under each "Voiced lines" mode (#57, #104). A line is voiced when its
/// message carries a VOICE tag (docs/raw-sources/spike-session-1-2026-11-16.md) whose clip is a full reading (#551).</summary>
[Trait("Category", "Unit")]
public sealed class VoicedLinesTests
{
    private const string Voiced = "<VOICE \"v_e01_001_000_00_c051\">\nThe bridge is out.";

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

    // Clip families from docs/findings/voice-clip-families.md (#551).
    [Theory]
    [InlineData("v_e00_002_001_000_00_c051")]
    [InlineData("v_f01_003_000_00_c051")]
    [InlineData("v_BE_0001")]
    [InlineData("v_du_0001")]
    [InlineData("v_br_0001")]
    [InlineData("v_tr_nar_0001")]
    public void AFullReadingClipIsVoiced(string clip)
    {
        Dialogue(VoicedLinesMode.NameOnly).Show("Ada", $"<VOICE \"{clip}\">\nThe bridge is out.");

        Assert.Equal("Ada", Said());
    }

    [Theory]
    [InlineData("v_co_Dpv_PC_Gallica_talk_26_c011", "Wait! I'm sensing something close! Don't move!")]
    [InlineData("v_co_Dpv_PC_Gallica_talk_19_c011", "Yeah, those look especially dangerous.")]
    [InlineData("v_co_Dpv_PC_Gallica_talk_31_c011", "Hey, look!")]
    [InlineData("v_co_11193_c011", "Almost there. Let's keep moving.")]
    [InlineData("v_co_1090_c001", "It's as though music is playing directly in my mind.")]
    [InlineData("v_en_0001", "Crowd chatter.")]
    [InlineData("v_fm_0001", "A line.")]
    [InlineData("v_tr_travel_voice_01", "A line.")]
    [InlineData("v_zz_unknown", "A line.")]
    [InlineData("", "A line.")]
    public void AGenericClipReadsTheText(string clip, string text)
    {
        Dialogue(VoicedLinesMode.NameOnly).Show("Gallica", $"<VOICE \"{clip}\">\n{text}");

        Assert.Equal($"Gallica: {text}", Said());
    }

    [Fact]
    public void AGenericClipFollowsTheSpeakerChangeRule()
    {
        var dialogue = Dialogue(VoicedLinesMode.NameOnly);
        dialogue.Show("Gallica", "<VOICE \"v_co_Dpv_PC_Gallica_talk_01_c011\">\nHey, look!");
        dialogue.Show("Gallica", "<VOICE \"v_co_Dpv_PC_Gallica_talk_02_c011\">\nOver there!");

        Assert.Equal(["Gallica: Hey, look!", "Over there!"], announcer.Said.Select(a => a.Text));
    }
}
