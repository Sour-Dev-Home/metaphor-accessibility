namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class RawMemoryRuleTests
{
    private const string PointerCode = "public static unsafe class C { public static int M(int* p) { int* q = p; return *q; } }";

    [Fact]
    public void PointersAreAViolation()
    {
        var assembly = Plant.Compile("Metaphor.Core", PointerCode, allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer"));
    }

    [Fact]
    public void FunctionPointersAreAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            "public static unsafe class C { public static void M(delegate* unmanaged<void> f) => f(); }",
            allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer"));
    }

    [Fact]
    public void MarshalIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted",
            "public static class C { public static nint M() => System.Runtime.InteropServices.Marshal.AllocHGlobal(4); }");

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("Marshal"));
    }

    [Fact]
    public void PInvokeIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            """
            public static class C
            {
                [System.Runtime.InteropServices.DllImport("kernel32.dll")]
                public static extern int GetTickCount();
            }
            """);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("GetTickCount"));
    }

    [Fact]
    public void TheHookLayerMayUsePointers()
    {
        var assembly = Plant.Compile(Rules.HookLayer, PointerCode, allowUnsafe: true);

        Assert.Empty(Rules.NoRawMemoryOutsideHooks(assembly));
    }

    [Fact]
    public void SafeSpanCodePasses()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            """
            public static class C
            {
                public static int M(System.ReadOnlySpan<byte> s) =>
                    System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s);
            }
            """);

        Assert.Empty(Rules.NoRawMemoryOutsideHooks(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.NoRawMemoryOutsideHooks(Scanned.Load(name)));
}
