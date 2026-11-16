namespace Metaphor.Architecture.Tests;

/// <summary>Rule 12 (#85): hooks get their dependencies through the constructor, not from statics someone set first.</summary>
[Trait("Category", "Unit")]
public sealed class HookDependencyRuleTests
{
    // Stand-ins for the real attribute and services record; the rule matches them by full name.
    private const string Stubs = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class GameHookAttribute(params string[] gameBuilds) : System.Attribute { }
            public sealed record HookServices(string Messages);
        }
        """;

    private static Mono.Cecil.AssemblyDefinition HookLayer(string source) =>
        Plant.Compile(Rules.HookLayer, $"{Stubs}\nnamespace Metaphor.Hooks {{ {source} }}");

    [Theory]
    [InlineData("public static class Shared { public static string? Log { get; set; } }", "Log")]
    [InlineData("public static class Shared { public static string? Log { get; private set; } }", "Log")]
    [InlineData("public static class Shared { public static string? Log; }", "Log")]
    [InlineData("public sealed class Shared { private static int count; public void Add() => count++; }", "count")]
    public void MutableStaticStateIsAViolation(string type, string member)
    {
        var violations = Rules.HooksGetDependenciesThroughConstructor(HookLayer(type));

        Assert.Contains(violations, v => v.Contains("Metaphor.Hooks.Shared") && v.Contains(member));
    }

    [Fact]
    public void ReadOnlyStaticsConstantsAndLambdasPass()
    {
        var assembly = HookLayer("""
            public static class Shared
            {
                public const string Build = "b";
                private static readonly System.Collections.Generic.List<object> Installed = [];
                public static string Name { get; } = "n";
                public static System.Func<int, int> Twice() => x => x * 2;
            }
            """);

        Assert.Empty(Rules.HooksGetDependenciesThroughConstructor(assembly));
    }

    [Theory]
    [InlineData("public DialogueHook() { }")]
    [InlineData("internal DialogueHook(HookServices services) { }")]
    [InlineData("public DialogueHook(HookServices services, int extra) { }")]
    public void AHookWithoutAPublicServicesConstructorIsAViolation(string constructor)
    {
        var assembly = HookLayer($"[GameHook(\"b\")] public sealed class DialogueHook {{ {constructor} }}");

        Assert.Contains(Rules.HooksGetDependenciesThroughConstructor(assembly),
            v => v.Contains("Metaphor.Hooks.DialogueHook") && v.Contains("HookServices"));
    }

    [Fact]
    public void AHookTakingServicesPasses()
    {
        var assembly = HookLayer(
            "[GameHook(\"b\")] public sealed class DialogueHook(HookServices services) { public string M => services.Messages; }");

        Assert.Empty(Rules.HooksGetDependenciesThroughConstructor(assembly));
    }

    [Fact]
    public void OnlyTheHookLayerIsChecked()
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted", "public static class Shared { public static string? Log { get; set; } }");

        Assert.Empty(Rules.HooksGetDependenciesThroughConstructor(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) =>
        Assert.Empty(Rules.HooksGetDependenciesThroughConstructor(Scanned.Load(name)));
}
