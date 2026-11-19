namespace Metaphor.Architecture.Tests;

/// <summary>The live inspector (#291) is a way into the running game from outside it, so it is compiled into Debug
/// builds only: no Release assembly uses named pipes, and the hook layer has the inspector exactly when it is a Debug
/// build. CI and the package are Release builds, where these assert its absence.</summary>
[Trait("Category", "Unit")]
public sealed class LiveInspectorRuleTests
{
    private const string PipeServer =
        """
        public static class C
        {
            public static object M() => new System.IO.Pipes.NamedPipeServerStream("x");
        }
        """;

    [Fact]
    public void APipeInAReleaseBuildIsAViolation() =>
        Assert.Contains(
            Rules.NoPipesInRelease(Plant.CompileRelease(Rules.HookLayer, PipeServer)),
            v => v.Contains("System.IO.Pipes.NamedPipeServerStream", StringComparison.Ordinal));

    [Fact]
    public void APipeInADebugBuildPasses() =>
        Assert.Empty(Rules.NoPipesInRelease(Plant.Compile(Rules.HookLayer, PipeServer)));

    [Fact]
    public void AReleaseBuildWithoutPipesPasses() =>
        Assert.Empty(Rules.NoPipesInRelease(
            Plant.CompileRelease(Rules.HookLayer, "public static class C { public static int M() => 1; }")));

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesUseNoPipesInRelease(string name) => Assert.Empty(Rules.NoPipesInRelease(Scanned.Load(name)));

    [Fact]
    public void TheHookLayerHasTheInspectorOnlyInADebugBuild()
    {
        var hookLayer = Scanned.Load(Rules.HookLayer);

        Assert.Equal(Rules.IsDebugBuild(hookLayer), hookLayer.MainModule.GetType("Metaphor.Hooks.LiveInspector") is not null);
    }
}
