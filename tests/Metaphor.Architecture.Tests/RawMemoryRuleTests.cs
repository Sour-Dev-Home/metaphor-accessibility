namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class RawMemoryRuleTests
{
    private const string PointerCode = "public static unsafe class C { public static int M(int* p) { int* q = p; return *q; } }";

    [Fact]
    public void PointersAreAViolation()
    {
        var assembly = Plant.Compile("Metaphor.Core", PointerCode, allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer in System.Int32 C::M(System.Int32*)"));
    }

    [Fact]
    public void FunctionPointersAreAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            "public static unsafe class C { public static void M(delegate* unmanaged<void> f) => f(); }",
            allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer in System.Void C::M("));
    }

    [Fact]
    public void PointerLocalsAreAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            "public static unsafe class C { public static nint M() { int x = 1; int* p = &x; return (nint)p; } }",
            allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer in System.IntPtr C::M()"));
    }

    [Fact]
    public void PointerFieldsAreAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core", "public unsafe struct S { public int* P; }", allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("pointer in field System.Int32* S::P"));
    }

    [Fact]
    public void UnsafeCodeWithNoPointerSlotIsAViolation()
    {
        // The pointer exists only on the evaluation stack: no parameter, local or field holds one.
        var assembly = Plant.Compile(
            "Metaphor.Core", "public static class C { public static unsafe void M(nint a) { *(int*)a = 1; } }",
            allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.Contains("allows unsafe code"));
    }

    [Fact]
    public void NativeMemoryIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Core",
            "public static unsafe class C { public static nint M() => (nint)System.Runtime.InteropServices.NativeMemory.Alloc(4); }",
            allowUnsafe: true);

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.EndsWith(".NativeMemory", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("System.Runtime.InteropServices.Marshal.AllocHGlobal(4)", "Marshal")]
    [InlineData("System.Runtime.CompilerServices.Unsafe.SizeOf<int>()", "Unsafe")]
    [InlineData("System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref Field, 4).Length", "MemoryMarshal")]
    [InlineData("System.Runtime.InteropServices.GCHandle.Alloc(Field)", "GCHandle")]
    public void RawMemoryApisAreAViolation(string expression, string api)
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted",
            $"public static class C {{ public static int Field; public static object M() => {expression}; }}");

        Assert.Contains(Rules.NoRawMemoryOutsideHooks(assembly), v => v.EndsWith("." + api, StringComparison.Ordinal));
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
