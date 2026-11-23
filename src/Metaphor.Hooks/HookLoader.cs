using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Metaphor.Core;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;

namespace Metaphor.Hooks;

/// <summary>What every hook is: a <c>[GameHook]</c> class that names its signature (#39) and observes each call through
/// exactly one shape interface (<c>IObserves…</c>), whose <see cref="PassThrough"/> adapter the loader hooks the
/// function with (#88, architecture rule 14). It needs a public constructor taking only <see cref="HookServices"/>,
/// which is how it gets everything it uses (architecture rule 12).</summary>
internal interface IGameHook
{
    /// <summary>The name of its signature in <c>hook-signatures.json</c>.</summary>
    string SignatureName { get; }

    /// <summary>The signatures of game functions it calls, by name. None for most hooks.</summary>
    IReadOnlyList<string> Calls => [];

    /// <summary>Hands the hook the address of each of <see cref="Calls"/>, just before its function is hooked. Called
    /// at most once, only when the running game build is one the hook's <c>[GameHook]</c> names, and its signature and
    /// each of <see cref="Calls"/> matched exactly once.</summary>
    void UseCalls(IReadOnlyDictionary<string, nint> calls)
    {
    }

    /// <summary>Tells a hook whose <c>[GameHook]</c> names a <c>Partner</c> that both of them went in. Called at most
    /// once, after every hook is installed, and never if either didn't go in.</summary>
    void PairInstalled()
    {
    }
}

/// <summary>The only way a hook goes into the game (#39, ground rule 2). <see cref="HookPlanner"/> decides from the
/// running build and a signature scan, and <see cref="HookInstallation"/> installs what it approves and reports the rest.
/// The same scan locates the game globals <see cref="HookServices.Globals"/> read (#300), before any hook goes in.
/// This finds and creates the hooks, reads the game's code for the scan, and makes the one call that installs a hook,
/// through Reloaded-II's shared hooks library (ADR 0006). The architecture rule <c>PatchingNeedsGameHook</c> stops code patching the game
/// any other way.</summary>
internal static class HookLoader
{
    /// <summary>Every hook handed to the game, with its adapter, kept alive until the process ends. Only the loader's
    /// thread adds to it.</summary>
    private static readonly List<PassThrough> Installed = [];

    private static bool IsDevBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Every <c>[GameHook]</c> type, with whether this build includes it for the mod in
    /// <paramref name="modFolder"/> (<see cref="HookOptIn"/>): development-only hooks are left out of Release builds,
    /// which players get, and opt-in hooks unless their file is in the mod's folder. Decided once, so the count of
    /// expected hooks and what <see cref="Install"/> tries always agree.</summary>
    public static IReadOnlyList<(Type Type, GameHookAttribute Hook, HookInclusion Inclusion)> Candidates(string modFolder) =>
        [.. LoadableTypes()
            .Select(type => (Type: type, Hook: type.GetCustomAttribute<GameHookAttribute>(inherit: false)))
            .Where(c => c.Hook is not null)
            .Select(c => (c.Type, c.Hook!, HookOptIn.Decide(
                c.Hook!.DevOnly, c.Hook.OptInFile, IsDevBuild, name => File.Exists(Path.Combine(modFolder, name)))))];

    /// <summary>How many of <paramref name="candidates"/> are expected to go in: those included, whether or not they
    /// can be created.</summary>
    public static int Expected(IReadOnlyList<(Type Type, GameHookAttribute Hook, HookInclusion Inclusion)> candidates) =>
        candidates.Count(c => c.Inclusion == HookInclusion.Included);

