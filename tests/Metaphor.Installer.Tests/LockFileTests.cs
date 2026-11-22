using System.Text.Json;
using System.Xml.Linq;

namespace Metaphor.Installer.Tests;

/// <summary>Restore gives the same lock file on every OS. The SDK infers a runtime identifier for a .NET Framework exe
/// on Windows alone (Microsoft.NET.RuntimeIdentifierInference.targets, SDK 9.0.319), so a lock file written on Windows
/// names one that Linux CI's locked restore never sees (#720). Each one a lock file names is stated in its project.</summary>
[Trait("Category", "Unit")]
public sealed class LockFileTests
{
    private static readonly string[] ProjectFolders = ["src", "tests", "tools", "installer"];

    public static TheoryData<string> LockFiles() => [.. LockFilePaths()];

    private static IEnumerable<string> LockFilePaths()
    {
        var root = RepositoryRoot();
        return ProjectFolders
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "packages.lock.json", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal);
    }

    [Fact]
    public void TheInstallerLockFileIsAmongThemAndNamesARuntimeIdentifier()
    {
        // The rule below would pass on nothing; this keeps it reading the lock file that broke CI.
        var installer = Path.Combine("installer", "Metaphor.Installer", "packages.lock.json");

        Assert.Contains(installer, LockFilePaths());
        Assert.Equal(["win-x64"], LockedRuntimeIdentifiers(Path.Combine(RepositoryRoot(), installer)));
    }

    [Theory]
    [MemberData(nameof(LockFiles))]
    public void EachRuntimeIdentifierInALockFileIsStatedInItsProject(string lockFile)
    {
        var path = Path.Combine(RepositoryRoot(), lockFile);
        var project = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.csproj"));

        Assert.Empty(LockedRuntimeIdentifiers(path).Except(StatedRuntimeIdentifiers(project)));
    }

    [Fact]
    public void AnInferredRuntimeIdentifierIsCaught()
    {
        var folder = Directory.CreateTempSubdirectory("metaphor-lockfile-");
        try
        {
            var project = Path.Combine(folder.FullName, "Planted.csproj");
            File.WriteAllText(project, "<Project><PropertyGroup><PlatformTarget>x64</PlatformTarget></PropertyGroup></Project>");
            var lockFile = Path.Combine(folder.FullName, "packages.lock.json");
            File.WriteAllText(lockFile, """{ "version": 2, "dependencies": { "net48": {}, "net48/win-x64": {} } }""");

            Assert.Equal(["win-x64"], LockedRuntimeIdentifiers(lockFile).Except(StatedRuntimeIdentifiers(project)));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>The runtime identifiers in a lock file's target names, such as <c>net48/win-x64</c>.</summary>
    private static List<string> LockedRuntimeIdentifiers(string lockFile)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(lockFile));
        return json.RootElement.GetProperty("dependencies").EnumerateObject()
            .Select(target => target.Name)
            .Where(name => name.Contains('/', StringComparison.Ordinal))
            .Select(name => name[(name.IndexOf('/', StringComparison.Ordinal) + 1)..])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The runtime identifiers a project sets itself, under any condition.</summary>
    private static HashSet<string> StatedRuntimeIdentifiers(string project) =>
        XDocument.Load(project).Descendants()
            .Where(element => element.Name.LocalName is "RuntimeIdentifier" or "RuntimeIdentifiers")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

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
