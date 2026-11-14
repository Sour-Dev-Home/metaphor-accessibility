namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class NativeLoadingRuleTests
{
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

    [Fact]
    public void NativeLibraryLoadIsAViolation()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            "public static class C { public static nint M() => System.Runtime.InteropServices.NativeLibrary.Load(\"x.dll\"); }");

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.Contains("NativeLibrary::Load"));
    }

    [Fact]
    public void CallingLoadLibraryIsAViolation()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            NativeCall("[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]", entryPoint: "LoadLibraryW"));

        Assert.Contains(Rules.NativeLibrariesLoadSafely(assembly), v => v.Contains("LoadLibraryW"));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.NativeLibrariesLoadSafely(Scanned.Load(name)));
}
