using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Metaphor.Core;
using Metaphor.Core.Announcements;

namespace Metaphor.Hooks;

/// <summary>The announcer that speaks through Prism (ADR 0005), and sends the same text to braille where the backend
/// supports it (#115, <see cref="OutputRoute"/>). It loads the pinned <c>prism.dll</c> by full path from the mod's
/// folder after checking its hash, and creates a backend only from <see cref="Backends"/>, in that order. Function
/// signatures and IDs are from Prism v0.18.3's <c>include/prism.h</c>.</summary>
internal sealed unsafe class PrismSpeech : IAnnouncer
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

    private readonly object gate = new();
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, byte, int> speak;
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, byte, int> output;
    private readonly OutputRoute route = new();
    private readonly nint backend;

    /// <summary>Which backend is speaking: one of <see cref="Backends"/>.</summary>
    public string BackendName { get; }

    private PrismSpeech(
        delegate* unmanaged[Cdecl]<nint, byte*, byte, int> speak,
        delegate* unmanaged[Cdecl]<nint, byte*, byte, int> output,
        nint backend,
        string backendName)
    {
        this.speak = speak;
        this.output = output;
        this.backend = backend;
        BackendName = backendName;
    }

    /// <summary>Starts speech from <paramref name="modFolder"/>'s <c>prism.dll</c>, or says why it couldn't. Never
    /// throws.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must leave the game running without speech.")]
    public static (PrismSpeech? Speech, string? WhyNot) TryStart(string modFolder)
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

            var config = configInit();
            if (config.Version != ConfigVersion)
            {
                return (null, $"prism.dll's config version is {config.Version}, not {ConfigVersion}");
            }
            // No availability callback, so Prism starts no availability poll (ADR 0005).
            config.AvailabilityCallback = 0;
            config.AvailabilityBaselineCallback = 0;
            var context = init(&config);
            if (context == 0)
            {
                return (null, "prism_init failed");
            }

            foreach (var (id, name) in Backends)
            {
                var candidate = create(context, id);
                if (candidate == 0)
                {
                    continue;
                }
                if (initialize(candidate) == 0) // PRISM_OK
                {
                    return (new PrismSpeech(speak, output, candidate, name), null);
                }
                free(candidate);
            }
            return (null, "no speech backend is available");
        }
        catch (Exception e)
        {
            return (null, $"Prism failed to start: {e.GetType().Name}");
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Speech failing must never reach the game.")]
    public void Announce(Announcement announcement)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(announcement);
            // prism_backend_output and _speak take a NUL-terminated UTF-8 string. Braille gets the spoken text (#115).
            var text = Encoding.UTF8.GetBytes(announcement.Text + '\0');
            var interrupt = announcement.Interrupt ? (byte)1 : (byte)0;
            lock (gate)
            {
                fixed (byte* bytes = text)
                {
                    if (route.TriesOutput && !route.SpeakInstead(output(backend, bytes, interrupt)))
                    {
                        return;
                    }
                    _ = speak(backend, bytes, interrupt);
                }
            }
        }
        catch (Exception)
        {
            // Nothing to tell: the transcript (Debug builds) still has the line.
        }
    }

    private static string PinnedDllSha256()
    {
        using var stream = typeof(PrismSpeech).Assembly.GetManifestResourceStream(PinResource)
            ?? throw new InvalidOperationException($"missing embedded resource {PinResource}");
        using var pin = JsonDocument.Parse(stream);
        return pin.RootElement.GetProperty("dllSha256").GetString()
            ?? throw new InvalidDataException("prism.json has no dllSha256");
    }
}
