namespace Metaphor.Architecture.Tests;

/// <summary>Rules 9–11: the lessons #55's hooks taught, as checks (#69).</summary>
[Trait("Category", "Unit")]
public sealed class HookShapeRuleTests
{
    // Stand-ins for the real attribute and the hooking library; the rules match them by full name.
    private const string Stubs = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class GameHookAttribute : System.Attribute
            {
                public GameHookAttribute(params string[] gameBuilds) { }
            }
        }
        namespace Reloaded.Hooks.Definitions
        {
            public interface IHook<T> { IHook<T> Activate(); T OriginalFunction { get; } }
            public interface IReloadedHooks { IHook<T> CreateHook<T>(T function, long address); }
        }
        """;

    private static Mono.Cecil.AssemblyDefinition HookLayer(string source) =>
        Plant.Compile(Rules.HookLayer, Stubs + source, allowUnsafe: true);

    // Rule 9: hook delegates use pointer-sized integers.

    [Fact]
    public void AHookDelegateWithA32BitIntegerIsAViolation()
    {
        var assembly = HookLayer("""
            [Metaphor.Hooks.GameHook] public sealed class MenuHook
            {
                private delegate uint Show(nint menu, int index, byte flags);
            }
            """);

        var violations = Rules.HookDelegatesArePointerSized(assembly);

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Contains("MenuHook/Show returns System.UInt32"));
        Assert.Contains(violations, v => v.Contains("MenuHook/Show parameter index is System.Int32"));
        Assert.Contains(violations, v => v.Contains("MenuHook/Show parameter flags is System.Byte"));
    }

    [Fact]
    public void AHookDelegateOfPointerSizedValuesPasses()
    {
        var assembly = HookLayer("""
            [Metaphor.Hooks.GameHook] public sealed unsafe class MenuHook
            {
                private delegate nint Show(nint menu, nuint count, long id, double time, int* index);
                private delegate void Close(ulong menu);
            }
            """);

        Assert.Empty(Rules.HookDelegatesArePointerSized(assembly));
    }

    [Fact]
    public void ADelegateHookedFromOutsideAHookAndAHooksFunctionPointerAreChecked()
    {
        var assembly = HookLayer("""
            public delegate int Shared(nint menu);
            public sealed class Helper
            {
                public void Hook(Reloaded.Hooks.Definitions.IReloadedHooks hooks, Shared shared, System.Func<int, nint> show)
                {
                    hooks.CreateHook(shared, 0);
                    hooks.CreateHook(show, 0);
                }
            }
            [Metaphor.Hooks.GameHook] public sealed unsafe class MenuHook
            {
                private delegate* unmanaged<nint, uint> original;
            }
            """);

        var violations = Rules.HookDelegatesArePointerSized(assembly);

        Assert.Contains(violations, v => v.Contains("Shared returns System.Int32"));
        Assert.Contains(violations, v => v.Contains("System.Func`2<System.Int32,System.IntPtr> passes System.Int32"));
        Assert.Contains(violations, v => v.Contains("MenuHook field original returns System.UInt32"));
    }

    [Fact]
    public void ADelegateOutsideAHookIsNotChecked()
    {
        var assembly = HookLayer("public sealed class Speech { private delegate int Speak(int text); }");

        Assert.Empty(Rules.HookDelegatesArePointerSized(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void EveryHookDelegateIsPointerSized(string name) =>
        Assert.Empty(Rules.HookDelegatesArePointerSized(Scanned.Load(name)));

    // Rule 10: game memory is read only through ModuleMemoryReader.

    [Fact]
    public void ReadingAGamePointerDirectlyIsAViolation()
    {
        var assembly = HookLayer("""
            [Metaphor.Hooks.GameHook] public sealed unsafe class MenuHook
            {
                private string Title(nint title) => System.Runtime.InteropServices.Marshal.PtrToStringAnsi(title);
                private int Index(int* index) => *index;
            }
            """);

        var violations = Rules.GameMemoryIsReadSafely(assembly);

        Assert.Contains(violations, v => v.Contains("MenuHook reads memory by address (System.Runtime.InteropServices.Marshal::PtrToStringAnsi)"));
        Assert.Contains(violations, v => v.Contains("MenuHook reads memory by address (ldind.i4)"));
    }

    [Fact]
    public void ReadingAGamePointerThroughAFieldOrAFrameworkCallIsAViolation()
    {
        var assembly = HookLayer("""
            public struct Menu { public int Index; }
            [Metaphor.Hooks.GameHook] public sealed unsafe class MenuHook
            {
                private int Index(Menu* menu) => menu->Index;
                private int Cast(nint menu) => ((Menu*)menu)->Index;
                private string Title(nint title) => new string((sbyte*)title);
                private string Name(byte* name, int length) => System.Text.Encoding.UTF8.GetString(name, length);
            }
            """);

        var violations = Rules.GameMemoryIsReadSafely(assembly);

        Assert.Contains(violations, v => v.Contains("(ldfld through a pointer)"));
        Assert.Contains(violations, v => v.Contains("(System.String::.ctor)"));
        Assert.Contains(violations, v => v.Contains("(System.Text.Encoding::GetString)"));
    }

    [Fact]
    public void AUtf8LiteralIsNotARead()
    {
        var assembly = HookLayer("""
            [Metaphor.Hooks.GameHook] public sealed class MenuHook
            {
                private int Length() => "menu"u8.Length;
            }
            """);

        Assert.Empty(Rules.GameMemoryIsReadSafely(assembly));
    }

    [Fact]
    public void ReadingThroughTheMemoryReaderPasses()
    {
        var assembly = HookLayer("""
            namespace Metaphor.Hooks
            {
                internal static unsafe class ModuleMemoryReader
                {
                    public static int? ReadInt32(nint address) => *(int*)address;
                }
            }
            [Metaphor.Hooks.GameHook] public sealed unsafe class MenuHook
            {
                private int Index(int* index) => Metaphor.Hooks.ModuleMemoryReader.ReadInt32((nint)index) ?? -1;
            }
            """);

        Assert.Empty(Rules.GameMemoryIsReadSafely(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void GameMemoryIsOnlyReadThroughTheReader(string name) =>
        Assert.Empty(Rules.GameMemoryIsReadSafely(Scanned.Load(name)));

    // Rule 11: a hook is stored in a field before it is activated.

    private const string Hook = """
        [Metaphor.Hooks.GameHook] public sealed class MenuHook
        {
            private System.Action callback = () => { };
            private Reloaded.Hooks.Definitions.IHook<System.Action> hook;

            public void Install(Reloaded.Hooks.Definitions.IReloadedHooks hooks, long address)
            {
                INSTALL
            }
        }
        """;

    [Theory]
    [InlineData("hook = hooks.CreateHook(callback, address).Activate();")]
    [InlineData("var created = hooks.CreateHook(callback, address); created.Activate(); hook = created;")]
    // A captured local becomes a field of a compiler-made closure that nothing keeps alive.
    [InlineData("var created = hooks.CreateHook(callback, address); System.Action go = () => created.Activate(); go();")]
    public void ActivatingAHookNotYetStoredIsAViolation(string install)
    {
        var assembly = HookLayer(Hook.Replace("INSTALL", install, StringComparison.Ordinal));

        var violation = Assert.Single(Rules.HooksAreStoredBeforeActivating(assembly));
        Assert.Contains("MenuHook", violation, StringComparison.Ordinal);
        Assert.Contains("activates a hook before storing it in one of its own fields", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void StoringTheHookThenActivatingItPasses()
    {
        var assembly = HookLayer(Hook.Replace(
            "INSTALL", "hook = hooks.CreateHook(callback, address); hook.Activate();", StringComparison.Ordinal));

        Assert.Empty(Rules.HooksAreStoredBeforeActivating(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void EveryHookIsStoredBeforeItIsActivated(string name) =>
        Assert.Empty(Rules.HooksAreStoredBeforeActivating(Scanned.Load(name)));
}
