namespace Metaphor.Architecture.Tests;

/// <summary>The capture build's probes are compiled into Debug builds only: no Release assembly writes a
/// <c>CaptureLines</c> line, and the hook layer has its poll (<c>CaptureProbes</c>) and writes those lines exactly when
/// it is a Debug build. CI and the package are Release builds, where these assert their absence.</summary>
[Trait("Category", "Unit")]
public sealed class DebugOnlyCaptureProbesTests
{
    private const string Caller =
        """
        namespace Metaphor.Core
        {
            public static class CaptureLines { public static string Locks(string locks) => locks; }
        }
        public static class C
        {
            public static string M() => Metaphor.Core.CaptureLines.Locks("x");
        }
        """;

    [Fact]
    public void ACaptureLineInAReleaseBuildIsAViolation() =>
        Assert.Contains(
            Rules.NoDebugOnlyCallsInRelease(Plant.CompileRelease(Rules.HookLayer, Caller)),
            v => v.Contains("Metaphor.Core.CaptureLines::Locks", StringComparison.Ordinal));

    [Fact]
    public void ACaptureLineInADebugBuildPasses() =>
        Assert.Empty(Rules.NoDebugOnlyCallsInRelease(Plant.Compile(Rules.HookLayer, Caller)));

    [Fact]
    public void TheHookLayerHasTheCapturePollOnlyInADebugBuild()
    {
        var hookLayer = Scanned.Load(Rules.HookLayer);

        Assert.Equal(Rules.IsDebugBuild(hookLayer), hookLayer.MainModule.GetType("Metaphor.Hooks.CaptureProbes") is not null);
    }

    [Fact]
    public void TheHookLayerWritesCaptureLinesOnlyInADebugBuild()
    {
        var hookLayer = Scanned.Load(Rules.HookLayer);

        var calls = hookLayer.MainModule.GetMemberReferences().OfType<Mono.Cecil.MethodReference>()
            .Any(called => called.DeclaringType.FullName == "Metaphor.Core.CaptureLines");

        Assert.Equal(Rules.IsDebugBuild(hookLayer), calls);
    }
}
