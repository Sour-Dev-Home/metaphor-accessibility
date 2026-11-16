using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Metaphor.Core;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;

namespace Metaphor.Hooks;

/// <summary>What every hook is: a <c>[GameHook]</c> class that names its signature and installs itself at the address
/// the loader found (#39). It needs a public parameterless constructor.</summary>
internal interface IGameHook
{
    /// <summary>The name of its signature in <c>hook-signatures.json</c>.</summary>
    string SignatureName { get; }

    /// <summary>The signatures of game functions it calls, by name. None for most hooks.</summary>
    IReadOnlyList<string> Calls => [];

    /// <summary>Hooks the function at <paramref name="address"/>. Called at most once, only when the running game
    /// build is one the hook's <c>[GameHook]</c> names, and its signature and each of <see cref="Calls"/> matched
    /// exactly once; <paramref name="calls"/> has the address of each of those.</summary>
    void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls);
}

/// <summary>The only way a hook goes into the game (#39, ground rule 2). <see cref="HookPlanner"/> decides from the
/// running build and a signature scan; this installs what it approves, through Reloaded-II's shared hooks library
/// (ADR 0006), and reports the rest. The architecture rule <c>PatchingNeedsGameHook</c> stops code patching the game
/// any other way.</summary>
internal static class HookLoader
{
    /// <summary>Every hook handed to the game, kept alive until the process ends. Only the loader's thread adds to it.</summary>
    private static readonly List<IGameHook> Installed = [];

    private static bool IsDevBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Every <c>[GameHook]</c> type this build installs, whether or not it can be created: development-only
    /// hooks are left out of Release builds, which players get.</summary>
    public static IReadOnlyList<Type> HookTypes() =>
        [.. LoadableTypes().Where(type =>
            type.GetCustomAttribute<GameHookAttribute>(inherit: false) is { } hook && (IsDevBuild || !hook.DevOnly))];

    /// <summary>Installs the hooks approved for <paramref name="runningBuild"/> and returns how many went in. Writes
    /// one line per hook to <paramref name="log"/>.</summary>
    /// <exception cref="InvalidOperationException">A hook should go in, but Reloaded-II gave no hooks controller.</exception>
    [SuppressMessage("Design", "CA1031", Justification = "One hook failing must not stop the others or reach the game.")]
    public static int Install(string runningBuild, IModLoader loader, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(log);
        // A logging failure must not make the count, or the status, disagree with what's in the game.
        void Log(string line)
        {
            try
            {
                log(line);
            }
            catch (Exception)
            {
            }
        }

        // The build check comes from the attribute first: a hook for another build isn't even created.
        var hooks = new List<(HookSpec Spec, IGameHook? Hook)>();
        foreach (var type in HookTypes())
        {
            var builds = type.GetCustomAttribute<GameHookAttribute>()!.GameBuilds;
            if (!builds.Contains(runningBuild))
            {
                hooks.Add((new HookSpec(type.Name, builds, ""), null));
                continue;
            }
            if (Activator.CreateInstance(type) is not IGameHook hook)
            {
                Log($"hook {type.Name} skipped: it isn't an IGameHook");
                continue;
            }
            hooks.Add((new HookSpec(type.Name, builds, hook.SignatureName, hook.Calls), hook));
        }

        var (moduleBase, regions) = hooks.Any(h => h.Hook is not null) ? ModuleMemoryReader.MainModuleRegions() : (0, []);
        var plan = HookPlanner.Plan(runningBuild, [.. hooks.Select(h => h.Spec)], HookSignatures.All, wanted =>
            ChunkedScan.FindAll(regions, (rva, buffer) => ModuleMemoryReader.ReadAt(moduleBase, rva, buffer), wanted));

        IReloadedHooks? reloadedHooks = null;
        var loaded = 0;
        foreach (var (decision, (_, hook)) in plan.Zip(hooks))
        {
            if (!decision.Install || hook is null)
            {
                Log($"hook {decision.Name} skipped: {decision.WhyNot}");
                continue;
            }
            if (reloadedHooks is null && !(loader.GetController<IReloadedHooks>()?.TryGetTarget(out reloadedHooks) ?? false))
            {
                // As the template gets it (docs/raw-sources/reloaded-ii-shared-hooks-library-2026-11-15.md).
                throw new InvalidOperationException("Reloaded-II gave no IReloadedHooks controller (reloaded.sharedlib.hooks)");
            }
            // Referenced for the life of the game, before it goes live: the game calls back into the hook's managed
            // delegate, and a collected delegate makes .NET fail-fast the whole process (seen on the first in-game
            // run of #55's listening hooks: APPCRASH in coreclr.dll, 0x80131623, seconds after loading). Kept even if
            // installing fails, since it may have gone live before failing.
            Installed.Add(hook);
            try
            {
                hook.Install(
                    reloadedHooks!, moduleBase + (nint)decision.Rva!.Value,
                    decision.Calls.ToDictionary(call => call.Key, call => moduleBase + (nint)call.Value));
            }
            catch (Exception e)
            {
                Log($"hook {decision.Name} failed to install: {e.GetType().Name}");
                continue;
            }
            loaded++;
            Log($"hook {decision.Name} installed at module offset 0x{decision.Rva:x}");
        }
        return loaded;
    }

    private static IEnumerable<Type> LoadableTypes()
    {
        try
        {
            return typeof(HookLoader).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }
}
