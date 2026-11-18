namespace Metaphor.Core;

// Issue #213: the hook layer's screen class lookup keeps this map, kept here so it is tested without the game. The
// forward link, choice to manager, was confirmed in a run on build 548ddc95
// (docs/raw-sources/screen-class-session-2026-11-17.md).

/// <summary>Which choice (screen object) last handed out each cursor manager through its cursor getter (#213): the
/// forward link, choice to manager, turned around so a cursor input can name its screen. Safe from any game thread.
/// <see cref="Saw"/> may run every frame for every open screen, so a pair it already knows costs one lock-free lookup.
/// </summary>
public sealed class CursorOwners
{
    /// <summary>The most managers remembered; past it the map forgets them all and starts again, as managers are made
    /// per visit (docs/raw-sources/menu-hook-counts-2026-11-17.md).</summary>
    public const int MaxManagers = 256;

    private readonly Lock gate = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, long> ownerByManager = new();

    /// <summary>Records that <paramref name="owner"/> handed out <paramref name="manager"/>.</summary>
    public void Saw(long owner, long manager)
    {
        if (ownerByManager.TryGetValue(manager, out var known) && known == owner)
        {
            return;
        }
        lock (gate)
        {
            if (ownerByManager.Count >= MaxManagers && !ownerByManager.ContainsKey(manager))
            {
                ownerByManager.Clear();
            }
            ownerByManager[manager] = owner;
        }
    }

    /// <summary>The choice that last handed out <paramref name="manager"/>, or null if none has been seen to.</summary>
    public long? OwnerOf(long manager) => ownerByManager.TryGetValue(manager, out var owner) ? owner : null;
}
