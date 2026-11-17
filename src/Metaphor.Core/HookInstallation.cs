using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core;

/// <summary>Installs what <see cref="HookPlanner"/> approves (#87): it plans, pairs each decision with its hook by
/// position, keeps each hook alive before it goes in, isolates each failure, and counts what went in. The hook loader
/// supplies the hooks, the scan and the call that installs one; this knows nothing of the hooking library.</summary>
public static class HookInstallation
{
    /// <summary>Plans <paramref name="hooks"/> for <paramref name="runningBuild"/> as <see cref="HookPlanner.Plan"/>
    /// does, then installs each approved hook with the function <paramref name="createInstaller"/> returns. That is
    /// called once, just before the first install, and never if nothing is approved; if it throws, so does this. A
    /// hook is paired with <c>null</c> when it wasn't created, which only a hook for another build may be. Writes one
    /// line per hook to <paramref name="log"/>, or to <paramref name="logError"/>, when given, for a hook that failed
    /// to install: the line a native crash report needs (#204). Returns how many went in, and every hook handed to the
    /// game, which the caller keeps alive.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "One hook failing must not stop the others or reach the game.")]
    public static (int Loaded, IReadOnlyList<THook> Kept) Run<THook>(
        string runningBuild,
        IReadOnlyList<(HookSpec Spec, THook? Hook)> hooks,
        IReadOnlyList<HookSignature> signatures,
        Func<IReadOnlyList<HookSignature>, ScanResult> scan,
        Func<Action<THook, HookDecision>> createInstaller,
        Action<string> log,
        Action<string>? logError = null)
        where THook : class
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(createInstaller);
        ArgumentNullException.ThrowIfNull(log);

        // A logging failure must not make the count, or the status, disagree with what's in the game.
        void Log(string line) => Safely(log, line);
        void LogError(string line) => Safely(logError ?? log, line);
        static void Safely(Action<string> write, string line)
        {
            try
            {
                write(line);
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
            install ??= createInstaller();
            // Referenced before it goes live: rule 11 in docs/coding-standards.md, and the coreclr fail-fast in
            // docs/raw-sources/message-listening-run-2026-11-16.md. Kept even if installing fails, since it may have
            // gone live before failing.
            kept.Add(hook);
            try
            {
                install(hook, decision);
            }
            catch (Exception e)
            {
                LogError($"hook {decision.Name} failed to install: {e.GetType().Name}");
                continue;
            }
            loaded++;
            Log($"hook {decision.Name} installed at module offset 0x{decision.Rva:x}");
        }
        return (loaded, kept);
    }
}
