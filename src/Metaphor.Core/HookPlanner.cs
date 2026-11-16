namespace Metaphor.Core;

/// <summary>A hook as the loader sees it: its name, the game builds its <c>[GameHook]</c> names, the signature in
/// <c>hook-signatures.json</c> that finds the function it hooks, and the signatures of any game functions it calls.</summary>
public sealed record HookSpec(
    string Name, IReadOnlyList<string> GameBuilds, string SignatureName, IReadOnlyList<string>? Calls = null);

/// <summary>Whether to install one hook: at <paramref name="Rva"/> (relative to the game module), or not, and why.</summary>
public sealed record HookDecision(string Name, long? Rva, string? WhyNot)
{
    private static readonly IReadOnlyDictionary<string, long> NoCalls = new Dictionary<string, long>();

    public bool Install => WhyNot is null;

    /// <summary>Where each game function the hook calls is, by signature name, as RVAs.</summary>
    public IReadOnlyDictionary<string, long> Calls { get; init; } = NoCalls;
}

/// <summary>Decides which hooks the loader installs (#39). Ground rule 2: a hook goes in only on a game build its
/// <c>[GameHook]</c> names, and only where its signature, and the signature of every game function it calls, matches
/// exactly once in a scan that read everything; otherwise it does nothing and says why.</summary>
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
        var wanted = hooks
            .Where(hook => hook.GameBuilds.Contains(runningBuild))
            .SelectMany(Needs)
            .Select(name => byName.GetValueOrDefault(name))
            .OfType<HookSignature>()
            .Distinct()
            .ToList();
        var found = wanted.Count == 0 ? new ScanResult(new Dictionary<string, IReadOnlyList<long>>(), Complete: true) : scan(wanted);

        return [.. hooks.Select(hook => Decide(hook, runningBuild, byName, found))];
    }

    /// <summary>Every signature a hook needs: the one it hooks first, then the ones it calls.</summary>
    private static IEnumerable<string> Needs(HookSpec hook) => [hook.SignatureName, .. hook.Calls ?? []];

    private static HookDecision Decide(
        HookSpec hook, string runningBuild, Dictionary<string, HookSignature> byName, ScanResult found)
    {
        if (!hook.GameBuilds.Contains(runningBuild))
        {
            return new(hook.Name, null, $"not checked on game build {runningBuild[..Math.Min(8, runningBuild.Length)]}");
        }
        if (Needs(hook).FirstOrDefault(name => !byName.ContainsKey(name)) is { } missing)
        {
            return new(hook.Name, null, $"no signature {missing} in hook-signatures.json");
        }
        if (!found.Complete)
        {
            return new(hook.Name, null, "the scan couldn't read all of the game's code");
        }

        var rvas = new Dictionary<string, long>();
        foreach (var name in Needs(hook))
        {
            var matches = found.Matches.GetValueOrDefault(name) ?? [];
            if (matches.Count != 1)
            {
                return new(hook.Name, null, matches.Count == 0
                    ? $"signature {name} matched nowhere"
                    : $"signature {name} matched {matches.Count} times");
            }
            rvas[name] = matches[0];
        }
        var calls = (hook.Calls ?? []).ToDictionary(name => name, name => rvas[name]);
        return calls.Count == 0
            ? new(hook.Name, rvas[hook.SignatureName], null)
            : new(hook.Name, rvas[hook.SignatureName], null) { Calls = calls };
    }
}
