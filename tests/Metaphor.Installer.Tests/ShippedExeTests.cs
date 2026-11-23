using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Metaphor.Installer.Tests;

/// <summary>The net48 exe players run, read as a file: x64, never asking for administrator rights, and built on
/// .NET Framework's own assemblies alone, none of them for networking.</summary>
[Trait("Category", "Unit")]
public sealed class ShippedExeTests
{
    private static readonly string Exe = Path.Combine(AppContext.BaseDirectory, "shipped", "Metaphor.Installer.exe");

    private static readonly string[] FrameworkAssemblies =
        ["mscorlib", "System", "System.Core", "System.IO.Compression", "System.Speech"];

    [Fact]
    public void IsAnX64Exe()
    {
        using var pe = new PEReader(File.OpenRead(Exe));

        Assert.Equal(Machine.Amd64, pe.PEHeaders.CoffHeader.Machine);
        Assert.False(pe.PEHeaders.IsDll);
    }

    [Fact]
    public void AsksForNoAdministratorRights()
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(Exe));

        Assert.Contains("level=\"asInvoker\"", text);
        Assert.DoesNotContain("requireAdministrator", text);
        Assert.DoesNotContain("highestAvailable", text);
    }

    [Fact]
    public void ReferencesOnlyDotNetFrameworksOwnAssemblies()
    {
        var references = Read(metadata => metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToList());

        Assert.Empty(references.Except(FrameworkAssemblies));
    }

    [Fact]
    public void UsesNoNetworking()
    {
        var namespaces = Read(metadata => metadata.TypeReferences
            .Select(handle => metadata.GetString(metadata.GetTypeReference(handle).Namespace)).Distinct().ToList());

        Assert.DoesNotContain(namespaces, name => name == "System.Net" || name.StartsWith("System.Net.", StringComparison.Ordinal));
        Assert.Contains("System.IO.Compression", namespaces); // the reader sees the exe's real references
    }

    private static T Read<T>(Func<MetadataReader, T> read)
    {
        using var pe = new PEReader(File.OpenRead(Exe));
        return read(pe.GetMetadataReader());
    }
}
