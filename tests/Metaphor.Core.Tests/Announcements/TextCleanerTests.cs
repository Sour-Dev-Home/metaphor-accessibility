using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>The text cleaner's rules, on made-up strings and made-up tags: no game text (#53). Which tags the game
/// really uses is #55's finding; these tests only fix what each kind of rule does.</summary>
[Trait("Category", "Unit")]
public sealed class TextCleanerTests
{
    private static readonly TextCleanerRules Rules = new()
    {
        Placeholders = new Dictionary<string, string> { ["HERO"] = "hero-name" },
        SpokenTags = new Dictionary<string, string> { ["PAD 1"] = "confirm button" },
    };

    private static string Clean(string text, Func<string, string?>? placeholders = null) =>
        new TextCleaner(Rules, placeholders ?? (_ => null)).Clean(text);

    [Fact]
    public void JoinsLinesAndSquashesSpaces() =>
        Assert.Equal("The bridge is out.", Clean("  The bridge\nis   out. "));

    [Fact]
    public void StripsATagItHasNoRuleFor() =>
        Assert.Equal("The bridge is out.", Clean("The <TINT 3>bridge<TINT 0> is out.<PAUSE>"));

    [Fact]
    public void FillsAPlaceholderWithWhatTheScreenShows() =>
        Assert.Equal("Kit waits.", Clean("<HERO> waits.", key => key == "hero-name" ? "Kit" : null));

    [Fact]
    public void DropsAPlaceholderWithNoValue() =>
        Assert.Equal("waits.", Clean("<HERO> waits."));

    [Fact]
    public void SaysAKnownIconAsAWord() =>
        Assert.Equal("Press confirm button to go on.", Clean("Press <PAD 1> to go on."));

    [Fact]
    public void DropsAnUnknownIcon() =>
        Assert.Equal("Press to go on.", Clean("Press <PAD 9> to go on."));

    [Fact]
    public void MatchesATagsArgumentsWhateverTheirSpacing() =>
        Assert.Equal("Press confirm button.", Clean("Press <PAD  1>."));

    [Theory]
    [InlineData("The <TINT 3>bridge</TINT> is out.")]
    [InlineData("The <TINT=3>bridge<TINT=0> is out.")]
    [InlineData("The <TINT:3>bridge<TINT:0> is out.")]
    public void StripsClosingTagsAndOtherArgumentStyles(string text) =>
        Assert.Equal("The bridge is out.", Clean(text));

    [Fact]
    public void APlaceholderFillsWhateverItsArguments() =>
        Assert.Equal("Kit waits.", Clean("<HERO 2> waits.", key => key == "hero-name" ? "Kit" : null));

    [Fact]
    public void APlaceholderIsHandedItsArguments() =>
        Assert.Equal(
            "Kit and Ada wait.",
            new TextCleaner(Rules, (key, args) => key == "hero-name" ? (args == "2" ? "Ada" : "Kit") : null)
                .Clean("<HERO> and <HERO   2> wait."));

    [Fact]
    public void TagNamesMatchOnlyInTheCaseTheRulesUse() =>
        Assert.Equal("waits.", Clean("<hero> waits.", key => key == "hero-name" ? "Kit" : null));

    [Theory]
    [InlineData("3 < 4 and 5 > 2")]
    [InlineData("<3 hearts")]
    public void LeavesAngleBracketsThatArentTags(string text) =>
        Assert.Equal(text, Clean(text));
}
