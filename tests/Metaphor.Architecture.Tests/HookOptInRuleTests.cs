using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>#39: code that patches the game can't skip the hook loader's build check by leaving out <c>[GameHook]</c>.</summary>
[Trait("Category", "Unit")]
public sealed class HookOptInRuleTests
{
    // Stands in for Reloaded-II's shared hooks library, so the snippets below can call "the hooking API".
    private static readonly AssemblyDefinition HooksLibrary = Plant.Compile(
        "Reloaded.Hooks.Definitions",
        "namespace Reloaded.Hooks.Definitions { public interface IReloadedHooks { object CreateHook(object f, long address); } }");

    private const string Attribute =
        "namespace Metaphor.Hooks { [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)] " +
        "public sealed class GameHookAttribute(params string[] builds) : System.Attribute { } }";

    private const string Hooking =
        "public void Install(Reloaded.Hooks.Definitions.IReloadedHooks hooks, long at) => hooks.CreateHook(this, at);";

    private const string WritingMemory =
        "public void Install(nint at) => System.Runtime.InteropServices.Marshal.WriteInt32(at, 0);";

    private static AssemblyDefinition HookLayer(string types) =>
        Plant.Compile(Rules.HookLayer, $"{Attribute}\nnamespace Metaphor.Hooks {{ {types} }}", references: HooksLibrary);

    [Theory]
    [InlineData(Hooking)]
    [InlineData(WritingMemory)]
    public void PatchingWithoutGameHookIsAViolation(string member)
    {
        var assembly = HookLayer($"public sealed class Sneaky {{ {member} }}");

        Assert.Contains(Rules.PatchingNeedsGameHook(assembly), v => v.Contains("Metaphor.Hooks.Sneaky"));
    }

    [Theory]
    [InlineData(Hooking)]
    [InlineData(WritingMemory)]
    public void PatchingInAGameHookPasses(string member)
    {
        var assembly = HookLayer($"[GameHook(\"b\")] public sealed class Dialogue {{ {member} }}");

        Assert.Empty(Rules.PatchingNeedsGameHook(assembly));
    }

    [Fact]
    public void ASubclassOfAGameHookDoesNotInheritIt()
    {
        var assembly = HookLayer(
            $"[GameHook(\"b\")] public class Dialogue {{ }} public sealed class Sneaky : Dialogue {{ {WritingMemory} }}");

        Assert.Contains(Rules.PatchingNeedsGameHook(assembly), v => v.Contains("Metaphor.Hooks.Sneaky"));
    }

    [Fact]
    public void ALambdaInsideAGameHookPasses()
    {
        var assembly = HookLayer(
            "[GameHook(\"b\")] public sealed class Dialogue { public System.Action Later(nint at) => " +
            "() => System.Runtime.InteropServices.Marshal.WriteInt32(at, 0); }");

        Assert.Empty(Rules.PatchingNeedsGameHook(assembly));
    }

    [Fact]
    public void TheHookLoaderMayUseTheHookingApi()
    {
        var assembly = HookLayer($"public static class HookLoader {{ public static void Get(Reloaded.Hooks.Definitions.IReloadedHooks hooks) {{ }} }}");

        Assert.Empty(Rules.PatchingNeedsGameHook(assembly));
    }

    [Theory]
    [InlineData("public static unsafe void M(nint at) => *(int*)at = 0;", "stind")]
    [InlineData("public static unsafe void M(nint at) => new System.Span<byte>((void*)at, 4)[0] = 1;", "Span")]
    [InlineData("public static unsafe void M(nint at) => ((delegate* unmanaged<void>)at)();", "calli")]
    [InlineData("public static unsafe void M(nint at) => System.Runtime.InteropServices.NativeMemory.Fill((void*)at, 4, 0);", "NativeMemory::Fill")]
    [InlineData("public static unsafe void M(nint at) => System.Runtime.CompilerServices.Unsafe.InitBlock((void*)at, 0, 4);", "Unsafe::InitBlock")]
    [InlineData("public static void M(nint at) => System.Runtime.InteropServices.Marshal.StructureToPtr(1, at, false);", "Marshal::StructureToPtr")]
    [InlineData("[System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] public static extern nint VirtualAlloc(nint a, nuint s, uint t, uint p);", "VirtualAlloc")]
    public void OtherWaysToWriteMemoryAreViolationsToo(string member, string use)
    {
        var assembly = Plant.Compile(
            Rules.HookLayer, $"{Attribute}\nnamespace Metaphor.Hooks {{ public static class Sneaky {{ {member} }} }}",
            allowUnsafe: true, HooksLibrary);

        Assert.Contains(Rules.PatchingNeedsGameHook(assembly), v => v.Contains("Sneaky") && v.Contains(use));
    }

    [Fact]
    public void TheLoadersNameOutsideTheHookLayerIsNoExemption()
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted",
            $"namespace Metaphor.Hooks {{ public static class HookLoader {{ {WritingMemory.Replace("public void", "public static void")} }} }}");

        Assert.Contains(Rules.PatchingNeedsGameHook(assembly), v => v.Contains("Metaphor.Hooks.HookLoader"));
    }

    [Fact]
    public void PrismsBindingMayCallThroughFunctionPointers()
    {
        // ADR 0005: PrismSpeech calls prism.dll's exports through function pointers; it patches nothing.
        var assembly = Plant.Compile(
            Rules.HookLayer,
            "namespace Metaphor.Hooks { internal sealed unsafe class PrismSpeech { public static void M(nint at) => ((delegate* unmanaged<void>)at)(); } }",
            allowUnsafe: true);

        Assert.Empty(Rules.PatchingNeedsGameHook(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesComply(string name) => Assert.Empty(Rules.PatchingNeedsGameHook(Scanned.Load(name)));
}
