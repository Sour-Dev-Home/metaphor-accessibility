using System.Diagnostics;
using System.Runtime.InteropServices;
using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Copies the running game's main module out for <see cref="SignatureScan"/>. Only reads: nothing is written,
/// patched or reprotected, which ADR 0002 allows ("Our own Reloaded-II mod reading the running game's memory").</summary>
internal static unsafe class ModuleMemoryReader
{
    // Values from https://learn.microsoft.com/windows/win32/memory/memory-protection-constants and
    // https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-memory_basic_information.
    private const uint MemCommit = 0x1000;
    private const uint PageGuard = 0x100;
    private const uint ReadableProtections = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80; // READONLY .. EXECUTE_WRITECOPY
    private const int ChunkBytes = 1 << 20;

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

    /// <summary>Copies the main module's committed, readable pages, as runs where they touch. Reserved, guarded and
    /// no-access pages are never read, and a chunk that fails to copy is skipped and counted.</summary>
    public static ModuleMemory ReadMainModule()
    {
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule ?? throw new InvalidOperationException("the game process has no main module");
        var sinceStart = (long)(DateTime.Now - process.StartTime).TotalMilliseconds;
        var start = module.BaseAddress;

        var runs = new List<MemoryRun>();
        long unreadable = 0;
        foreach (var (spanStart, spanLength) in ReadableSpans(start, start + module.ModuleMemorySize))
        {
            var bytes = new byte[spanLength];
            var runFrom = 0;
            for (var offset = 0; offset < spanLength; offset += ChunkBytes)
            {
                var size = Math.Min(ChunkBytes, spanLength - offset);
                bool copied;
                fixed (byte* target = &bytes[offset])
                {
                    copied = ReadProcessMemory(CurrentProcess, spanStart + offset, target, (nuint)size, out var read) &&
                        read == (nuint)size;
                }
                if (!copied)
                {
                    AddRun(spanStart, bytes, runFrom, offset);
                    unreadable += size;
                    runFrom = offset + size;
                }
            }
            AddRun(spanStart, bytes, runFrom, spanLength);
        }
        return new ModuleMemory(module.ModuleMemorySize, runs, unreadable, sinceStart);

        void AddRun(nint spanStart, byte[] bytes, int from, int to)
        {
            if (to > from)
            {
                runs.Add(new MemoryRun(spanStart + from - start, bytes.AsMemory(from, to - from)));
            }
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
