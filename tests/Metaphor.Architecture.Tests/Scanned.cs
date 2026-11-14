using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>The real assemblies built from src/, copied next to the tests by the project file.</summary>
internal static class Scanned
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "scanned");

    public static IReadOnlyList<string> AssemblyNames { get; } =
        [.. Directory.GetFiles(Folder, "*.dll").Select(path => Path.GetFileNameWithoutExtension(path))];

    public static TheoryData<string> Names => new(AssemblyNames);

    public static AssemblyDefinition Load(string name)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Folder);
        return AssemblyDefinition.ReadAssembly(
            Path.Combine(Folder, name + ".dll"), new ReaderParameters { AssemblyResolver = resolver });
    }
}
