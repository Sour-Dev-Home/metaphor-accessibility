using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class FeatureIndependenceRuleTests
{
    private static readonly AssemblyDefinition Core = Plant.Compile(
        "Metaphor.Core", "namespace Metaphor.Core { public interface IAnnouncer { void Say(string text); } }");

    private static readonly AssemblyDefinition Speech = Plant.Compile(
        "Metaphor.Features.Speech", "namespace Metaphor.Features.Speech { public sealed class Narrator { } }");

    [Fact]
    public void AFeatureUsingAnotherFeatureIsAViolation()
    {
        var audioCues = Plant.Compile(
            "Metaphor.Features.AudioCues",
            "public static class C { public static object M() => new Metaphor.Features.Speech.Narrator(); }",
            references: Speech);

        Assert.Contains(Rules.FeaturesAreIndependent(audioCues), v => v.Contains("Metaphor.Features.Speech"));
    }

    [Fact]
    public void AFeatureUsingCoreInterfacesPasses()
    {
        var audioCues = Plant.Compile(
            "Metaphor.Features.AudioCues",
            "public static class C { public static void M(Metaphor.Core.IAnnouncer a) => a.Say(\"hi\"); }",
            references: Core);

        Assert.Empty(Rules.FeaturesAreIndependent(audioCues));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.FeaturesAreIndependent(Scanned.Load(name)));
}
