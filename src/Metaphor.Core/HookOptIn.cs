namespace Metaphor.Core;

/// <summary>Whether a build installs a hook, before any build check or signature scan.</summary>
public enum HookInclusion
{
    /// <summary>The hook goes on to the build check and signature scan like any other, and counts as expected.</summary>
    Included,

    /// <summary>A development-only hook in a Release build: left out silently, and not expected.</summary>
    NotInThisBuild,

    /// <summary>An opt-in hook whose file isn't in the mod's folder: skipped with a log line, and not expected.</summary>
    NotOptedIn,
}

/// <summary>A dev opt-in whose file was in the mod's folder (#223).</summary>
/// <param name="Name">Its name in plain words, as the load message says it.</param>
/// <param name="Active">Whether every one of its hooks went in.</param>
public sealed record OptInState(string Name, bool Active);

/// <summary>What installing the hooks did: how many went in, and the state of each opt-in the run asked for.</summary>
public sealed record InstalledHooks(int Count, IReadOnlyList<OptInState> OptIns);

/// <summary>Decides which hooks a build installs: development-only hooks only in a Debug build, and an opt-in hook only
/// when its file is in the mod's folder, so a risky dev hook stays off unless a run asks for it.</summary>
public static class HookOptIn
{
    /// <summary>Whether to include a hook marked <paramref name="devOnly"/> with the opt-in file
    /// <paramref name="optInFile"/> (null when it needs none). <paramref name="exists"/> says whether a file of that name
    /// is in the mod's folder; it is asked only for a bare file name, so an opt-in never reaches outside the folder.
    /// </summary>
    public static HookInclusion Decide(bool devOnly, string? optInFile, bool devBuild, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        if (devOnly && !devBuild)
        {
            return HookInclusion.NotInThisBuild;
        }
        if (optInFile is null)
        {
            return HookInclusion.Included;
        }
        return IsBareFileName(optInFile) && exists(optInFile) ? HookInclusion.Included : HookInclusion.NotOptedIn;
    }

    /// <summary>The opt-ins whose files a run had, from each <paramref name="optedInHooks"/>: an included hook's opt-in
    /// name and whether it went in. An opt-in is active only when every one of its hooks went in (#223). In the order
    /// each name first appears.</summary>
    public static IReadOnlyList<OptInState> Summarize(IEnumerable<(string Name, bool WentIn)> optedInHooks) =>
        [.. optedInHooks.GroupBy(h => h.Name, StringComparer.Ordinal)
            .Select(g => new OptInState(g.Key, g.All(h => h.WentIn)))];

    /// <summary>Whether <paramref name="name"/> names a file directly in a folder: no separators, drive or parent.</summary>
    public static bool IsBareFileName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." &&
        name.IndexOfAny(['/', '\\', ':']) < 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
