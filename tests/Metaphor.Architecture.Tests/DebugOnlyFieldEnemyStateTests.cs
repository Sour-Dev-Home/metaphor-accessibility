namespace Metaphor.Architecture.Tests;

/// <summary>The field enemies' AI state and HP (#649) are logged in Debug builds only: no Release assembly calls
/// <c>FieldEnemyState</c>, and the hook layer calls it exactly when it is a Debug build. CI and the package are Release
/// builds, where these assert its absence.</summary>
[Trait("Category", "Unit")]
public sealed class DebugOnlyFieldEnemyStateTests
{
    private const string Caller =
        """
        namespace Metaphor.Core
        {
            public static class FieldEnemyState { public static string Describe(long enemy) => "x"; }
        }
        public static class C
        {
            public static string M() => Metaphor.Core.FieldEnemyState.Describe(1);
        }
        """;

    [Fact]
    public void ACallInAReleaseBuildIsAViolation() =>
        Assert.Contains(
            Rules.NoDebugOnlyCallsInRelease(Plant.CompileRelease(Rules.HookLayer, Caller)),
            v => v.Contains("Metaphor.Core.FieldEnemyState::Describe", StringComparison.Ordinal));

    [Fact]
    public void ACallInADebugBuildPasses() =>
        Assert.Empty(Rules.NoDebugOnlyCallsInRelease(Plant.Compile(Rules.HookLayer, Caller)));

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesMakeNoDebugOnlyCallsInRelease(string name) =>
        Assert.Empty(Rules.NoDebugOnlyCallsInRelease(Scanned.Load(name)));

    [Fact]
    public void TheHookLayerLogsEnemyStateOnlyInADebugBuild()
    {
        var hookLayer = Scanned.Load(Rules.HookLayer);

        Assert.Equal(Rules.IsDebugBuild(hookLayer), CallsFieldEnemyState(hookLayer));
    }

    private static bool CallsFieldEnemyState(Mono.Cecil.AssemblyDefinition assembly) =>
        assembly.MainModule.GetMemberReferences()
            .OfType<Mono.Cecil.MethodReference>()
            .Any(called => called.DeclaringType.FullName == "Metaphor.Core.FieldEnemyState");
}
