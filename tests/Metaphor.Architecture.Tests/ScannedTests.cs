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
