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
