namespace Metaphor.Architecture.Tests;

/// <summary>Rule 14 (#88): hooks pass calls through the loader's adapters. A hook observes through one shape and leaves
/// creating, activating and guarding to the adapter, so that code exists once.</summary>
[Trait("Category", "Unit")]
public sealed class PassThroughRuleTests
{
    // Stand-ins for the real attribute, a shape interface and the hooking library; the rule matches them by full name.
    private const string Stubs = """
        namespace Metaphor.Hooks
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class GameHookAttribute : System.Attribute
            {
                public GameHookAttribute(params string[] gameBuilds) { }
            }
            public interface IObserves3 { void Observe(nint a, nint b, nint c); }
            public interface IObserves4 { void Observe(nint a, nint b, nint c, nint d); }
        }
        namespace Reloaded.Hooks.Definitions
        {
            public interface IHook<T> { IHook<T> Activate(); T OriginalFunction { get; } }
            public interface IReloadedHooks { IHook<T> CreateHook<T>(T function, long address); }
        }
        """;

    private static Mono.Cecil.AssemblyDefinition HookLayer(string source) =>
        Plant.Compile(Rules.HookLayer, Stubs + source);

    private const string Hook = """
        [Metaphor.Hooks.GameHook] public sealed class MenuHook : Metaphor.Hooks.IObserves3
        {
            private Reloaded.Hooks.Definitions.IHook<System.Action> hook;

            public void Observe(nint a, nint b, nint c)
            {
                BODY
            }
        }
        """;

    private static Mono.Cecil.AssemblyDefinition PlantedHook(string body) =>
        HookLayer(Hook.Replace("BODY", body, StringComparison.Ordinal));

