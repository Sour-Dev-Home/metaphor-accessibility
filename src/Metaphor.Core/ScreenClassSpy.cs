namespace Metaphor.Core;

// Issues #213 and #208: what the hook layer's screen class lookup decodes, kept here so it is tested without the game.
// The layouts are MSVC's x64 run-time type information and our reading of the exe on one build
// (docs/raw-sources/screen-class-link-2026-11-17.md); the forward link, choice to manager, was confirmed in a run on
// build 548ddc95 (docs/raw-sources/screen-class-session-2026-11-17.md).

/// <summary>Names an object's class from the run-time type information (RTTI) its vtable points at: the 8 bytes before
/// the vtable hold a complete object locator, whose signature is <c>1</c> on x64, whose <c>+0x0C</c> holds the type
/// descriptor's module offset and whose <c>+0x14</c> its own; the decorated name starts 16 bytes into the descriptor
/// (method: docs/raw-sources/ui-focus-candidates-2026-11-16.md). Class names are identifiers, not game text.</summary>
public static class RttiName
{
    /// <summary>The class of the object at <paramref name="instance"/> as its decorated name without the <c>.?AV</c>
    /// (or <c>.?AU</c>) prefix and the closing <c>@@</c>, such as <c>TitleMenuChoice@ope@ui@app</c>; null when any step
    /// is unreadable or leaves the module.</summary>
    public static string? Of(
        long instance, long moduleBase, long moduleSize, Func<long, long?> readPointer, Func<long, int?> readInt32,
        Func<long, string?> readAnsi)
    {
        ArgumentNullException.ThrowIfNull(readPointer);
        ArgumentNullException.ThrowIfNull(readInt32);
        ArgumentNullException.ThrowIfNull(readAnsi);
        bool InModule(long address) => address >= moduleBase && address - moduleBase < moduleSize;
        if (readPointer(instance) is not { } vtable || !InModule(vtable) ||
            readPointer(vtable - 8) is not { } locator || !InModule(locator) ||
            readInt32(locator) != 1 || readInt32(locator + 0x14) != locator - moduleBase ||
            readInt32(locator + 0x0C) is not { } descriptorRva || !InModule(moduleBase + descriptorRva) ||
            readAnsi(moduleBase + descriptorRva + 0x10) is not { } decorated)
        {
            return null;
        }
        return decorated.StartsWith(".?AV", StringComparison.Ordinal) || decorated.StartsWith(".?AU", StringComparison.Ordinal)
            ? decorated[4..^(decorated.EndsWith("@@", StringComparison.Ordinal) ? 2 : 0)]
            : null;
    }
}

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
