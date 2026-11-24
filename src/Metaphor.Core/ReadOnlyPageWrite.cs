namespace Metaphor.Core;

/// <summary>One pointer written into a read-only page of the game's image (#253): the page is made writable, the
/// pointer written, and the page's old protection put back, even when the write fails or throws. A plain
/// <c>WriteProcessMemory</c> is refused on the target cursor's vtable page with error 998 (by ear, 2026-11-24, Steam
/// build 18330018), so the vtable swap writes through this. Kept here so it is tested without the game: the hook layer
/// hands it <c>VirtualProtect</c> and its write.</summary>
public static class ReadOnlyPageWrite
{
    /// <summary><c>PAGE_READWRITE</c>:
    /// https://learn.microsoft.com/windows/win32/memory/memory-protection-constants</summary>
    public const uint ReadWrite = 0x04;

    /// <summary>Writes <paramref name="value"/> at <paramref name="address"/>. <paramref name="protect"/> sets the
    /// protection of the page holding an address and returns the old one, or null when it can't. True only when the
    /// page was made writable, the write succeeded and the old protection went back.</summary>
    public static bool WritePointer(
        long address, long value, Func<long, uint, uint?> protect, Func<long, long, bool> write)
    {
        ArgumentNullException.ThrowIfNull(protect);
        ArgumentNullException.ThrowIfNull(write);
        if (protect(address, ReadWrite) is not { } old)
        {
            return false;
        }
        var written = false;
        try
        {
            written = write(address, value);
        }
        finally
        {
            written &= protect(address, old) is not null;
        }
        return written;
    }
}
