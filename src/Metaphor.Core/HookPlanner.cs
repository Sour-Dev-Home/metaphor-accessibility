namespace Metaphor.Core;

/// <summary>A hook as the loader sees it: its name, the game builds its <c>[GameHook]</c> names, and the signature in
/// <c>hook-signatures.json</c> that finds its function.</summary>
public sealed record HookSpec(string Name, IReadOnlyList<string> GameBuilds, string SignatureName);

/// <summary>Whether to install one hook: at <paramref name="Rva"/> (relative to the game module), or not, and why.</summary>
public sealed record HookDecision(string Name, long? Rva, string? WhyNot)
{
    public bool Install => WhyNot is null;
}

/// <summary>Decides which hooks the loader installs (#39). Ground rule 2: a hook goes in only on a game build its
/// <c>[GameHook]</c> names, and only where its signature matches exactly once; otherwise it does nothing and says
/// why.</summary>
public static class HookPlanner
{
    /// <summary>One decision per hook in <paramref name="hooks"/>, for the game build <paramref name="runningBuild"/>,
    /// with <paramref name="signatures"/> from the signatures file. <paramref name="scan"/> finds every match of each
    /// signature it's given, by name, as RVAs; it's called once, and only with the signatures of hooks that passed the
    /// build check.</summary>
    public static IReadOnlyList<HookDecision> Plan(
        string runningBuild,
        IReadOnlyList<HookSpec> hooks,
        IReadOnlyList<HookSignature> signatures,
        Func<IReadOnlyList<HookSignature>, ScanResult> scan)
    {
        ArgumentNullException.ThrowIfNull(runningBuild);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(scan);

        var byName = signatures.ToDictionary(signature => signature.Name);
        var onThisBuild = hooks.Where(hook => hook.GameBuilds.Contains(runningBuild)).ToList();
        var wanted = onThisBuild
            .Select(hook => byName.GetValueOrDefault(hook.SignatureName))
            .OfType<HookSignature>()
            .Distinct()
            .ToList();
        var found = wanted.Count == 0 ? new ScanResult(new Dictionary<string, IReadOnlyList<long>>(), Complete: true) : scan(wanted);

        return [.. hooks.Select(hook => Decide(hook, runningBuild, byName, found))];
    }

    private static HookDecision Decide(
        HookSpec hook, string runningBuild, Dictionary<string, HookSignature> byName,
        ScanResult found)
    {
        if (!hook.GameBuilds.Contains(runningBuild))
        {
            return new(hook.Name, null, $"not checked on game build {runningBuild[..Math.Min(8, runningBuild.Length)]}");
        }
        if (!byName.ContainsKey(hook.SignatureName))
        {
            return new(hook.Name, null, $"no signature {hook.SignatureName} in hook-signatures.json");
        }
        if (!found.Complete)
        {
            return new(hook.Name, null, "the scan couldn't read all of the game's code");
        }
        var matches = found.Matches.GetValueOrDefault(hook.SignatureName) ?? [];
        return matches.Count switch
        {
            1 => new(hook.Name, matches[0], null),
            0 => new(hook.Name, null, $"signature {hook.SignatureName} matched nowhere"),
            var n => new(hook.Name, null, $"signature {hook.SignatureName} matched {n} times"),
        };
    }
}
