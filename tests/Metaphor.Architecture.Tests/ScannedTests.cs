namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class ScannedTests
{
    [Fact]
    public void EveryProjectUnderSrcIsScanned()
    {
        var projects = Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order();

        Assert.Equal(projects, Scanned.AssemblyNames.Order());
    }

    // The mod ships the hook layer and everything it references (scripts/release/package-mod.sh), so a project of ours
    // outside src/, such as one under tools/, would ship unchecked if a scanned assembly referenced it.
    [Fact]
    public void EveryProjectAScannedAssemblyUsesIsScanned() =>
        Assert.Empty(UnscannedDependencies([.. Scanned.AssemblyNames.Select(Scanned.Load)]));

    [Fact]
    public void AnUnscannedProjectOfOursThatAScannedOneUsesIsCaught()
    {
        var tool = Plant.Compile("Metaphor.SomeTool", "public static class Tool { public static int Get() => 1; }");
        var feature = Plant.Compile(
            "Metaphor.Features.Planted", "public static class C { public static int M() => Tool.Get(); }", references: tool);

        Assert.Equal(["Metaphor.SomeTool"], UnscannedDependencies([feature]));
    }

    /// <summary>Our assemblies (named <c>Metaphor.*</c>) that <paramref name="scanned"/> reference but don't include.</summary>
    private static IEnumerable<string> UnscannedDependencies(IReadOnlyList<Mono.Cecil.AssemblyDefinition> scanned)
    {
        var names = scanned.Select(assembly => assembly.Name.Name).ToHashSet(StringComparer.Ordinal);
        return scanned
            .SelectMany(assembly => assembly.Modules)
            .SelectMany(module => module.AssemblyReferences)
            .Select(reference => reference.Name)
            .Where(name => name.StartsWith("Metaphor.", StringComparison.Ordinal) && !names.Contains(name))
            .Distinct()
            .Order();
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void EachFileIsNamedAfterItsAssembly(string name)
    {
        // The rules trust the assembly's own name, so a renamed file can't borrow the hook layer's exemptions.
        Assert.Equal(name, Scanned.Load(name).Name.Name);
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder.FullName, "Metaphor.sln")))
        {
            folder = folder.Parent ?? throw new InvalidOperationException("No Metaphor.sln above the test folder");
        }
        return folder.FullName;
    }
}
