using System.Diagnostics;
using System.Runtime.InteropServices;
using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>The hook layer's only native memory reads: the main module's readable regions, raw copies for
/// <see cref="ChunkedScan"/>, and safe reads through pointers the game hands hooks. Only reads: nothing is written,
/// patched or reprotected, which ADR 0002 allows ("Our own Reloaded-II mod reading the running game's memory").</summary>
internal static unsafe class ModuleMemoryReader
{
    // Values from https://learn.microsoft.com/windows/win32/memory/memory-protection-constants and
    // https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-memory_basic_information.
    private const uint MemCommit = 0x1000;
    private const uint PageGuard = 0x100;
    private const uint ReadableProtections = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80; // READONLY .. EXECUTE_WRITECOPY

    // GetCurrentProcess's pseudo-handle, so no real handle to the game is opened.
    // https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess
    private static readonly nint CurrentProcess = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
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
    private static extern nuint VirtualQuery(nint address, out MemoryBasicInformation buffer, nuint length);

    // Copies through the kernel, which returns false on a page it can't read instead of faulting, so a page whose
    // protection changes after VirtualQuery can't crash the game.
    // https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint address, byte* buffer, nuint size, out nuint read);

    /// <summary>The main module's base address and its committed, readable stretches as RVAs and lengths, for
    /// <see cref="ChunkedScan"/> with <see cref="ReadAt"/> (#39). Nothing is copied here.</summary>
    public static (nint Base, IReadOnlyList<(long Rva, long Length)> Regions) MainModuleRegions()
    {
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule ?? throw new InvalidOperationException("the game process has no main module");
        var start = module.BaseAddress;
        return (start, [.. ReadableSpans(start, start + module.ModuleMemorySize)
            .Select(span => ((long)(span.Start - start), (long)span.Length))]);
    }

    /// <summary>Copies <paramref name="buffer"/>.Length bytes from <paramref name="rva"/> past <paramref name="moduleBase"/>
    /// through <c>ReadProcessMemory</c>, which fails instead of faulting; false unless every byte arrived.</summary>
    public static bool ReadAt(nint moduleBase, long rva, Span<byte> buffer) => Copy(moduleBase + (nint)rva, buffer);

    /// <summary>The main module's base address.</summary>
    public static nint MainModuleBase()
    {
        using var process = Process.GetCurrentProcess();
        return process.MainModule?.BaseAddress ?? throw new InvalidOperationException("the game process has no main module");
    }

    // The readers below are for hooks reading through pointers the game hands them. They all copy through
    // ReadProcessMemory, because .NET can't catch the access violation a bad pointer would cause: a failed copy gives
    // null instead.

    /// <summary>Reads one pointer-sized value at <paramref name="address"/>, or null when it can't be read.</summary>
    public static nint? ReadPointer(nint address)
    {
        nint value = 0;
        return ReadProcessMemory(CurrentProcess, address, (byte*)&value, (nuint)sizeof(nint), out var read) &&
            read == (nuint)sizeof(nint)
            ? value
            : null;
    }

    /// <summary>Reads one 32-bit value at <paramref name="address"/>, or null when it can't be read.</summary>
    public static int? ReadInt32(nint address)
    {
        var value = 0;
        return ReadProcessMemory(CurrentProcess, address, (byte*)&value, sizeof(int), out var read) && read == sizeof(int)
            ? value
            : null;
    }

    /// <summary>Copies <paramref name="buffer"/>.Length bytes from <paramref name="address"/>; false unless every byte
    /// arrived.</summary>
    public static bool ReadBytes(nint address, Span<byte> buffer) => Copy(address, buffer);

    /// <summary>Reads a NUL-terminated ANSI string at <paramref name="address"/>, up to <paramref name="maxBytes"/>,
    /// as <see cref="MemoryText.ReadAnsi"/> does; null when its start can't be read.</summary>
    public static string? ReadAnsiString(nint address, int maxBytes = 512) => MemoryText.ReadAnsi(CopyAt, address, maxBytes);

    /// <summary>Reads <paramref name="length"/> bytes at <paramref name="address"/> as UTF-8, as
    /// <see cref="MemoryText.ReadUtf8"/> does; null when the length is negative or over <paramref name="maxBytes"/>, or
    /// the bytes can't all be read.</summary>
    public static string? ReadUtf8(nint address, int length, int maxBytes = 16 << 20) =>
        MemoryText.ReadUtf8(CopyAt, address, length, maxBytes);

    private static bool CopyAt(long address, Span<byte> buffer) => Copy((nint)address, buffer);

    private static bool Copy(nint address, Span<byte> buffer)
    {
        fixed (byte* target = buffer)
        {
            return ReadProcessMemory(CurrentProcess, address, target, (nuint)buffer.Length, out var read) &&
                read == (nuint)buffer.Length;
        }
    }

    /// <summary>The committed, readable, unguarded stretches of [<paramref name="start"/>, <paramref name="end"/>),
    /// with touching regions merged.</summary>
    private static List<(nint Start, int Length)> ReadableSpans(nint start, nint end)
    {
        var spans = new List<(nint Start, int Length)>();
        nint spanStart = 0, spanEnd = 0;
        for (var address = start; address < end;)
        {
            if (VirtualQuery(address, out var region, (nuint)sizeof(MemoryBasicInformation)) == 0)
            {
                throw new InvalidOperationException($"VirtualQuery failed at module offset 0x{address - start:x}");
            }
            var regionEnd = (nint)Math.Min((long)region.BaseAddress + (long)region.RegionSize, end);
            var readable = region.State == MemCommit && (region.Protect & PageGuard) == 0 &&
                (region.Protect & ReadableProtections) != 0;
            if (readable && address == spanEnd && spanEnd != 0)
            {
                spanEnd = regionEnd;
            }
            else if (readable)
            {
                Add();
                (spanStart, spanEnd) = (address, regionEnd);
            }
            address = regionEnd;
        }
        Add();
        return spans;

        void Add()
        {
            if (spanEnd > spanStart)
            {
                // A module's size is an int (ProcessModule.ModuleMemorySize), so a span inside it always fits.
                spans.Add((spanStart, checked((int)(spanEnd - spanStart))));
            }
        }
    }
}
