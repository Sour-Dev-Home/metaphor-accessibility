namespace Metaphor.Core;

/// <summary>A hook as the loader sees it: its name, the game builds its <c>[GameHook]</c> names, the signature in
/// <c>hook-signatures.json</c> that finds the function it hooks, and the signatures of any game functions it calls.</summary>
public sealed record HookSpec(
    string Name, IReadOnlyList<string> GameBuilds, string SignatureName, IReadOnlyList<string>? Calls = null)
{
    /// <summary>The name of the hook it goes in with, or null for none: if either can't go in, neither does (#172).
    /// </summary>
    public string? Partner { get; init; }
}

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
/// exactly once in a scan that read everything; otherwise it does nothing and says why. Game globals (#300) are located
/// by the same rules, from the same scan.</summary>
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
        Func<IReadOnlyList<HookSignature>, ScanResult> scan) =>
        Plan(runningBuild, hooks, [], signatures, scan, _ => null).Hooks;

    /// <summary>As <see cref="Plan(string, IReadOnlyList{HookSpec}, IReadOnlyList{HookSignature}, Func{IReadOnlyList{HookSignature}, ScanResult})"/>,
    /// and one decision per global in <paramref name="globals"/> from the same scan (#300). A global is decided as a
    /// hook is, on its own: a global that can't be located leaves every hook as it was. One that can is located with
    /// <see cref="GlobalSpec.Locate"/>, reading its load's displacement with <paramref name="readInt32"/>, which is
    /// called only for globals whose signature matched exactly once.</summary>
    public static LoadPlan Plan(
        string runningBuild,
        IReadOnlyList<HookSpec> hooks,
        IReadOnlyList<GlobalSpec> globals,
        IReadOnlyList<HookSignature> signatures,
        Func<IReadOnlyList<HookSignature>, ScanResult> scan,
        Func<long, int?> readInt32)
    {
        ArgumentNullException.ThrowIfNull(runningBuild);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(readInt32);

        var byName = signatures.ToDictionary(signature => signature.Name);
        // A global is checked as a hook on its load's code is, so it gets the same build check and exactly-once match.
        var asHooks = globals.Select(global => new HookSpec(global.Name, global.GameBuilds, global.SignatureName)).ToList();
        var wanted = hooks.Concat(asHooks)
            .Where(hook => hook.GameBuilds.Contains(runningBuild))
            .SelectMany(Needs)
            .Select(name => byName.GetValueOrDefault(name))
            .OfType<HookSignature>()
            .Distinct()
            .ToList();
        var found = wanted.Count == 0 ? new ScanResult(new Dictionary<string, IReadOnlyList<long>>(), Complete: true) : scan(wanted);

        var alone = hooks.Select(hook => Decide(hook, runningBuild, byName, found)).ToList();
        var byHook = hooks.Zip(alone).GroupBy(h => h.First.Name).ToDictionary(g => g.Key, g => g.First().Second);
        return new(
            [.. hooks.Zip(alone, (hook, decision) => WithPartner(hook, decision, byHook))],
            [.. globals.Zip(asHooks, (global, spec) => Locate(global, Decide(spec, runningBuild, byName, found), readInt32))]);
    }

    private static GlobalDecision Locate(GlobalSpec global, HookDecision match, Func<long, int?> readInt32)
    {
        if (!match.Install)
        {
            return new(global.Name, null, match.WhyNot);
        }
        return global.Locate(match.Rva!.Value, readInt32) is { } rva
            ? new(global.Name, rva, null)
            : new(global.Name, null, "its load couldn't be read");
    }

    /// <summary>A hook with a partner goes in only if the partner is planned too and can go in, so neither is left in
    /// the game without the other (#172).</summary>
    private static HookDecision WithPartner(
        HookSpec hook, HookDecision decision, Dictionary<string, HookDecision> byHook)
    {
        if (!decision.Install || hook.Partner is not { } partner)
        {
            return decision;
        }
        if (!byHook.TryGetValue(partner, out var other))
        {
            return new(hook.Name, null, $"its partner {partner} isn't being loaded");
        }
        return other.Install ? decision : new(hook.Name, null, $"its partner {partner} can't go in ({other.WhyNot})");
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
