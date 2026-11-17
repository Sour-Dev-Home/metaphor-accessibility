namespace Metaphor.Core;

// Issue #213: what the development-only screen class listener decodes, kept here so it is tested without the game. The
// layouts are MSVC's x64 run-time type information and our reading of the exe on one build
// (docs/raw-sources/screen-class-link-2026-11-17.md); which link names a screen is a lead until a run shows it.

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

/// <summary>The screen class candidates for one cursor input (#213): the manager and its class, the choice that last
/// handed it out through its cursor getter and that choice's class (the forward link), and the manager's own fields that
/// point at named objects (a back link, if it has one). Addresses and class names only, no game text.</summary>
public sealed record ScreenClassSeen(
    long Manager, string? ManagerClass, long? Owner, string? OwnerClass, IReadOnlyList<(int Offset, string Name)> Fields)
{
    /// <summary>The log's form: <c>mgr=0x… mgrClass=… owner=0x…|none ownerClass=… fields=+0x…:Name,…|none</c>.</summary>
    public override string ToString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"mgr=0x{Manager:x} mgrClass={ManagerClass ?? "?"} owner={(Owner is { } owner ? $"0x{owner:x}" : "none")} ownerClass={OwnerClass ?? "?"} fields={(Fields.Count == 0 ? "none" : string.Join(',', Fields.Select(f => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"+0x{f.Offset:x}:{f.Name}"))))}");
}

/// <summary>Looks through an object whose layout is unknown for fields that point at objects with a class name (#213):
/// a back pointer from a cursor manager to the choice that owns it would show up here, if the manager keeps one.
/// </summary>
public static class NamedFields
{
    // User-mode addresses on x64 Windows end below 0x7FFF'FFFF'0000; the first 64 KiB are never mapped.
    // https://learn.microsoft.com/windows/win32/memory/virtual-address-space
    private const long LowestAddress = 0x10000;
    private const long HighestAddress = 0x7FFF_FFFE_FFFF;

    /// <summary>Each 8-byte field of <paramref name="fields"/> whose target <paramref name="classNameOf"/> names, by
    /// offset, in order.</summary>
    public static IReadOnlyList<(int Offset, string Name)> Of(ReadOnlySpan<byte> fields, Func<long, string?> classNameOf)
    {
        ArgumentNullException.ThrowIfNull(classNameOf);
        var named = new List<(int, string)>();
        for (var at = 0; at + sizeof(long) <= fields.Length; at += sizeof(long))
        {
            var pointer = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(fields[at..]);
            if (pointer is >= LowestAddress and <= HighestAddress && classNameOf(pointer) is { } name)
            {
                named.Add((at, name));
            }
        }
        return named;
    }
}

/// <summary>Which choice (screen object) last handed out each cursor manager through its cursor getter (#213): the
/// forward link, choice to manager, turned around so a cursor input can name its screen. Safe from any game thread.
/// </summary>
public sealed class CursorOwners
{
    /// <summary>The most managers remembered; past it the map forgets them all and starts again, as managers are made
    /// per visit (docs/raw-sources/menu-hook-counts-2026-11-17.md).</summary>
    public const int MaxManagers = 256;

    private readonly Lock gate = new();
    private readonly Dictionary<long, long> ownerByManager = [];

    /// <summary>Records that <paramref name="owner"/> handed out <paramref name="manager"/>.</summary>
    public void Saw(long owner, long manager)
    {
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
    public long? OwnerOf(long manager)
    {
        lock (gate)
        {
            return ownerByManager.TryGetValue(manager, out var owner) ? owner : null;
        }
    }
}
