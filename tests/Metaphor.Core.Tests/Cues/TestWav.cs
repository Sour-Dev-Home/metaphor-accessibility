using System.Buffers.Binary;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>Builds WAV files for the sample tests (#841): 16-bit PCM, any channel count, rate and bit depth written in
/// the header as given.</summary>
internal static class TestWav
{
    /// <summary>A file of interleaved <paramref name="samples"/>.</summary>
    public static byte[] Pcm(
        short[] samples, int channels = 1, int rate = CueWave.SampleRate, int bits = 16, int format = 1,
        byte[]? extraChunk = null)
    {
        var extra = extraChunk ?? [];
        var data = samples.Length * 2;
        var file = new byte[12 + 24 + extra.Length + 8 + data];
        var span = file.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], file.Length - 8);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], (short)format);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], rate * channels * bits / 8);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * bits / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], (short)bits);
        extra.CopyTo(span[36..]);
        var at = 36 + extra.Length;
        "data"u8.CopyTo(span[at..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(at + 4)..], data);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(at + 8 + (i * 2))..], samples[i]);
        }
        return file;
    }

    /// <summary>A mono sine of <paramref name="frames"/> frames at <paramref name="peak"/>.</summary>
    public static short[] Sine(int frames, double hertz = 441, double peak = 10000) =>
        [.. Enumerable.Range(0, frames).Select(i => (short)Math.Round(peak * Math.Sin(2 * Math.PI * hertz * i / CueWave.SampleRate)))];

    /// <summary>A sample read from a mono file of <paramref name="samples"/>.</summary>
    public static CueSample Sample(short[] samples, string name = "test.wav") =>
        CueSample.Parse(name, Pcm(samples), out _) ?? throw new InvalidOperationException("the test file didn't parse");
}
