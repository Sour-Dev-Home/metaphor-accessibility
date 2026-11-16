namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed class SignatureProbesTests
{
    private static string Entry(string name = "CriFs_InitializeLibrary", string pattern = "49 89 E3 ?? 5B", string source = "s") =>
        $$"""{ "name": "{{name}}", "pattern": "{{pattern}}", "source": "{{source}}" }""";

    private static string ParseError(string json) =>
        Assert.Throws<InvalidDataException>(() => SignatureProbes.Parse(json)).Message;

    [Fact]
    public void ReadsAProbe() =>
        Assert.Equal(
            new SignatureProbe("CriFs_InitializeLibrary", "49 89 E3 ?? 5B", "s"),
            Assert.Single(SignatureProbes.Parse($"[{Entry()}]")));

    [Theory]
    [InlineData("")]
    [InlineData("49 8G")]
    public void RejectsABadPattern(string pattern) =>
        Assert.Contains("CriFs_InitializeLibrary", ParseError($"[{Entry(pattern: pattern)}]"), StringComparison.Ordinal);

    [Fact]
    public void EachProbeCitesItsSource() =>
        Assert.Contains("source", ParseError($"[{Entry(source: "")}]"), StringComparison.Ordinal);

    [Fact]
    public void RejectsANameUsedTwice() =>
        Assert.Contains("listed twice", ParseError($"[{Entry()}, {Entry()}]"), StringComparison.Ordinal);

    [Fact]
    public void TheCommittedProbesAreCriFsReleasePatterns()
    {
        // The dev tool's probes file: the 15 non-empty Release patterns in
        // docs/raw-sources/crifs-v2-hook-metaphor-patterns-2026-11-15.md. It is never built into the mod.
        var probes = CommittedProbes("signature-probes.json");

        Assert.Equal(15, probes.Count);
        Assert.Contains(probes, probe =>
            probe is { Name: "CriFs_InitializeLibrary", Pattern: "49 89 E3 49 89 5B ?? 49 89 6B ?? 49 89 7B" });
        Assert.All(probes, probe =>
            Assert.Equal("docs/raw-sources/crifs-v2-hook-metaphor-patterns-2026-11-15.md", probe.Source));
    }

    [Fact]
    public void TheCommittedMessageProbesAreMfToolkitsMessageSignatures()
    {
        // For #55: the six message-function signatures in docs/raw-sources/game-text-hooks-community-2026-11-15.md.
        var probes = CommittedProbes("mf-toolkit-message-probes.json");

        Assert.Equal(6, probes.Count);
        Assert.Contains(probes, probe => probe is
        {
            Name: "GetMessageByLabelIndex",
            Pattern: "48 89 5C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 55 41 56 41 57 48 8B EC 48 81 EC 80 00 00 00 49 8B F1",
        });
        Assert.All(probes, probe =>
            Assert.Equal("docs/raw-sources/game-text-hooks-community-2026-11-15.md", probe.Source));
    }

    private static IReadOnlyList<SignatureProbe> CommittedProbes(string fileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        return SignatureProbes.Parse(File.ReadAllText(Path.Combine(root, "tools/Metaphor.SignatureCheck", fileName)));
    }
}
