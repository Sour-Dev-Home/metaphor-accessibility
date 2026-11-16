using System.Text.Json;
using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>The real assemblies built from src/, copied next to the tests by the project file.</summary>
internal static class Scanned
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "scanned");

    public static IReadOnlyList<string> AssemblyNames { get; } =
        [.. Directory.GetFiles(Folder, "*.dll").Select(path => Path.GetFileNameWithoutExtension(path))];

    public static TheoryData<string> Names => new(AssemblyNames);

    /// <summary>The SHA-256 of each game build in the known-builds list embedded in the scanned Core assembly. Core's
    /// own tests validate the list; this reads only the identifiers.</summary>
    public static IReadOnlySet<string> KnownGameBuilds()
    {
        var resource = Load("Metaphor.Core").MainModule.Resources
            .OfType<EmbeddedResource>()
            .Single(r => r.Name == "Metaphor.Core.known-game-builds.json");
        using var json = JsonDocument.Parse(resource.GetResourceData());
        return json.RootElement.EnumerateArray().Select(build => build.GetProperty("sha256").GetString()!).ToHashSet();
    }

    public static AssemblyDefinition Load(string name)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Folder);
        return AssemblyDefinition.ReadAssembly(
            Path.Combine(Folder, name + ".dll"), new ReaderParameters { AssemblyResolver = resolver });
    }
}