    /// <summary>Installs the included <paramref name="candidates"/> approved for <paramref name="runningBuild"/> and
    /// returns how many went in, with each opt-in whose file was there and whether all its hooks went in (#223). Each hook is created with <paramref name="services"/>. Writes one line per hook to
    /// <paramref name="log"/>, including each opt-in hook left out, or to <paramref name="logError"/> for a hook that
    /// failed to install (#204).</summary>
    /// <exception cref="InvalidOperationException">A hook should go in, but Reloaded-II gave no hooks controller.</exception>
    [SuppressMessage("Design", "CA1031", Justification = "A logging failure must not stop hooks going in.")]
    public static InstalledHooks Install(
        string runningBuild,
        IReadOnlyList<(Type Type, GameHookAttribute Hook, HookInclusion Inclusion)> candidates,
        IModLoader loader,
        HookServices services,
        Action<string> log,
        Action<string> logError)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(logError);
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
        var hooks = new List<(HookSpec Spec, PassThrough? Hook)>();
        foreach (var (type, attribute, inclusion) in candidates)
        {
            if (inclusion == HookInclusion.NotOptedIn)
            {
                Log($"hook {type.Name} skipped: opt-in, and {attribute.OptInFile} isn't in the mod's folder");
                continue;
            }
            if (inclusion != HookInclusion.Included)
            {
                continue;
            }
            var builds = attribute.GameBuilds;
            if (!builds.Contains(runningBuild))
            {
                hooks.Add((new HookSpec(type.Name, builds, ""), null));
                continue;
            }
            if (type.GetConstructor([typeof(HookServices)]) is null)
            {
                Log($"hook {type.Name} skipped: it has no public constructor taking HookServices");
                continue;
            }
            if (Activator.CreateInstance(type, services) is not IGameHook hook)
            {
                Log($"hook {type.Name} skipped: it isn't an IGameHook");
                continue;
            }
            if (PassThrough.For(hook) is not { } adapter)
            {
                Log($"hook {type.Name} skipped: it observes no shape the loader has an adapter for");
                continue;
            }
            hooks.Add((new HookSpec(type.Name, builds, hook.SignatureName, hook.Calls) { Partner = attribute.Partner }, adapter));
        }

        var readers = services.Globals;
        var (moduleBase, regions) = hooks.Any(h => h.Hook is not null) || readers.Count > 0
            ? ModuleMemoryReader.MainModuleRegions()
            : (0, []);
        var missing = new List<MissingGlobal>();
        var (loaded, kept, wentIn) = HookInstallation.Run(
            runningBuild, hooks, HookSignatures.All,
            wanted => ChunkedScan.FindAll(regions, (rva, buffer) => ModuleMemoryReader.ReadAt(moduleBase, rva, buffer), wanted),
            () => ReloadedInstall(loader, moduleBase), log, logError, adapter => adapter.Hook.PairInstalled(),
            new GlobalLookup(
                [.. readers.Select(reader => reader.Global)],
                rva => ModuleMemoryReader.ReadInt32(moduleBase + (nint)rva),
                decision => UseGlobal(readers, decision, moduleBase, missing)),
            Log);
        // Referenced for the life of the game (HookInstallation.Run says why).
        Installed.AddRange(kept);
        // A hook skipped above, such as one without the constructor, isn't in wentIn, so its opt-in isn't active.
        return new(loaded, HookOptIn.Summarize(candidates
            .Where(c => c.Inclusion == HookInclusion.Included && c.Hook.OptInFile is not null)
            .Select(c => (c.Hook.OptInName ?? c.Hook.OptInFile!, wentIn.Contains(c.Type.Name)))))
        {
            MissingGlobals = missing,
        };
    }

    /// <summary>Hands a located global to the reader that declared it (#300); one that wasn't located leaves its reader
    /// unknown and is listed as missing, for the load message.</summary>
    private static void UseGlobal(
        IReadOnlyList<IReadsGlobal> readers, GlobalDecision decision, nint moduleBase, List<MissingGlobal> missing)
    {
        var reader = readers.First(reader => reader.Global.Name == decision.Name);
        if (decision.Found)
        {
            reader.Locate(moduleBase + (nint)decision.Rva!.Value);
        }
        else
        {
            missing.Add(new(decision.Name, reader.Global.TurnsOff));
        }
    }

    /// <summary>Installs one approved hook, through its adapter, with Reloaded-II's hooks controller, fetched now, once a
    /// hook should go in.</summary>
    private static Action<PassThrough, HookDecision> ReloadedInstall(IModLoader loader, nint moduleBase)
    {
        if (!(loader.GetController<IReloadedHooks>()?.TryGetTarget(out var reloadedHooks) ?? false))
        {
            // As the template gets it (docs/raw-sources/reloaded-ii-shared-hooks-library-2026-11-15.md).
            throw new InvalidOperationException("Reloaded-II gave no IReloadedHooks controller (reloaded.sharedlib.hooks)");
        }
        return (adapter, decision) => adapter.Install(
            reloadedHooks, moduleBase + (nint)decision.Rva!.Value,
            decision.Calls.ToDictionary(call => call.Key, call => moduleBase + (nint)call.Value));
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
