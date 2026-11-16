namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class GameHookRuleTests
{
    private const string Known = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const string Unknown = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    // Stands in for the real attribute in Metaphor.Hooks; the rule matches it by full name.
    private const string Attribute = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class GameHookAttribute : System.Attribute
            {
                public GameHookAttribute(params string[] gameBuilds) { }
            }
        }
        """;

    private static readonly HashSet<string> KnownBuilds = [Known];

    [Fact]
    public void AHookWithNoBuildIsAViolation()
    {
        var assembly = Plant.Compile(Rules.HookLayer, Attribute + "[Metaphor.Hooks.GameHook] public sealed class MenuHook { }");

        Assert.Contains(Rules.HooksNameKnownBuilds(assembly, KnownBuilds), v => v.Contains("MenuHook names no game build"));
    }

    [Fact]
    public void AHookWithAnUnknownBuildIsAViolation()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer, Attribute + $"[Metaphor.Hooks.GameHook(\"{Known}\", \"{Unknown}\")] public sealed class MenuHook {{ }}");

        var violation = Assert.Single(Rules.HooksNameKnownBuilds(assembly, KnownBuilds));
        Assert.Contains($"MenuHook names unknown game build {Unknown}", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void AHookNamingKnownBuildsPasses()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer, Attribute + $"[Metaphor.Hooks.GameHook(\"{Known}\")] public sealed class MenuHook {{ }}");

        Assert.Empty(Rules.HooksNameKnownBuilds(assembly, KnownBuilds));
    }

    [Fact]
    public void TheHookLayerDefinesTheAttributeTheRuleLooksFor() =>
        Assert.NotNull(Scanned.Load(Rules.HookLayer).MainModule.GetType(Rules.GameHookAttribute));

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void EveryHookNamesAKnownBuild(string name) =>
        Assert.Empty(Rules.HooksNameKnownBuilds(Scanned.Load(name), Scanned.KnownGameBuilds()));

    [Fact]
    public void TheKnownBuildsAreReadFromCore() =>
        Assert.Contains(Known, Scanned.KnownGameBuilds());
}
