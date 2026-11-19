using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Reading the settings file (#104): anything unusable falls back to the defaults, with a log line.</summary>
[Trait("Category", "Unit")]
public sealed class ModSettingsTests
{
    private readonly List<string> logged = [];

    private ModSettings Parse(string? json) => ModSettings.Parse(json, logged.Add);

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
    public void AnythingInvalidFallsBackToDefaultsAndIsLogged(string json)
    {
        Assert.Equal(VoicedLinesMode.Auto, Parse(json).VoicedLines);
        Assert.Single(logged);
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
        Assert.Throws<ArgumentNullException>("log", () => ModSettings.Parse(null, null!));
    }

    // #58's key bindings and #115's braille switches will share the file.
    [Fact]
    public void UnknownKeysAreIgnored()
    {
        Assert.Equal(VoicedLinesMode.Nothing, Parse("""{ "future": 1, "voicedLines": "nothing" }""").VoicedLines);
    }
}
