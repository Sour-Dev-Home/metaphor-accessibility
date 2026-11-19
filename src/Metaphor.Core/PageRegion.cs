namespace Metaphor.Core;

/// <summary>One region of pages as Windows' <c>VirtualQuery</c> reports it: where it starts, how long it is, its state and
/// its protection. Plain values, so the hook layer queries and this decides what is safe to read. Values and meanings
/// from docs/raw-sources/windows-guard-pages-and-virtualquery-2026-11-19.md.</summary>
public readonly record struct PageRegion(long Base, long Size, uint State, uint Protect)
{
    /// <summary><c>MEM_COMMIT</c>: pages with storage behind them. <c>Protect</c> is undefined for any other state.</summary>
    public const uint MemCommit = 0x1000;

    /// <summary><c>PAGE_GUARD</c>: the first access to such a page fails and clears the guard, and Microsoft says the same
    /// of a system service that touches one.</summary>
    public const uint PageGuard = 0x100;

    /// <summary><c>PAGE_READONLY</c>, <c>PAGE_READWRITE</c>, <c>PAGE_WRITECOPY</c>, <c>PAGE_EXECUTE_READ</c>,
    /// <c>PAGE_EXECUTE_READWRITE</c> and <c>PAGE_EXECUTE_WRITECOPY</c>.</summary>
    public const uint ReadableProtections = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80;

    /// <summary>Committed, readable and not a guard page, so a read neither faults nor changes the page.</summary>
    public bool IsReadable => State == MemCommit && (Protect & PageGuard) == 0 && (Protect & ReadableProtections) != 0;

    /// <summary>Whether all <paramref name="count"/> bytes from <paramref name="address"/> lie in readable regions,
    /// asking <paramref name="query"/> for the region holding each address in turn; it gives null when it can't say,
    /// which refuses. Only asks: nothing at <paramref name="address"/> is read.</summary>
    public static bool AllReadable(long address, int count, Func<long, PageRegion?> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (count < 0 || address > long.MaxValue - count)
        {
            return false;
        }
        for (var at = address; at < address + count;)
        {
            if (query(at) is not { IsReadable: true } region || region.Size <= 0 || region.Base > at ||
                region.Base > long.MaxValue - region.Size || region.Base + region.Size <= at)
            {
                return false;
            }
            at = region.Base + region.Size;
        }
        return true;
    }
}
