using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Which game code called a hooked function, for development-only listeners (#93). The loader's adapters
/// don't pass the return address on, so this looks for it from inside <c>Observe</c>, two ways:
/// <list type="number">
/// <item>a stack walk (<c>RtlCaptureStackBackTrace</c>), taking the first return address inside the game module
/// (<see cref="CallerFrames.FirstInModule"/>);</item>
/// <item>when the walk finds none, as every walk did in session 1 (docs/raw-sources/spike-session-1-2026-11-16.md),
/// reading this thread's own stack upward from here for values that point into the module just after a call
/// (<see cref="CallerFrames.ReturnAddressesOnStack"/>).</item>
/// </list>
/// It only reads: this thread's stack, between here and the stack's top as Windows reports it, and the module's bytes,
/// all through <see cref="ModuleMemoryReader"/>.
///
/// [NEEDS VERIFICATION] Why the walk found nothing. A walk unwinds each frame with the unwind data registered for its
/// code; one guess is that a frame between the hook and the game (the hooking library's stub or the runtime's thunk)
/// has none and moves the stack pointer, so the walk loses its place and stops. The log's <c>frames=</c> count says how
/// far it got.</summary>
internal sealed class CallerAddress
{
    // Enough for this method, the observer, the adapter's callback, the runtime's thunk and the game's frame, with room
    // to spare. https://learn.microsoft.com/windows/win32/debug/rtlcapturestackbacktrace caps it below 63 frames.
    private const int FramesToCapture = 32;

    // How much of the stack above this frame the fallback reads: the mod's own frames (observer, adapter, the runtime's
    // reverse P/Invoke stub) sit below the game's, and 16 KiB covers them many times over.
    private const int StackBytesToRead = 16 * 1024;

    private readonly nint moduleBase;
    private readonly long moduleSize;

    /// <summary>The game module's base address, or 0 when it couldn't be found.</summary>
    public nint ModuleBase => moduleBase;

    /// <summary>The game module's size in memory, or 0 when it couldn't be found.</summary>
    public long ModuleSize => moduleSize;

    /// <summary>Never throws, so it can't stop the loader installing other hooks: without the main module it finds no
    /// caller.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A development log's caller column isn't worth failing hook installation.")]
    public CallerAddress()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            if (process.MainModule is { } module)
            {
                moduleBase = module.BaseAddress;
                moduleSize = module.ModuleMemorySize;
            }
        }
        catch (Exception)
        {
            // moduleSize stays 0: no frame is in the module, so Find finds no caller.
        }
    }

    // Walks with each frame's unwind data and stops at the thread's stack limits, so it ends early instead of faulting.
    // https://learn.microsoft.com/windows/win32/debug/rtlcapturestackbacktrace
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ushort RtlCaptureStackBackTrace(uint framesToSkip, uint framesToCapture, [Out] nint[] backTrace, nint backTraceHash);

    // The current thread's stack bounds: low is the lowest address it may reach, high its top (exclusive).
    // https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentthreadstacklimits
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void GetCurrentThreadStackLimits(out nuint lowLimit, out nuint highLimit);

    /// <summary>The caller as far as it can be found: by the walk, else by reading the stack.</summary>
    public CallerTrace Find()
    {
        var frames = new nint[FramesToCapture];
        var captured = RtlCaptureStackBackTrace(0, FramesToCapture, frames, 0);
        var addresses = new long[captured];
        for (var i = 0; i < captured; i++)
        {
            addresses[i] = frames[i];
        }

        if (CallerFrames.FirstInModule(addresses, moduleBase, moduleSize) is { } returnRva)
        {
            var call = new byte[CallerFrames.DirectCallLength];
            var target = ModuleMemoryReader.ReadAt(moduleBase, returnRva - CallerFrames.DirectCallLength, call)
                ? CallerFrames.DirectCallTarget(call, returnRva)
                : null;
            return new([(returnRva, target)], "walk", captured);
        }

        var candidates = StackCandidates();
        return new(candidates, candidates.Count > 0 ? "scan" : "none", captured);
    }

    private IReadOnlyList<(long ReturnRva, long? CallTargetRva)> StackCandidates()
    {
        if (moduleSize == 0)
        {
            return [];
        }
        var here = Here();
        GetCurrentThreadStackLimits(out _, out var high);
        var available = (long)high - here;
        if (available <= 0)
        {
            return [];
        }
        var stack = new byte[(int)Math.Min(available, StackBytesToRead) & ~7];
        if (!ModuleMemoryReader.ReadBytes((nint)here, stack))
        {
            return [];
        }
        return CallerFrames.ReturnAddressesOnStack(
            stack, moduleBase, moduleSize, (rva, buffer) => ModuleMemoryReader.ReadAt(moduleBase, rva, buffer));
    }

    // An address in this method's own frame, rounded up to 8: everything from here to the stack's top is the frames of
    // this thread's callers, the game's among them. Taking a local's address reads nothing.
    private static unsafe long Here()
    {
        long marker = 0;
        return ((long)&marker + 7) & ~7L;
    }
}
