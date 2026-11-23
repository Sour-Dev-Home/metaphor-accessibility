using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Reading the settings file (#104): anything unusable falls back to the defaults, with a log line.</summary>
[Trait("Category", "Unit")]
public sealed class ModSettingsTests
{
    private readonly List<string> logged = [];

    private ModSettings Parse(string? json) => ModSettings.Parse(json, SampleSettings.Catalogue, logged.Add);

    [Fact]
    public void TheBracketKeysAreNamedByThemselves()
    {
        Assert.Equal(0xDB, BindingNames.Key("["));
        Assert.Equal(0xDD, BindingNames.Key("]"));
    }

    [Fact]
    public void NoFileMeansDefaults()
    {
        Assert.Equal(VoicedLinesMode.Auto, Parse(null).VoicedLines);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("auto", VoicedLinesMode.Auto)]
    [InlineData("nameOnly", VoicedLinesMode.NameOnly)]
    [InlineData("NAMEONLY", VoicedLinesMode.NameOnly)]
    [InlineData("fullLine", VoicedLinesMode.FullLine)]
    [InlineData("nothing", VoicedLinesMode.Nothing)]
    public void EachModeIsRead(string value, VoicedLinesMode expected)
    {
        Assert.Equal(expected, Parse($$"""{ "voicedLines": "{{value}}" }""").VoicedLines);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("{ \"voicedLines\": \"loud\" }")]
    [InlineData("{ \"voicedLines\": 3 }")]
    [InlineData("{ \"voicedLines\": null }")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("null")]
    // Found by the property tests (#443): a number in a string, or a list of names, parsed as a mode.
    [InlineData("{ \"voicedLines\": \"3\" }")]
    [InlineData("{ \"voicedLines\": \"Auto, NameOnly\" }")]
    [InlineData("{ \"voicedLines\": \" nameOnly\" }")]
    // Found by the property tests (#443): an escaped lone surrogate threw instead of falling back.
    [InlineData("{ \"voicedLines\": \"\\uD800\" }")]
    public void AnythingInvalidFallsBackToDefaultsAndIsLogged(string json)
    {
        Assert.Equal(VoicedLinesMode.Auto, Parse(json).VoicedLines);
        Assert.Single(logged);
    }

    // Found by the property tests (#443): text that isn't valid UTF-16 threw instead of falling back. A fact, not inline
    // data, so the lone surrogate reaches the parser as written.
    [Fact]
    public void TextThatIsNotValidUtf16FallsBackToDefaultsAndIsLogged()
    {
        Assert.Equal(ModSettings.Defaults, Parse("{ \"voicedLines\": \"\uD800\" }"));
        Assert.Contains("is not valid JSON", Assert.Single(logged), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"voicedLines\": \"loud\" }", "\"voicedLines\" is not one of auto, nameOnly, fullLine, nothing; using auto")]
    [InlineData("{ not json", "is not valid JSON")]
    [InlineData("[]", "is not a JSON object; using defaults")]
    public void TheLogSaysWhatWasWrong(string json, string reason)
    {
        Parse(json);

        Assert.Contains(reason, Assert.Single(logged), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingKeyIsTheDefault()
    {
        Assert.Equal(VoicedLinesMode.Auto, Parse("{}").VoicedLines);
        Assert.Empty(logged);
    }

    // People edit the file by hand.
    [Fact]
    public void CommentsAndTrailingCommasAreAllowed()
    {
        var json = """
            {
              // what the reader speaks
              "voicedLines": "nameOnly",
            }
            """;

        Assert.Equal(VoicedLinesMode.NameOnly, Parse(json).VoicedLines);
        Assert.Empty(logged);
    }

    [Fact]
    public void AMissingLogIsRefused()
    {
        Assert.Throws<ArgumentNullException>("log", () => ModSettings.Parse(null, SampleSettings.Catalogue, null!));
        Assert.Throws<ArgumentNullException>("features", () => ModSettings.Parse(null, null!, logged.Add));
    }

    [Fact]
    public void SoundCuesAreOnAtAModerateVolumeByDefault()
    {
        Assert.True(Parse("{}").SoundCues);
        Assert.Equal(50, Parse("{}").SoundCueVolume);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("""{ "soundCues": false }""", false, 50)]
    [InlineData("""{ "soundCues": true, "soundCueVolume": 0 }""", true, 0)]
    [InlineData("""{ "soundCueVolume": 100 }""", true, 100)]
    [InlineData("""{ "soundCueVolume": 35 }""", true, 35)]
    public void TheSoundCueSettingsAreRead(string json, bool on, int volume)
    {
        var settings = Parse(json);

        Assert.Equal(on, settings.SoundCues);
        Assert.Equal(volume, settings.SoundCueVolume);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("""{ "soundCues": "off" }""", "settings file: \"soundCues\" is not true or false; using true")]
    [InlineData("""{ "soundCues": 0 }""", "settings file: \"soundCues\" is not true or false; using true")]
    [InlineData("""{ "soundCueVolume": 101 }""", "settings file: \"soundCueVolume\" is not a whole number from 0 to 100; using 50")]
    [InlineData("""{ "soundCueVolume": -1 }""", "settings file: \"soundCueVolume\" is not a whole number from 0 to 100; using 50")]
    [InlineData("""{ "soundCueVolume": 40.5 }""", "settings file: \"soundCueVolume\" is not a whole number from 0 to 100; using 50")]
    [InlineData("""{ "soundCueVolume": "40" }""", "settings file: \"soundCueVolume\" is not a whole number from 0 to 100; using 50")]
    public void AnUnusableSoundCueSettingIsItsDefaultAndLogged(string json, string reason)
    {
        var settings = Parse(json);

        Assert.True(settings.SoundCues);
        Assert.Equal(50, settings.SoundCueVolume);
        Assert.Equal([reason], logged);
    }

    // #58's key bindings and #115's braille switches will share the file.
    [Fact]
    public void UnknownKeysAreIgnored()
    {
        Assert.Equal(VoicedLinesMode.Nothing, Parse("""{ "future": 1, "voicedLines": "nothing" }""").VoicedLines);
    }

    // The update check is on unless the file turns it off (#576, ADR 0018).
    [Fact]
    public void TheUpdateCheckIsOnByDefault()
    {
        Assert.True(ModSettings.Defaults.CheckForUpdates);
        Assert.True(Parse("{}").CheckForUpdates);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void TheUpdateCheckIsRead(string value, bool expected)
    {
        var settings = Parse($$"""{ "voicedLines": "nothing", "checkForUpdates": {{value}} }""");

        Assert.Equal(expected, settings.CheckForUpdates);
        Assert.Equal(VoicedLinesMode.Nothing, settings.VoicedLines);
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("0")]
    [InlineData("null")]
    public void AnUpdateCheckValueThatIsNotTrueOrFalseLeavesItOnAndIsLogged(string value)
    {
        var settings = Parse($$"""{ "voicedLines": "nothing", "checkForUpdates": {{value}} }""");

        Assert.True(settings.CheckForUpdates);
        Assert.Equal(VoicedLinesMode.Nothing, settings.VoicedLines);
        Assert.Contains("\"checkForUpdates\" is not true or false; using true", Assert.Single(logged), StringComparison.Ordinal);
    }
}