    [Theory]
    [InlineData("Reloaded.Hooks.Definitions.IReloadedHooks hooks = null; hooks.CreateHook<System.Action>(() => { }, a);", "CreateHook")]
    [InlineData("hook.Activate();", "Activate")]
    public void AHookThatInstallsItselfIsAViolation(string body, string call)
    {
        var violation = Assert.Single(Rules.HooksPassThroughAdapters(PlantedHook(body)));

        Assert.Contains("MenuHook", violation, StringComparison.Ordinal);
        Assert.Contains($"calls {call}", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void AHookThatCallsTheHookingLibraryAtAllIsAViolation()
    {
        var violation = Assert.Single(Rules.HooksPassThroughAdapters(PlantedHook("_ = hook.OriginalFunction;")));

        Assert.Contains("calls get_OriginalFunction", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void AHookThatListsCallsButNeverTakesThemIsAViolation()
    {
        var assembly = HookLayer("""
            [Metaphor.Hooks.GameHook] public sealed class MenuHook : Metaphor.Hooks.IObserves3
            {
                public System.Collections.Generic.IReadOnlyList<string> Calls => ["menu_open"];
                public void Observe(nint a, nint b, nint c) { }
            }
            """);

        var violation = Assert.Single(Rules.HooksPassThroughAdapters(assembly));
        Assert.Contains("MenuHook lists Calls but has no UseCalls", violation, StringComparison.Ordinal);
    }

    [Theory]
    // An explicit implementation is named after its interface (Metaphor.Hooks.IGameHook.Calls).
    [InlineData("", false)]
    [InlineData("void Metaphor.Hooks.IGameHook.UseCalls(System.Collections.Generic.IReadOnlyDictionary<string, nint> calls) { }", true)]
    public void ExplicitlyImplementedCallsAndUseCallsAreMatched(string useCalls, bool passes)
    {
        var assembly = HookLayer($$"""
            namespace Metaphor.Hooks
            {
                public interface IGameHook
                {
                    System.Collections.Generic.IReadOnlyList<string> Calls { get; }
                    void UseCalls(System.Collections.Generic.IReadOnlyDictionary<string, nint> calls) { }
                }
            }
            [Metaphor.Hooks.GameHook]
            public sealed class MenuHook : Metaphor.Hooks.IGameHook, Metaphor.Hooks.IObserves3
            {
                System.Collections.Generic.IReadOnlyList<string> Metaphor.Hooks.IGameHook.Calls => ["menu_open"];
                {{useCalls}}
                public void Observe(nint a, nint b, nint c) { }
            }
            """);

        var violations = Rules.HooksPassThroughAdapters(assembly);

        if (passes)
        {
            Assert.Empty(violations);
        }
        else
        {
            Assert.Contains("MenuHook lists Calls but has no UseCalls", Assert.Single(violations), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("try { } catch (System.Exception) { }")]
    [InlineData("try { } catch { }")]
    [InlineData("try { } catch (System.Exception) when (a > 0) { }")]
    [InlineData("try { } catch when (a > 0) { }")]
    // Code nested in a hook (a lambda's closure) belongs to it.
    [InlineData("System.Action go = () => { try { } catch (System.Exception) { } }; go();")]
    public void AHookWithItsOwnCatchAllIsAViolation(string body)
    {
        var violation = Assert.Single(Rules.HooksPassThroughAdapters(PlantedHook(body)));

        Assert.Contains("MenuHook", violation, StringComparison.Ordinal);
        Assert.Contains("catches every exception", violation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("try { } catch (System.FormatException) { }")]
    [InlineData("try { } catch (System.FormatException) when (a > 0) { }")]
    public void CatchingOneKindOfExceptionPasses(string body) =>
        Assert.Empty(Rules.HooksPassThroughAdapters(PlantedHook(body)));

    [Theory]
    [InlineData("", "observes no shape")]
    [InlineData(": Metaphor.Hooks.IObserves3, Metaphor.Hooks.IObserves4", "observes more than one shape")]
    public void AHookMustObserveExactlyOneShape(string interfaces, string problem)
    {
        var assembly = HookLayer($$"""
            [Metaphor.Hooks.GameHook] public sealed class MenuHook {{interfaces}}
            {
                public void Observe(nint a, nint b, nint c) { }
                public void Observe(nint a, nint b, nint c, nint d) { }
            }
            """);

        var violation = Assert.Single(Rules.HooksPassThroughAdapters(assembly));
        Assert.Contains($"MenuHook {problem}", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAdapterOutsideAHookMayInstallAndGuard()
    {
        var assembly = HookLayer("""
            public sealed class PassThrough3
            {
                private Reloaded.Hooks.Definitions.IHook<System.Action> hook;

                public void Install(Reloaded.Hooks.Definitions.IReloadedHooks hooks, long address)
                {
                    hook = hooks.CreateHook<System.Action>(() => { try { } catch (System.Exception) { } }, address);
                    hook.Activate();
                }
            }
            """);

        Assert.Empty(Rules.HooksPassThroughAdapters(assembly));
    }

    [Fact]
    public void AHookThatOnlyObservesPasses() => Assert.Empty(Rules.HooksPassThroughAdapters(PlantedHook("")));

    private const string BehaviourChangingHook = """
        namespace Metaphor.Hooks
        {
            public interface IChangesBehaviour { void Activate(Reloaded.Hooks.Definitions.IReloadedHooks hooks, long address); }
        }
        [Metaphor.Hooks.GameHook] public sealed class PadHook : Metaphor.Hooks.IChangesBehaviour INTERFACES
        {
            private Reloaded.Hooks.Definitions.IHook<System.Action> hook;

            public void Activate(Reloaded.Hooks.Definitions.IReloadedHooks hooks, long address)
            {
                hook = hooks.CreateHook<System.Action>(() => { try { } catch (System.Exception) { } }, address);
                hook.Activate();
            }

            public void Observe(nint a, nint b, nint c) { }
        }
        """;

    [Fact]
    public void AHookThatChangesBehaviourMayInstallAndGuardItself()
    {
        // Rule 14's exception: written the long way, still under rules 8-11.
        var assembly = HookLayer(BehaviourChangingHook.Replace("INTERFACES", "", StringComparison.Ordinal));

        Assert.Empty(Rules.HooksPassThroughAdapters(assembly));
    }

    [Fact]
    public void AHookThatChangesBehaviourMustNotAlsoObserveAShape()
    {
        var assembly = HookLayer(BehaviourChangingHook.Replace("INTERFACES", ", Metaphor.Hooks.IObserves3",
            StringComparison.Ordinal));

        var violation = Assert.Single(Rules.HooksPassThroughAdapters(assembly));
        Assert.Contains("PadHook changes behaviour and observes a shape", violation, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void EveryHookPassesThroughAnAdapter(string name) =>
        Assert.Empty(Rules.HooksPassThroughAdapters(Scanned.Load(name)));
}
