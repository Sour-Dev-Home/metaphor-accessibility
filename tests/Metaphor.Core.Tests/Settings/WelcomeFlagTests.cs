using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The first-launch welcome's flag (#617): <c>"welcomed": true</c> in the settings file once the welcome is
/// said, written into the player's own file without losing what they wrote.</summary>
[Trait("Category", "Unit")]
public sealed class WelcomeFlagTests
{
    private readonly List<string> logged = [];

    private ModSettings Parse(string? json) => ModSettings.Parse(json, SampleSettings.Catalogue, logged.Add);

    [Fact]
    public void WithNoFileTheWelcomeIsStillToSay() => Assert.False(Parse(null).Welcomed);

    [Theory]
    [InlineData("""{ "welcomed": true }""", true)]
    [InlineData("""{ "welcomed": false }""", false)]
    [InlineData("""{ "voicedLines": "auto" }""", false)]
    public void TheFlagIsReadFromTheFile(string json, bool welcomed) => Assert.Equal(welcomed, Parse(json).Welcomed);

    [Fact]
    public void AnUnusableFlagCountsAsWelcomedWithALogLine()
    {
        // A typo shouldn't make the welcome repeat on every launch.
        Assert.True(Parse("""{ "welcomed": "yes" }""").Welcomed);
        Assert.Contains(logged, line => line.Contains("\"welcomed\"", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkingNoFileWritesAFileWithOnlyTheFlag()
    {
        var marked = WelcomeFlag.Mark(null);

        Assert.NotNull(marked);
        Assert.True(Parse(marked).Welcomed);
    }

    [Fact]
    public void MarkingKeepsEverySettingAndCommentThePlayerWrote()
    {
        const string File = """
            // my settings
            {
              "speech": { "menus": false },
              "keys": { "dialogue": { "repeatKey": "Q" } }
            }
            """;

        var marked = WelcomeFlag.Mark(File)!;

        Assert.Contains("// my settings", marked, StringComparison.Ordinal);
        var settings = Parse(marked);
        Assert.True(settings.Welcomed);
        Assert.False(settings.IsOn(SampleFeature.Menus));
        Assert.Equal(0x51, settings.Binding("dialogue", "repeatKey", 0));
        Assert.Empty(logged);
    }

    [Fact]
    public void MarkingAnEmptyObjectGivesValidJson() => Assert.True(Parse(WelcomeFlag.Mark("{}")).Welcomed);

    [Fact]
    public void MarkingAFileThatSaysFalseTurnsItTrue()
    {
        var marked = WelcomeFlag.Mark("""{ "welcomed" : false, "soundCues": false }""")!;

        var settings = Parse(marked);
        Assert.True(settings.Welcomed);
        Assert.False(settings.SoundCues);
    }

    [Fact]
    public void AFileAlreadyMarkedIsLeftAlone() => Assert.Null(WelcomeFlag.Mark("""{ "welcomed": true }"""));

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"speech\": ")]
    public void AFileThatIsNotAJsonObjectIsLeftAlone(string json) => Assert.Null(WelcomeFlag.Mark(json));
}
