namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed class KnownGameBuildsTests
{
    private const string Sha = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const string OtherSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ReadsABuildWithItsLabels()
    {
        var builds = KnownGameBuilds.Parse(
            $$"""[{ "sha256": "{{Sha}}", "steamBuildId": 18330018, "gameVersion": "1.0.0", "evidence": "e" }]""");

        var build = Assert.Single(builds);
        Assert.Equal(new GameBuild(Sha, 18330018, "1.0.0", "e"), build);
    }

    [Fact]
    public void TheGameVersionMayBeUnrecorded()
    {
        var builds = KnownGameBuilds.Parse($$"""[{ "sha256": "{{Sha}}", "steamBuildId": 1, "evidence": "e" }]""");

        Assert.Null(Assert.Single(builds).GameVersion);
    }

    [Theory]
    [InlineData("548DDC955C176867F062C44F94C03DD9AC33CAEFB278A5AC388E792AEDF09504")]
    [InlineData("548ddc95")]
    [InlineData("")]
    public void TheIdentifierMustBeALowercaseSha256(string sha)
    {
        var json = $$"""[{ "sha256": "{{sha}}", "steamBuildId": 1, "evidence": "e" }]""";

        var error = Assert.Throws<InvalidDataException>(() => KnownGameBuilds.Parse(json));
        Assert.Contains("SHA-256", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachBuildNeedsEvidence()
    {
        var json = $$"""[{ "sha256": "{{Sha}}", "steamBuildId": 1, "evidence": " " }]""";

        var error = Assert.Throws<InvalidDataException>(() => KnownGameBuilds.Parse(json));
        Assert.Contains("evidence", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(""", "steamBuildId": 0""")]
    public void EachBuildNeedsASteamBuildId(string steamBuildId)
    {
        var json = $$"""[{ "sha256": "{{Sha}}"{{steamBuildId}}, "evidence": "e" }]""";

        var error = Assert.Throws<InvalidDataException>(() => KnownGameBuilds.Parse(json));
        Assert.Contains("Steam build ID", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyEntryIsMalformed() =>
        Assert.Throws<InvalidDataException>(() => KnownGameBuilds.Parse("[null]"));

    [Fact]
    public void ABuildIsListedOnce()
    {
        var entry = $$"""{ "sha256": "{{Sha}}", "steamBuildId": 1, "evidence": "e" }""";
        var other = $$"""{ "sha256": "{{OtherSha}}", "steamBuildId": 2, "evidence": "e" }""";

        var error = Assert.Throws<InvalidDataException>(() => KnownGameBuilds.Parse($"[{entry}, {other}, {entry}]"));
        Assert.Contains(Sha, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShippedListIsValid() => Assert.Contains(KnownGameBuilds.All, build => build.Sha256 == Sha);
}
