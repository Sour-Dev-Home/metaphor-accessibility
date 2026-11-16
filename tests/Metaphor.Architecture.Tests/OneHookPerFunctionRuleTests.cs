namespace Metaphor.Architecture.Tests;

/// <summary>Rule 13 (#86): one hook per game function. Two hooks on one function each decode the same call, and the
/// order they run in depends on install order; observers behind one hook share a single decode.</summary>
[Trait("Category", "Unit")]
public sealed class OneHookPerFunctionRuleTests
{
    // Stands in for the real attribute; the rule matches it by full name.
    private const string Attribute = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class GameHookAttribute(params string[] gameBuilds) : System.Attribute { }
        }
        """;

    private static Mono.Cecil.AssemblyDefinition HookLayer(string source) =>
        Plant.Compile(Rules.HookLayer, $"{Attribute}\nnamespace Metaphor.Hooks {{ {source} }}");

    private static string Hook(string type, string signature) =>
        $"[GameHook(\"b\")] public sealed class {type} {{ public string SignatureName => \"{signature}\"; }}";

    [Fact]
    public void TwoHooksOnOneFunctionAreAViolation()
    {
        var assembly = HookLayer(Hook("DialogueHook", "MsgRequestByName") + Hook("RequestListener", "MsgRequestByName"));

        var violation = Assert.Single(Rules.OneHookPerFunction(assembly));
        Assert.Contains("MsgRequestByName", violation, StringComparison.Ordinal);
        Assert.Contains("Metaphor.Hooks.DialogueHook", violation, StringComparison.Ordinal);
        Assert.Contains("Metaphor.Hooks.RequestListener", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void HooksOnDifferentFunctionsPass()
    {
        var assembly = HookLayer(Hook("DialogueHook", "MsgRequestByName") + Hook("FileHook", "CompileMsgFile"));

        Assert.Empty(Rules.OneHookPerFunction(assembly));
    }

    [Fact]
    public void AHookWhoseSignatureNameIsNotAConstantIsAViolation()
    {
        var assembly = HookLayer(
            "[GameHook(\"b\")] public sealed class DialogueHook { public string SignatureName => System.Environment.MachineName; }");

        Assert.Contains(Rules.OneHookPerFunction(assembly),
            v => v.Contains("Metaphor.Hooks.DialogueHook") && v.Contains("constant"));
    }

    [Fact]
    public void TypesThatAreNotHooksAreNotChecked()
    {
        var assembly = HookLayer(
            "public sealed class A { public string SignatureName => \"X\"; } public sealed class B { public string SignatureName => \"X\"; }");

        Assert.Empty(Rules.OneHookPerFunction(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) =>
        Assert.Empty(Rules.OneHookPerFunction(Scanned.Load(name)));
}
