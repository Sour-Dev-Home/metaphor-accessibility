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

    /// <summary>The class a destroyed choice's freed memory reads as: its destructor leaves the abstract base's vtable, so
    /// no live owner has it (docs/findings/stale-cursor-owner-names-a-reused-manager.md, build 548ddc95).</summary>
    public const string DestroyedOwnerClass = "SingleChoiceBase@ope@ui@app";

    /// <summary>The class of <paramref name="manager"/>'s owner, read by <paramref name="classOf"/> from the owner's
    /// address, or null when no owner was seen or its class is unreadable. An owner that now reads
    /// <see cref="DestroyedOwnerClass"/> is gone and its manager's address reused (#732): it is forgotten, so the new
    /// screen on that manager isn't named for the dead one.</summary>
    public string? ClassOf(long manager, Func<long, string?> classOf)
    {
        ArgumentNullException.ThrowIfNull(classOf);
        if (OwnerOf(manager) is not { } owner || classOf(owner) is not { } name)
        {
            return null;
        }
        if (name != DestroyedOwnerClass)
        {
            return name;
        }
        lock (gate)
        {
            // Only the owner just read: another choice may have taken the manager since.
            if (ownerByManager.TryGetValue(manager, out var current) && current == owner)
            {
                ownerByManager.TryRemove(manager, out _);
            }
        }
        return null;
    }

    /// <summary>The choice that last handed out <paramref name="manager"/>, or null if none has been seen to.</summary>
    public long? OwnerOf(long manager) => ownerByManager.TryGetValue(manager, out var owner) ? owner : null;
}
