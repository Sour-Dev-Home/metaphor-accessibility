namespace Metaphor.Features.Updates.Tests;

/// <summary>Release versions in semantic-version order (#576), pre-releases included, as semver.org 2.0.0 section 11
/// orders them.</summary>
[Trait("Category", "Unit")]
public sealed class ReleaseVersionTests
{
    private static ReleaseVersion Version(string text) => ReleaseVersion.Parse(text) ?? throw new ArgumentException(text);

    // Each older than the next: semver.org's own example list, then this project's release tags.
    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta")]
    [InlineData("1.0.0-beta", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("2.0.0", "2.1.0")]
    [InlineData("2.1.0", "2.1.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("v0.0.1-unstable.2", "v0.0.1-unstable.3")]
    [InlineData("v0.0.1-unstable.9", "v0.0.1-unstable.10")]
    [InlineData("v0.0.1-unstable.3", "0.0.1")]
    [InlineData("0.0.1", "0.1.0-unstable.1")]
    [InlineData("1.0.0-99999999999999999999", "1.0.0-100000000000000000000")]
    public void TheSecondIsNewer(string older, string newer)
    {
        Assert.True(Version(newer).IsNewerThan(Version(older)));
        Assert.False(Version(older).IsNewerThan(Version(newer)));
    }

    [Theory]
    [InlineData("1.2.3", "v1.2.3")]
    [InlineData("1.2.3", "1.2.3+build.5")]
    [InlineData("0.0.1-unstable.2", "V0.0.1-unstable.2")]
    public void TheSameVersionIsNotNewer(string one, string other)
    {
        Assert.False(Version(one).IsNewerThan(Version(other)));
        Assert.False(Version(other).IsNewerThan(Version(one)));
    }

    [Theory]
    [InlineData("v0.0.1-unstable.2", "0.0.1-unstable.2")]
    [InlineData("1.2.3+build.5", "1.2.3")]
    public void TheTextDropsTheVAndTheBuild(string tag, string text)
    {
        Assert.Equal(text, Version(tag).Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-a..b")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-a_b")]
    [InlineData("1.2.3+")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    [InlineData("vv1.2.3")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.99999999999")]
    [InlineData("１.2.3")]
    public void AnythingElseIsNotAVersion(string? text)
    {
        Assert.Null(ReleaseVersion.Parse(text));
    }
}
