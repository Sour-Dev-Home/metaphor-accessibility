namespace Metaphor.Architecture.Tests;

/// <summary>Rule 15 (#167, ADR 0010): a hook on a function in the exe's <c>.text1</c> section is opt-in
/// (<c>OptInFile</c>) or development-only (<c>DevOnly</c>), in every build.</summary>
[Trait("Category", "Unit")]
public sealed class Text1OptInRuleTests
{
    // Stands in for the real attribute; the rule matches it by full name and reads its named properties.
    private const string Attribute = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class GameHookAttribute(params string[] gameBuilds) : System.Attribute
            {
                public bool DevOnly { get; init; }
                public string? OptInFile { get; init; }
            }
        }
        """;

    private static readonly Dictionary<string, string> Sections = new(StringComparer.Ordinal)
    {
        ["MsgOpenBody"] = ".text1",
        ["MsgRequestByName"] = ".ecode",
    };

    private static Mono.Cecil.AssemblyDefinition HookLayer(string arguments, string signature) =>
        Plant.Compile(Rules.HookLayer,
            $"{Attribute}\nnamespace Metaphor.Hooks {{ [GameHook(\"b\"{arguments})] public sealed class WindowHook " +
            $"{{ public string SignatureName => \"{signature}\"; }} }}");

    [Theory]
    [InlineData("")]
    [InlineData(", DevOnly = false")]
    public void AText1HookThatIsNeitherOptInNorDevOnlyIsAViolation(string arguments)
    {
        var violation = Assert.Single(Rules.Text1HooksAreOptIn(HookLayer(arguments, "MsgOpenBody"), Sections));

        Assert.Contains("Metaphor.Hooks.WindowHook", violation, StringComparison.Ordinal);
        Assert.Contains("MsgOpenBody", violation, StringComparison.Ordinal);
        Assert.Contains(".text1", violation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(", OptInFile = \"opt-in-dialogue-window.txt\"")]
    [InlineData(", DevOnly = true")]
    public void AText1HookThatIsOptInOrDevOnlyPasses(string arguments) =>
        Assert.Empty(Rules.Text1HooksAreOptIn(HookLayer(arguments, "MsgOpenBody"), Sections));

    [Fact]
    public void AHookOutsideText1NeedsNeither() =>
        Assert.Empty(Rules.Text1HooksAreOptIn(HookLayer("", "MsgRequestByName"), Sections));

    [Fact]
    public void TheRealHooksFollowTheRule()
    {
        var sections = Scanned.SignatureSections();

        // The dialogue window hooks (#153) are in .text1, so the rule has real hooks to check.
        Assert.Equal(".text1", sections["MsgOpenBody"]);
        Assert.Empty(Rules.Text1HooksAreOptIn(Scanned.Load(Rules.HookLayer), sections));
    }
}
