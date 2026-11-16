using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core;

/// <summary>Installs what <see cref="HookPlanner"/> approves (#87): it plans, pairs each decision with its hook by
/// position, keeps each hook alive before it goes in, isolates each failure, and counts what went in. The hook loader
/// supplies the hooks, the scan and the call that installs one; this knows nothing of the hooking library.</summary>
public static class HookInstallation
{
    /// <summary>Plans <paramref name="hooks"/> for <paramref name="runningBuild"/> as <see cref="HookPlanner.Plan"/>
    /// does, then installs each approved hook with the function <paramref name="installer"/> returns. That is asked
    /// for once, just before the first install, and never if nothing is approved; if it throws, so does this. A hook
    /// is paired with <c>null</c> when it wasn't created, which only a hook for another build may be. Writes one line
    /// per hook to <paramref name="log"/>. Returns how many went in, and every hook handed to the game, which the
    /// caller keeps alive.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "One hook failing must not stop the others or reach the game.")]
    public static (int Loaded, IReadOnlyList<THook> Kept) Run<THook>(
        string runningBuild,
        IReadOnlyList<(HookSpec Spec, THook? Hook)> hooks,
        IReadOnlyList<HookSignature> signatures,
        Func<IReadOnlyList<HookSignature>, ScanResult> scan,
        Func<Action<THook, HookDecision>> installer,
        Action<string> log)
        where THook : class
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(installer);
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

        var plan = HookPlanner.Plan(runningBuild, [.. hooks.Select(h => h.Spec)], signatures, scan);
        Action<THook, HookDecision>? install = null;
        var kept = new List<THook>();
        var loaded = 0;
        foreach (var (decision, (_, hook)) in plan.Zip(hooks))
        {
            if (!decision.Install || hook is null)
            {
                Log($"hook {decision.Name} skipped: {decision.WhyNot}");
                continue;
            }
            install ??= installer();
            // Referenced before it goes live: the game calls back into the hook's managed delegate, and a collected
            // delegate makes .NET fail-fast the whole process (seen on the first in-game run of #55's listening hooks:
            // APPCRASH in coreclr.dll, 0x80131623, seconds after loading). Kept even if installing fails, since it
            // may have gone live before failing.
            kept.Add(hook);
            try
            {
                install(hook, decision);
            }
            catch (Exception e)
            {
                Log($"hook {decision.Name} failed to install: {e.GetType().Name}");
                continue;
            }
            loaded++;
            Log($"hook {decision.Name} installed at module offset 0x{decision.Rva:x}");
        }
        return (loaded, kept);
    }
}
