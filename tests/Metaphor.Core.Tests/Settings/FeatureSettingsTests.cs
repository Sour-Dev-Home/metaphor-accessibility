using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>A toggle for every feature (#661): each feature and each cue has its own key in the settings file, on by
/// default, and key bindings are read by name.</summary>
[Trait("Category", "Unit")]
public sealed class FeatureSettingsTests
{
    private readonly List<string> logged = [];

    private ModSettings Parse(string json) => ModSettings.Parse(json, logged.Add);

    [Fact]
    public void EveryFeatureAndCueHasItsOwnKeyAndSpokenName()
    {
        var keys = Enum.GetValues<Feature>().Select(SettingKey.Of)
            .Concat(Enum.GetValues<Cue>().Select(SettingKey.Of)).ToList();

        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key.Spoken)));
        Assert.Equal(keys.Count, keys.Select(key => key.Path).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryFeatureAndCueIsOnByDefault()
    {
        Assert.All(Enum.GetValues<Feature>(), feature => Assert.True(ModSettings.Defaults.IsOn(feature)));
        Assert.All(Enum.GetValues<Cue>(), cue => Assert.Equal(new CueSetting(true, 100), ModSettings.Defaults.Sound(cue)));
    }

    [Theory]
    [InlineData("""{ "speech": { "dialogue": false } }""", Feature.Dialogue)]
    [InlineData("""{ "speech": { "menus": false } }""", Feature.Menus)]
    [InlineData("""{ "speech": { "nameEntry": false } }""", Feature.NameEntry)]
    [InlineData("""{ "speech": { "travelMap": false } }""", Feature.TravelMap)]
    [InlineData("""{ "speech": { "placeNames": false } }""", Feature.PlaceNames)]
    [InlineData("""{ "speech": { "updateNotice": false } }""", Feature.UpdateNotice)]
    [InlineData("""{ "field": { "nearby": false } }""", Feature.Nearby)]
    [InlineData("""{ "field": { "whereAmI": false } }""", Feature.WhereAmI)]
    public void AFeatureTurnsOffAloneFromItsKey(string json, Feature off)
    {
        var settings = Parse(json);

        Assert.All(Enum.GetValues<Feature>(), feature => Assert.Equal(feature != off, settings.IsOn(feature)));
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("""{ "speech": { "dialogue": "off" } }""", "speech.dialogue")]
    [InlineData("""{ "speech": { "dialogue": 0 } }""", "speech.dialogue")]
    [InlineData("""{ "speech": false }""", "speech")]
    public void AnUnusableToggleStaysOnWithALogLine(string json, string named)
    {
        var settings = Parse(json);

        Assert.True(settings.IsOn(Feature.Dialogue));
        Assert.Contains(logged, line => line.Contains($"\"{named}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownKeysInAGroupAreIgnored()
    {
        var settings = Parse("""{ "speech": { "battle": false }, "sounds": { "bell": false }, "keys": { "battle": { "note": 1 } } }""");

        Assert.Equal(ModSettings.Defaults, settings);
        Assert.Empty(logged);
    }

    [Fact]
    public void ACueTurnsOffOrChangesVolumeAlone()
    {
        var settings = Parse("""{ "sounds": { "allyHit": { "on": false }, "enemyHit": { "volume": 30 } } }""");

        Assert.Equal(new CueSetting(false, 100), settings.Sound(Cue.AllyHit));
        Assert.Equal(new CueSetting(true, 30), settings.Sound(Cue.EnemyHit));
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("""{ "sounds": { "allyHit": { "volume": 101 } } }""", "sounds.allyHit.volume")]
    [InlineData("""{ "sounds": { "allyHit": { "on": "no" } } }""", "sounds.allyHit.on")]
    [InlineData("""{ "sounds": { "allyHit": true } }""", "sounds.allyHit")]
    public void AnUnusableCueValueKeepsItsDefaultWithALogLine(string json, string named)
    {
        var settings = Parse(json);

        Assert.Equal(new CueSetting(true, 100), settings.Sound(Cue.AllyHit));
        Assert.Contains(logged, line => line.Contains($"\"{named}\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("R", 0x52)]
    [InlineData("r", 0x52)]
    [InlineData("7", 0x37)]
    [InlineData("F5", 0x74)]
    [InlineData("none", 0)]
    public void AKeyIsReadByName(string name, int code)
    {
        var settings = Parse($$"""{ "keys": { "dialogue": { "repeatKey": "{{name}}" } } }""");

        Assert.Equal(code, settings.Binding("dialogue", "repeatKey", -1));
        Assert.Empty(logged);
    }

    // Bits of the game's pad mask, game build 548ddc95 (Steam build 18330018):
    // docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.
    [Theory]
    [InlineData("X", 0x8000)]
    [InlineData("lb+rb", 0x0C00)]
    [InlineData("LB + DpadDown", 0x0440)]
    [InlineData("none", 0)]
    public void ButtonsAreReadByName(string names, int mask)
    {
        var settings = Parse($$"""{ "keys": { "dialogue": { "readerButtons": "{{names}}" } } }""");

        Assert.Equal(mask, settings.Binding("dialogue", "readerButtons", -1));
        Assert.Empty(logged);
    }

    [Theory]
    [InlineData("repeatKey", "\"Ctrl\"")]
    [InlineData("repeatKey", "82")]
    [InlineData("readerButtons", "\"LB+Z\"")]
    [InlineData("readerButtons", "\"\"")]
    public void AnUnusableBindingFallsBackToTheDefaultWithALogLine(string name, string value)
    {
        var settings = Parse($$"""{ "keys": { "dialogue": { "{{name}}": {{value}} } } }""");

        Assert.Equal(-1, settings.Binding("dialogue", name, -1));
        Assert.Contains(logged, line => line.Contains($"\"keys.dialogue.{name}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ABindingNotInTheFileIsTheDefault()
    {
        Assert.Equal(0x54, ModSettings.Defaults.Binding("dialogue", "readerKey", 0x54));
    }

    [Fact]
    public void TheSameFileReadTwiceGivesEqualSettings()
    {
        const string json = """
            { "speech": { "menus": false }, "sounds": { "allyHit": { "volume": 20 } },
              "keys": { "field": { "nearbyKey": "M" } } }
            """;

        Assert.Equal(Parse(json), Parse(json));
        Assert.NotEqual(ModSettings.Defaults, Parse(json));
    }
}
