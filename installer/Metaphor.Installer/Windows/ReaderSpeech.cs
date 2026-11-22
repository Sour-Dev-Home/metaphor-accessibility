using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Metaphor.Installer.Windows;

/// <summary>
/// Speech through the running screen reader with the payload's prism.dll, as install.ps1's New-ReaderSpeech does
/// (scripts/player/MetaphorInstall.psm1): loaded by full path, held open from its SHA-256 check until it has loaded, and
/// started with the first of NVDA, JAWS and ZoomText that initializes, by Prism v0.18.3's backend IDs and calls
/// (docs/raw-sources/installer-prism-speech-2026-11-20.md). Disposing frees the backend, shuts Prism down and unloads
/// it, so its folder can be deleted.
/// </summary>
internal sealed class ReaderSpeech : IDisposable
{
    // NVDA, JAWS and ZoomText, the order install.ps1 tries them in ($PrismReaders).
    private static readonly ulong[] Readers = [0x89CC19C5C4AC1A56, 0xAC3D60E9BD84B53E, 0xAE439D62DC7B1479];

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr InitCall(IntPtr config);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ContextCall(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateCall(IntPtr context, ulong id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BackendCall(IntPtr backend);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeCall(IntPtr backend);

    // Text is NUL-terminated UTF-8; C's bool is one byte.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SpeakCall(IntPtr backend, byte[] text, byte interrupt);

    private readonly IntPtr library;
    private readonly IntPtr context;
    private IntPtr backend;
    private readonly ContextCall shutdown;
    private readonly FreeCall free;
    private readonly SpeakCall speak;

    private ReaderSpeech(IntPtr library, IntPtr context, IntPtr backend, ContextCall shutdown, FreeCall free, SpeakCall speak)
    {
        this.library = library;
        this.context = context;
        this.backend = backend;
        this.shutdown = shutdown;
        this.free = free;
        this.speak = speak;
    }

    /// <summary>The reader's speech, or null when prism.dll isn't the checked one, doesn't load, or no reader starts.</summary>
    public static ReaderSpeech? Open(SpeechLibrary prism)
    {
        IntPtr library;
        try
        {
            using var held = new FileStream(prism.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Payload.Sha256(held) != prism.Sha256)
            {
                return null;
            }
            library = NativeMethods.LoadLibraryExW(prism.Path, IntPtr.Zero, NativeMethods.SearchLoadFolderAndSystem32);
        }
        catch (IOException)
        {
            return null;
        }
        if (library == IntPtr.Zero)
        {
            return null;
        }
        var init = Call<InitCall>(library, "prism_init");
        var shutdown = Call<ContextCall>(library, "prism_shutdown");
        var create = Call<CreateCall>(library, "prism_registry_create");
        var initialize = Call<BackendCall>(library, "prism_backend_initialize");
        var free = Call<FreeCall>(library, "prism_backend_free");
        var speak = Call<SpeakCall>(library, "prism_backend_speak");
        if (init is null || shutdown is null || create is null || initialize is null || free is null || speak is null)
        {
            NativeMethods.FreeLibrary(library);
            return null;
        }
        var context = init(IntPtr.Zero);
        if (context == IntPtr.Zero)
        {
            NativeMethods.FreeLibrary(library);
            return null;
        }
        foreach (var id in Readers)
        {
            var backend = create(context, id);
            if (backend == IntPtr.Zero)
            {
                continue;
            }
            if (initialize(backend) == 0)
            {
                return new ReaderSpeech(library, context, backend, shutdown, free, speak);
            }
            free(backend);
        }
        shutdown(context);
        NativeMethods.FreeLibrary(library);
        return null;
    }

    /// <summary>Queues LINE after what the reader is saying, never cutting it off.</summary>
    public void Speak(string line)
    {
        if (backend != IntPtr.Zero)
        {
            speak(backend, Encoding.UTF8.GetBytes(line + "\0"), 0);
        }
    }

    public void Dispose()
    {
        if (backend == IntPtr.Zero)
        {
            return;
        }
        free(backend);
        shutdown(context);
        NativeMethods.FreeLibrary(library);
        backend = IntPtr.Zero;
    }

    private static T? Call<T>(IntPtr library, string name) where T : Delegate
    {
        var address = NativeMethods.GetProcAddress(library, name);
        return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(address);
    }
}
