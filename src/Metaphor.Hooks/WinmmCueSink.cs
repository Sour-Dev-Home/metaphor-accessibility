using System.Runtime.InteropServices;
using Metaphor.Core.Cues;

namespace Metaphor.Hooks;

/// <summary>Sounds a cue's WAV through winmm's <c>PlaySoundW</c> (ADR 0019), on <see cref="CuePlayer"/>'s own thread.
/// The flags and their meaning are quoted in <c>docs/raw-sources/winmm-playsound-2026-11-21.md</c>.</summary>
internal sealed class WinmmCueSink : ICueSink
{
    // SND_SYNC: returns once the sound has finished, so the array the marshaller passes is valid throughout; with
    // SND_ASYNC the buffer would have to outlive the call. SND_MEMORY: the argument is a WAV file in memory.
    // SND_NODEFAULT: a sound that can't be played is silent rather than the system's default sound.
    private const uint SndSync = 0x0000;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    public void Play(byte[] wav)
    {
        ArgumentNullException.ThrowIfNull(wav);
        if (!PlaySound(wav, 0, SndSync | SndMemory | SndNoDefault))
        {
            throw new InvalidOperationException("PlaySound returned false");
        }
    }

    // https://learn.microsoft.com/en-us/previous-versions/dd743680(v=vs.85)
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] sound, nint module, uint flags);
}
