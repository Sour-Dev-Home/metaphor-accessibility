using System.Runtime.InteropServices;

namespace Metaphor.Core.Tests;

/// <summary><see cref="PageRegion.AllReadable"/> against real Windows pages (#393): a readable page, a guard page and a
/// reserved one, side by side in this test process. The hook layer asks <c>VirtualQuery</c> the same way
/// (<c>ModuleMemoryReader.Query</c>). Whether <c>ReadProcessMemory</c> on a guard page also clears the guard, as
/// docs/raw-sources/windows-guard-pages-and-virtualquery-2026-11-19.md says a system service may, differs from what
/// that note observed on one Windows build, so only its failing is pinned here.</summary>
[Trait("Category", "WindowsOnly")]
public sealed class PageRegionWindowsTests : IDisposable
{
    // https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-virtualalloc
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint ReadWrite = 0x04;
    private const int Page = 0x1000;

    private readonly nint block;

    public PageRegionWindowsTests()
    {
        // Three pages reserved; the first committed readable, the second committed as a guard page, the third left
        // reserved.
        block = Native.VirtualAlloc(0, 3 * Page, MemReserve, ReadWrite);
        Assert.NotEqual(0, block);
        Assert.NotEqual(0, Native.VirtualAlloc(block, Page, MemCommit, ReadWrite));
        Assert.NotEqual(0, Native.VirtualAlloc(block + Page, Page, MemCommit, ReadWrite | PageRegion.PageGuard));
    }

    public void Dispose() => Native.VirtualFree(block, 0, MemRelease);

    private static PageRegion? Query(long address) =>
        Native.VirtualQuery((nint)address, out var region, (nuint)Marshal.SizeOf<Native.MemoryBasicInformation>()) == 0
            ? null
            : new PageRegion(region.BaseAddress, (long)region.RegionSize, region.State, region.Protect);

    private bool StillGuarded() => Query(block + Page) is { } region && (region.Protect & PageRegion.PageGuard) != 0;

    [Fact]
    public void TheReadablePageIsAllowed() => Assert.True(PageRegion.AllReadable(block, Page, Query));

    [Fact]
    public void AReadRunningIntoTheGuardPageIsRefusedAndLeavesItGuarded()
    {
        Assert.False(PageRegion.AllReadable(block + Page - 8, 16, Query));
        Assert.False(PageRegion.AllReadable(block + Page, 1, Query));
        Assert.True(StillGuarded());
    }

    [Fact]
    public void AReadOfTheReservedPageIsRefused() => Assert.False(PageRegion.AllReadable(block + 2 * Page, 8, Query));

    [Fact]
    public void ReadProcessMemoryOnTheGuardPageFails()
    {
        var buffer = new byte[8];

        Assert.False(Native.ReadProcessMemory(-1, block + Page, buffer, (nuint)buffer.Length, out _));
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBasicInformation
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public ushort PartitionId;
            public nuint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern nint VirtualAlloc(nint address, nuint size, uint type, uint protect);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualFree(nint address, nuint size, uint type);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern nuint VirtualQuery(nint address, out MemoryBasicInformation buffer, nuint length);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint read);
    }
}
