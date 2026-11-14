using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>Compiles a C# snippet in memory into an assembly with a chosen name, to plant a rule violation.</summary>
internal static class Plant
{
    private static readonly ImmutableArray<MetadataReference> Framework =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))];

    /// <summary>Compiles <paramref name="source"/> as <paramref name="assemblyName"/>. Fails the test on a compile error.</summary>
    public static AssemblyDefinition Compile(
        string assemblyName, string source, bool allowUnsafe = false, params AssemblyDefinition[] references)
    {
        var bytes = Emit(assemblyName, source, allowUnsafe, references);
        var resolver = new InMemoryResolver(references);
        return AssemblyDefinition.ReadAssembly(
            new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver });
    }

    private static byte[] Emit(
        string assemblyName, string source, bool allowUnsafe, AssemblyDefinition[] references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [.. Framework, .. references.Select(ToMetadataReference)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return stream.ToArray();
    }

    private static MetadataReference ToMetadataReference(AssemblyDefinition assembly)
    {
        using var stream = new MemoryStream();
        assembly.Write(stream);
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private sealed class InMemoryResolver(AssemblyDefinition[] assemblies) : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name) =>
            assemblies.FirstOrDefault(a => a.Name.Name == name.Name) ?? base.Resolve(name);
    }
}
