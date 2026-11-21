using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Metaphor.Core;
using Metaphor.Core.Announcements;

namespace Metaphor.Hooks;

/// <summary>Speech through Prism (ADR 0005), and the same text to braille where the backend supports it (#115,
/// <see cref="OutputRoute"/>). It loads the pinned <c>prism.dll</c> by full path from the mod's folder after checking
/// its hash, and creates a backend only from <see cref="Backends"/>, in that order. A screen reader started or quit after
/// the game is picked up by <see cref="SwitchingAnnouncer"/> (#527), which asks this class for a fresh one. Function
/// signatures and IDs are from Prism v0.18.3's <c>include/prism.h</c>.</summary>
internal sealed unsafe class PrismSpeech
{
    /// <summary>The only backends the mod creates, tried in order: screen readers first, which fail to initialise
    /// when they aren't running, then Windows voices. ZDSR, PC-Talker and Boy PC Reader are left out on purpose: they
    /// load a DLL by bare name (ADR 0005), and an architecture test fails the build if their IDs appear here.</summary>
    internal static readonly (ulong Id, string Name)[] Backends =
    [
        (0x89CC19C5C4AC1A56, "NVDA"),
        (0xAC3D60E9BD84B53E, "JAWS"),
        (0xAE439D62DC7B1479, "ZoomText"),
        (0x6797D32F0D994CB4, "OneCore"),
        (0x1D6DF72422CEEE66, "SAPI"),
    ];

    /// <summary>How many of <see cref="Backends"/>, from the start, are screen readers.</summary>
    private const int ScreenReaderCount = 3;

    private const byte ConfigVersion = 4; // PRISM_CONFIG_VERSION in prism.h v0.18.3
    private const string PinResource = "Metaphor.Hooks.prism.json";

    // PrismConfig in prism.h v0.18.3; blittable, 56 bytes on x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct PrismConfig
    {
        public byte Version;
        public nint Registry;
        public nint AvailabilityCallback;
        public nint AvailabilityUserdata;
        public uint AvailabilityPollIntervalMs;
        public uint AvailabilityDebounceSamples;
        public uint AvailabilityBackoffMaxMs;
        public byte AvailabilityAutoPowerManage;
        public nint AvailabilityBaselineCallback;
    }

    private readonly nint context;
    private readonly delegate* unmanaged[Cdecl]<nint, ulong, nint> create;
    private readonly delegate* unmanaged[Cdecl]<nint, int> initialize;
    private readonly delegate* unmanaged[Cdecl]<nint, void> free;
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, byte, int> speak;
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, byte, int> output;
    private readonly delegate* unmanaged[Cdecl]<nint, int> stop;

    private PrismSpeech(
        nint context,
        delegate* unmanaged[Cdecl]<nint, ulong, nint> create,
        delegate* unmanaged[Cdecl]<nint, int> initialize,
        delegate* unmanaged[Cdecl]<nint, void> free,
        delegate* unmanaged[Cdecl]<nint, byte*, byte, int> speak,
        delegate* unmanaged[Cdecl]<nint, byte*, byte, int> output,
        delegate* unmanaged[Cdecl]<nint, int> stop)
    {
        this.context = context;
        this.create = create;
        this.initialize = initialize;
        this.free = free;
        this.speak = speak;
        this.output = output;
        this.stop = stop;
    }

