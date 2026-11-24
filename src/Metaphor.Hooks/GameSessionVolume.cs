using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Metaphor.Core.Audio;

namespace Metaphor.Hooks;

/// <summary>The game process's own volume in Windows' per-app mixer (#927, the maintainer's decision of 2026-11-24):
/// the default audio session's <c>ISimpleAudioVolume</c> on the default render endpoint, through Core Audio's COM
/// interfaces (docs/raw-sources/core-audio-session-volume-2026-11-24.md). No game code is read or touched. The mod runs
/// in the game's process, so "this process's default session" is the game's.
/// [NEEDS VERIFICATION] that the game plays its sound in its default session on the default console endpoint: by
/// ear, the game goes quieter while the settings menu is open.</summary>
internal sealed class GameSessionVolume : IAppVolume
{
    // EDataFlow eRender and ERole eConsole, the first members of their enums (Wine's mmdeviceapi.idl).
    private const int Render = 0;
    private const int Console = 0;
    // CLSCTX_INPROC_SERVER (wtypesbase.h) [NEEDS VERIFICATION against the SDK header; not on the saved pages].
    private const int InProcServer = 1;
    private static readonly Guid SessionManagerId = new("bfa971f1-4d5e-40bb-935e-967039bfbee4");

    public float? Read()
    {
        var volume = Volume();
        return volume is not null && volume.GetMasterVolume(out var level) == 0 ? level : null;
    }

    public bool Write(float level) =>
        Volume() is { } volume && volume.SetMasterVolume(Math.Clamp(level, 0f, 1f), 0) == 0;

    // Asked each time, so a changed default device is followed.
    private static ISimpleAudioVolume? Volume()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        if (enumerator.GetDefaultAudioEndpoint(Render, Console, out var device) != 0)
        {
            return null;
        }
        var id = SessionManagerId;
        if (device.Activate(ref id, InProcServer, 0, out var activated) != 0 ||
            activated is not IAudioSessionManager manager)
        {
            return null;
        }
        // A null session GUID is this process's default session; 0 is not cross-process.
        return manager.GetSimpleAudioVolume(0, 0, out var volume) == 0 ? volume : null;
    }

    [ComImport]
    [Guid("bcde0395-e52f-467c-8e3d-c4579291692e")]
    private class MMDeviceEnumerator
    {
    }

    // Each interface declares its methods in the IDL's order up to the last one called, so the slots line up.
    [ComImport]
    [Guid("a95664d2-9614-4f35-a746-de8db63617e6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("d666063f-1587-4e43-81f1-b948e807363f")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
    }

    [ComImport]
    [Guid("bfa971f1-4d5e-40bb-935e-967039bfbee4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager
    {
        [PreserveSig]
        int GetAudioSessionControl(nint sessionGuid, int streamFlags, out nint control);

        [PreserveSig]
        int GetSimpleAudioVolume(nint sessionGuid, int streamFlags, out ISimpleAudioVolume volume);
    }

    [ComImport]
    [Guid("87ce5498-68d6-44e5-9215-6da47ef883d8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float level, nint eventContext);

        [PreserveSig]
        int GetMasterVolume(out float level);
    }
}

/// <summary>Runs the volume duck on its own thread (#927): the settings menu asks from the game's input thread, which
/// must not wait on Windows' audio service or join a COM apartment the game owns. At start it puts back a level a
/// run left ducked.</summary>
internal sealed class VolumeThread : IVolumeDuck
{
    private readonly BlockingCollection<Action> work = [];
    private readonly VolumeDuck duck;
    private readonly Action<string> errors;

    public VolumeThread(VolumeDuck duck, Action<string> errors)
    {
        this.duck = duck;
        this.errors = errors;
        var thread = new Thread(Run) { IsBackground = true, Name = "Metaphor game volume" };
        thread.SetApartmentState(ApartmentState.MTA);
        work.Add(duck.RestoreLeftover);
        thread.Start();
    }

    public void Duck() => work.Add(duck.Duck);

    public void Restore() => work.Add(duck.Restore);

    [SuppressMessage("Design", "CA1031", Justification = "A failed volume change must not end the thread or the game.")]
    private void Run()
    {
        foreach (var next in work.GetConsumingEnumerable())
        {
            try
            {
                next();
            }
            catch (Exception exception)
            {
                errors($"game volume: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
