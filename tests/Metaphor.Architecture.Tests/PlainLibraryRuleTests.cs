namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class PlainLibraryRuleTests
{
    private static readonly Mono.Cecil.AssemblyDefinition Loader = Plant.Compile(
        "Reloaded.Mod.Interfaces", "namespace Reloaded.Mod.Interfaces { public interface IModLoader { } }");

    private const string UsesLoader = "public static class C { public static object? M(Reloaded.Mod.Interfaces.IModLoader l) => l; }";

    [Fact]
    public void ReferencingTheLoaderIsAViolation()
    {
        var assembly = Plant.Compile("Metaphor.Core", UsesLoader, references: Loader);

        Assert.Contains(Rules.NoLoaderOrWindows(assembly), v => v.Contains("Reloaded.Mod.Interfaces"));
    }

    [Fact]
    public void TheHookLayerMayReferenceTheLoader()
    {
        var assembly = Plant.Compile("Metaphor.Hooks", UsesLoader, references: Loader);

        Assert.Empty(Rules.NoLoaderOrWindows(assembly));
    }

    [Fact]
    public void WindowsRegistryIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core", "public static class C { public static object M() => Microsoft.Win32.Registry.CurrentUser; }");

        Assert.Contains(Rules.NoLoaderOrWindows(assembly), v => v.Contains("Microsoft.Win32.Registry"));
    }

    [Fact]
    public void TargetingWindowsIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted", "[assembly: System.Runtime.Versioning.SupportedOSPlatform(\"windows\")]");

        Assert.Contains(Rules.NoLoaderOrWindows(assembly), v => v.Contains("windows"));
    }

    [Fact]
    public void PlainCodePasses()
    {
        var assembly = Plant.Compile("Metaphor.Core", "public static class C { public static int M() => 1; }");

        Assert.Empty(Rules.NoLoaderOrWindows(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.NoLoaderOrWindows(Scanned.Load(name)));

    [Fact]
    public void TheRealHookLayerIsSeenAsWindowsOnly()
    {
        // Proves the platform check reads a real net9.0-windows build, not just a planted attribute.
        Assert.True(Rules.TargetsWindows(Scanned.Load(Rules.HookLayer)));
    }
}