    /// <summary>Starts speech from <paramref name="modFolder"/>'s <c>prism.dll</c>, or says why it couldn't. Started,
    /// it may have no backend yet (<see cref="SwitchingAnnouncer.BackendName"/> null), and later changes of backend
    /// go to <paramref name="log"/>. Never throws.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must leave the game running without speech.")]
    public static (SwitchingAnnouncer? Speech, string? WhyNot) TryStart(string modFolder, Action<string> log)
    {
        try
        {
            var path = Path.Combine(modFolder, "prism.dll");
            if (!File.Exists(path))
            {
                return (null, "no prism.dll");
            }
            nint library;
            // Held open, shared for reading only, from the hash check until the load, so the file checked is the file
            // loaded. Full path: architecture rule 4's exception for this library (ADR 0005).
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var whyNot = PinnedFile.WhyNot(held, "prism.dll", PinnedDllSha256());
                if (whyNot is not null)
                {
                    return (null, whyNot);
                }
                library = NativeLibrary.Load(path);
            }
            var configInit = (delegate* unmanaged[Cdecl]<PrismConfig>)NativeLibrary.GetExport(library, "prism_config_init");
            var init = (delegate* unmanaged[Cdecl]<PrismConfig*, nint>)NativeLibrary.GetExport(library, "prism_init");
            var create = (delegate* unmanaged[Cdecl]<nint, ulong, nint>)NativeLibrary.GetExport(library, "prism_registry_create");
            var initialize = (delegate* unmanaged[Cdecl]<nint, int>)NativeLibrary.GetExport(library, "prism_backend_initialize");
            var free = (delegate* unmanaged[Cdecl]<nint, void>)NativeLibrary.GetExport(library, "prism_backend_free");
            var speak = (delegate* unmanaged[Cdecl]<nint, byte*, byte, int>)NativeLibrary.GetExport(library, "prism_backend_speak");
            var output = (delegate* unmanaged[Cdecl]<nint, byte*, byte, int>)NativeLibrary.GetExport(library, "prism_backend_output");
            // prism.h v0.18.3 lines 413-414 (docs/raw-sources/prism-backend-stop-2026-11-21.md).
            var stop = (delegate* unmanaged[Cdecl]<nint, int>)NativeLibrary.GetExport(library, "prism_backend_stop");

            var config = configInit();
            if (config.Version != ConfigVersion)
            {
                return (null, $"prism.dll's config version is {config.Version}, not {ConfigVersion}");
            }
            // No availability callback, so Prism starts no availability poll (ADR 0005). Re-detection creates fresh
            // backends instead (docs/raw-sources/prism-backend-redetection-2026-11-20.md).
            config.AvailabilityCallback = 0;
            config.AvailabilityBaselineCallback = 0;
            var context = init(&config);
            if (context == 0)
            {
                return (null, "prism_init failed");
            }

            var prism = new PrismSpeech(context, create, initialize, free, speak, output, stop);
            return (new SwitchingAnnouncer(
                prism.FirstStarted(0, ScreenReaderCount),
                prism.FirstStarted(ScreenReaderCount, Backends.Length),
                () => prism.FirstStarted(0, ScreenReaderCount),
                log,
                TimeProvider.System,
                probe => ThreadPool.UnsafeQueueUserWorkItem(_ => probe(), null)), null);
        }
        catch (Exception e)
        {
            return (null, $"Prism failed to start: {e.GetType().Name}");
        }
    }

    /// <summary>Creates and initialises a fresh instance of each of <see cref="Backends"/>[from..to) in order, and
    /// returns the first that starts.</summary>
    private Backend? FirstStarted(int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            var (id, name) = Backends[i];
            var candidate = create(context, id);
            if (candidate == 0)
            {
                continue;
            }
            if (initialize(candidate) == 0) // PRISM_OK
            {
                return new Backend(this, candidate, name);
            }
            free(candidate);
        }
        return null;
    }

    private static string PinnedDllSha256()
    {
        using var stream = typeof(PrismSpeech).Assembly.GetManifestResourceStream(PinResource)
            ?? throw new InvalidOperationException($"missing embedded resource {PinResource}");
        using var pin = JsonDocument.Parse(stream);
        return pin.RootElement.GetProperty("dllSha256").GetString()
            ?? throw new InvalidDataException("prism.json has no dllSha256");
    }

    /// <summary>One started Prism backend. <see cref="SwitchingAnnouncer"/> calls it under its lock, so a backend
    /// is never spoken through and freed at once.</summary>
    private sealed class Backend(PrismSpeech prism, nint handle, string name) : ISpeechBackend
    {
        private nint handle = handle;

        public string Name => name;

        public int Output(string text, bool interrupt) => Call(prism.output, text, interrupt);

        public int Speak(string text, bool interrupt) => Call(prism.speak, text, interrupt);

        public int StopSpeaking() => handle == 0 ? 1 : prism.stop(handle); // 1: PRISM_ERROR_NOT_INITIALIZED

        public void Dispose()
        {
            var freed = Interlocked.Exchange(ref handle, 0);
            if (freed != 0)
            {
                prism.free(freed);
            }
        }

        private int Call(delegate* unmanaged[Cdecl]<nint, byte*, byte, int> function, string text, bool interrupt)
        {
            if (handle == 0)
            {
                return 1; // PRISM_ERROR_NOT_INITIALIZED
            }
            // prism_backend_output and _speak take a NUL-terminated UTF-8 string. Braille gets the spoken text (#115).
            var bytes = Encoding.UTF8.GetBytes(text + '\0');
            fixed (byte* start = bytes)
            {
                return function(handle, start, interrupt ? (byte)1 : (byte)0);
            }
        }
    }
}
