namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class NativeLoadingRuleTests
{
    // An assembly with no native-loading exception, unlike the hook layer (ADR 0005).
    private const string NoException = "Metaphor.Core";

    private static string NativeCall(string attributes = "", string entryPoint = "GetTickCount", string assembly = "") =>
        $$"""
        using System.Runtime.InteropServices;
        {{assembly}}
        public static class C
        {
            {{attributes}}
            [DllImport("kernel32.dll")]
            public static extern int {{entryPoint}}();
        }
        """;

    [Fact]
    public void ANativeCallWithTheDefaultSearchIsAViolation()
    {
        var assembly = Plant.Compile(Rules.HookLayer, NativeCall());

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.Contains("GetTickCount"));
    }

    [Fact]
    public void ANativeCallSearchingTheAppFolderIsAViolation()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            NativeCall("[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]"));

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.Contains("GetTickCount"));
    }

    [Fact]
    public void ANativeCallRestrictedToSystem32Passes()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            NativeCall("[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]"));

        Assert.Empty(Rules.NativeLibrariesLoadSafely(assembly));
    }

    [Fact]
    public void AnAssemblyWideSystem32RestrictionPasses()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            NativeCall(assembly: "[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]"));

        Assert.Empty(Rules.NativeLibrariesLoadSafely(assembly));
    }

    [Theory]
    [InlineData("System.Runtime.InteropServices.NativeLibrary.Load(\"x.dll\")", "NativeLibrary::Load")]
    [InlineData("System.Runtime.InteropServices.NativeLibrary.TryLoad(\"x.dll\", out _)", "NativeLibrary::TryLoad")]
    [InlineData(
        "System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(C).Assembly, (n, a, p) => 0)",
        "NativeLibrary::SetDllImportResolver")]
    public void LoadingThroughTheRuntimeIsAViolation(string statement, string api)
    {
        var assembly = Plant.Compile(NoException, $"public static class C {{ public static void M() {{ {statement}; }} }}");

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.EndsWith(api, StringComparison.Ordinal));
    }

    [Fact]
    public void ALoadContextLoadingNativeLibrariesIsAViolation()
    {
        var assembly = Plant.Compile(
            NoException,
            """
            public sealed class Context : System.Runtime.Loader.AssemblyLoadContext
            {
                protected override nint LoadUnmanagedDll(string name) => LoadUnmanagedDllFromPath(name);
            }
            """);

        Assert.Contains(
            Rules.NativeLibrariesLoadSafely(assembly),
            v => v.EndsWith("AssemblyLoadContext::LoadUnmanagedDllFromPath", StringComparison.Ordinal));
    }

    [Fact]
    public void CallingLoadLibraryIsAViolation()
    {
        var assembly = Plant.Compile(
            NoException,
            NativeCall("[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]", entryPoint: "LoadLibraryW"));

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.Contains("LoadLibraryW"));
    }

    [Fact]
    public void TheHookLayerMayLoadALibraryThroughTheRuntimeUnderItsException()
    {
        // ADR 0005: prism.dll, by full path after its hash check. The exception covers only runtime loading; a DllImport
        // in the hook layer still has to search System32 alone (the tests above).
        var assembly = Plant.Compile(
            Rules.HookLayer,
            "public static class C { public static nint M(string path) => System.Runtime.InteropServices.NativeLibrary.Load(path); }");

        Assert.Empty(Rules.NativeLibrariesLoadSafely(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.NativeLibrariesLoadSafely(Scanned.Load(name)));
}
