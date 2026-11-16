using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Which game code called a hooked function, for development-only listeners (#93). The loader's adapters
/// don't pass the return address on, so this walks the current thread's stack from inside <c>Observe</c> and takes the
/// first return address inside the game module (<see cref="CallerFrames.FirstInModule"/>). It only reads: the walk
/// covers this thread's own stack, and the bytes before the return address go through
/// <see cref="ModuleMemoryReader"/>.
///
/// [NEEDS VERIFICATION] That the walk gets past the hooking library's stub to the game's frame. It relies on that stub
/// reaching our delegate by jumps alone, so the game's return address is the next frame out; a stub that pushes
/// anything would hide it, and the log then says <c>caller=(unknown)</c> or names a caller whose preceding bytes aren't a
/// call. The first run settles it: a real caller's <c>call=</c> names the hooked function or a thunk to it.</summary>
internal sealed class CallerAddress
{
    // Enough for this method, the observer, the adapter's callback, the runtime's thunk and the game's frame, with room
    // to spare. https://learn.microsoft.com/windows/win32/debug/rtlcapturestackbacktrace caps it below 63 frames.
    private const int FramesToCapture = 32;

    private readonly nint moduleBase;
    private readonly long moduleSize;

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
            // moduleSize stays 0: no frame is in the module, so Find returns (null, null).
        }
    }

    // Walks with each frame's unwind data and stops at the thread's stack limits, so it ends early instead of faulting.
    // https://learn.microsoft.com/windows/win32/debug/rtlcapturestackbacktrace
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern ushort RtlCaptureStackBackTrace(uint framesToSkip, uint framesToCapture, [Out] nint[] backTrace, nint backTraceHash);

    /// <summary>The caller's return address as a module offset, and where the direct call before it went, if it is one.
    /// Either is null when unknown.</summary>
    public (long? ReturnRva, long? CallTargetRva) Find()
    {
        var frames = new nint[FramesToCapture];
        var captured = RtlCaptureStackBackTrace(0, FramesToCapture, frames, 0);
        var addresses = new long[captured];
        for (var i = 0; i < captured; i++)
        {
            addresses[i] = frames[i];
        }

        if (CallerFrames.FirstInModule(addresses, moduleBase, moduleSize) is not { } returnRva)
        {
            return (null, null);
        }
        var call = new byte[CallerFrames.DirectCallLength];
        var target = ModuleMemoryReader.ReadAt(moduleBase, returnRva - CallerFrames.DirectCallLength, call)
            ? CallerFrames.DirectCallTarget(call, returnRva)
            : null;
        return (returnRva, target);
    }
}
