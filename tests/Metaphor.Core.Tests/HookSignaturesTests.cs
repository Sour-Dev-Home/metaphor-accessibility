namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed class HookSignaturesTests
{
    private const string Sha = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private static string Entry(string name = "draw-text", string pattern = "48 8B ?? 10", string builds = $"\"{Sha}\"",
        string evidence = "e", string section = ".ecode") =>
        $$"""{ "name": "{{name}}", "pattern": "{{pattern}}", "gameBuilds": [{{builds}}], "evidence": "{{evidence}}", "section": "{{section}}" }""";

    private static string ParseError(string json) =>
        Assert.Throws<InvalidDataException>(() => HookSignatures.Parse(json)).Message;

    [Fact]
    public void ReadsASignature()
    {
        var signature = Assert.Single(HookSignatures.Parse($"[{Entry()}]"));

        Assert.Equal("draw-text", signature.Name);
        Assert.Equal("48 8B ?? 10", signature.Pattern);
        Assert.Equal([Sha], signature.GameBuilds);
        Assert.Equal("e", signature.Evidence);
        Assert.Equal(".ecode", signature.Section);
    }

    /// <summary>#167: the section says whether a hook on it must be opt-in (ADR 0010).</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ecode")]
    public void EachSignatureNamesTheSectionItIsIn(string section) =>
        Assert.Contains("section", ParseError($"[{Entry(section: section)}]"), StringComparison.Ordinal);

    [Fact]
    public void EverySignatureWeShipNamesItsSection() =>
        Assert.All(HookSignatures.All, signature => Assert.StartsWith(".", signature.Section, StringComparison.Ordinal));

    [Theory]
    [InlineData("48 8G")]
    [InlineData("488B")]
    [InlineData("?? ??")]
    [InlineData("")]
    public void RejectsAPatternThatIsNotHexBytesAndWildcards(string pattern) =>
        Assert.Contains("draw-text", ParseError($"[{Entry(pattern: pattern)}]"), StringComparison.Ordinal);

    [Fact]
    public void RejectsABuildThatIsNotALowercaseSha256() =>
        Assert.Contains("SHA-256", ParseError($"[{Entry(builds: "\"548DDC\"")}]"), StringComparison.Ordinal);

    [Fact]
    public void EachSignatureNamesABuild() =>
        Assert.Contains("game build", ParseError($"[{Entry(builds: "")}]"), StringComparison.Ordinal);

    [Fact]
    public void EachSignatureNeedsEvidence() =>
        Assert.Contains("evidence", ParseError($"[{Entry(evidence: " ")}]"), StringComparison.Ordinal);

    [Fact]
    public void EachSignatureNeedsAName() =>
        Assert.Contains("name", ParseError($"[{Entry(name: "")}]"), StringComparison.Ordinal);

    [Fact]
    public void RejectsANameUsedTwice() =>
        Assert.Contains("draw-text", ParseError($"[{Entry()}, {Entry()}]"), StringComparison.Ordinal);

    [Fact]
    public void RejectsMalformedJson() =>
        Assert.Contains("malformed", ParseError("[{"), StringComparison.Ordinal);

    [Fact]
    public void TheShippedSignaturesNameOnlyKnownBuilds()
    {
        var known = KnownGameBuilds.All.Select(build => build.Sha256).ToHashSet();

        Assert.All(HookSignatures.All, signature => Assert.Subset(known, signature.GameBuilds.ToHashSet()));
    }
}
