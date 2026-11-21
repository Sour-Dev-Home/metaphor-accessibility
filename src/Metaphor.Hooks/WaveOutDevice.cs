using System.Runtime.InteropServices;
using Metaphor.Core.Cues;

namespace Metaphor.Hooks;

/// <summary>Opens the cue mixer's streams with winmm's waveOut functions from System32 (ADR 0019), one handle per
/// channel. Signatures, return codes and the buffer rules are quoted in
/// <c>docs/raw-sources/winmm-waveout-2026-11-21.md</c>.</summary>
/// <remarks>One handle per channel follows BlindGuyNW's FF6ScreenReader (CREDITS.md); its code is unlicensed, so
/// none was read or copied.</remarks>
internal sealed class WaveOutDevice : IWaveDevice
{
    public IWaveOut Open(int slots, int framesPerSlot) => new WaveOutHandle(slots, framesPerSlot);
}

/// <summary>One waveOut handle and its buffers. The headers and sample buffers are pinned from open to close, so the
/// device never sees memory move; <see cref="WaveSlots"/> releases a slot before it is written again and resets
/// before it closes. A handle that won't close keeps its buffers pinned for good rather than free memory the device
/// may still read.</summary>
internal sealed class WaveOutHandle : IWaveOut
{
    private const uint WaveMapper = uint.MaxValue; // WAVE_MAPPER, (-1)
    private const uint CallbackNull = 0;
    private const ushort WaveFormatPcm = 1;
    private const ushort Channels = 2;
    private const ushort BitsPerSample = 16;
    private const uint NoError = 0; // MMSYSERR_NOERROR
    private const uint StillPlaying = 33; // WAVERR_STILLPLAYING, WAVERR_BASE + 1

    private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();

    private readonly nint handle;
    private readonly WaveHeader[] headers;
    private readonly GCHandle headersPin;
    private readonly short[][] buffers;
    private readonly GCHandle[] bufferPins;

    public WaveOutHandle(int slots, int framesPerSlot)
    {
        const ushort blockAlign = Channels * BitsPerSample / 8;
        var format = new WaveFormat
        {
            FormatTag = WaveFormatPcm,
            Channels = Channels,
            SamplesPerSec = CueWave.SampleRate,
            AvgBytesPerSec = CueWave.SampleRate * blockAlign,
            BlockAlign = blockAlign,
            BitsPerSample = BitsPerSample,
            Size = 0,
        };
        Check(waveOutOpen(out handle, WaveMapper, ref format, 0, 0, CallbackNull), "waveOutOpen");
        headers = new WaveHeader[slots];
        headersPin = GCHandle.Alloc(headers, GCHandleType.Pinned);
        buffers = new short[slots][];
        bufferPins = new GCHandle[slots];
        for (var slot = 0; slot < slots; slot++)
        {
            buffers[slot] = new short[framesPerSlot * Channels];
            bufferPins[slot] = GCHandle.Alloc(buffers[slot], GCHandleType.Pinned);
        }
    }

    public void Write(int slot, ReadOnlySpan<short> frames)
    {
        frames.CopyTo(buffers[slot]);
        // dwFlags must be zero before preparing; lpData and dwBufferLength set (waveOutPrepareHeader's remarks).
        var header = new WaveHeader
        {
            Data = bufferPins[slot].AddrOfPinnedObject(),
            BufferLength = (uint)(frames.Length * sizeof(short)),
        };
        headers[slot] = header;
        Check(waveOutPrepareHeader(handle, Header(slot), HeaderSize), "waveOutPrepareHeader");
        var written = waveOutWrite(handle, Header(slot), HeaderSize);
        if (written != NoError)
        {
            // Never queued, so it can be unprepared at once. Either failure throws, and the mixer closes the channel.
            var unprepared = waveOutUnprepareHeader(handle, Header(slot), HeaderSize);
            throw new InvalidOperationException(
                $"waveOutWrite returned {written}; waveOutUnprepareHeader then returned {unprepared}");
        }
    }

    public SlotState Release(int slot)
    {
        // Unpreparing a header never prepared returns zero, so a slot is released however it was left.
        var result = waveOutUnprepareHeader(handle, Header(slot), HeaderSize);
        if (result == StillPlaying)
        {
            return SlotState.StillPlaying;
        }
        Check(result, "waveOutUnprepareHeader");
        return SlotState.Released;
    }

    public void Reset() => Check(waveOutReset(handle), "waveOutReset");

    public void Close()
    {
        Check(waveOutClose(handle), "waveOutClose");
        headersPin.Free();
        foreach (var pin in bufferPins)
        {
            pin.Free();
        }
    }

    private nint Header(int slot) => headersPin.AddrOfPinnedObject() + (nint)(slot * HeaderSize);

    private static void Check(uint result, string function)
    {
        if (result != NoError)
        {
            throw new InvalidOperationException($"{function} returned {result}");
        }
    }

    // WAVEHDR. Every member falls on its natural boundary, so the SDK's byte packing changes nothing: 48 bytes on x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public nint Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public nuint User;
        public uint Flags;
        public uint Loops;
        public nint Next;
        public nuint Reserved;
    }

    // WAVEFORMATEX, byte-packed as the headers declare it: 18 bytes.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

#pragma warning disable SA1300, IDE1006 // The functions keep their documented names.
    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutOpen(
        out nint handle, uint deviceId, ref WaveFormat format, nint callback, nint instance, uint flags);

    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutPrepareHeader(nint handle, nint header, uint headerSize);

    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutWrite(nint handle, nint header, uint headerSize);

    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutUnprepareHeader(nint handle, nint header, uint headerSize);

    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutReset(nint handle);

    [DllImport("winmm.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint waveOutClose(nint handle);
#pragma warning restore SA1300, IDE1006
}
