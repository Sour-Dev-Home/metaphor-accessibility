using System.Text;

namespace Metaphor.Core.Cues;

/// <summary>Turns a cue's notes into sound (ADR 0019): 16-bit mono PCM, each note faded in and out so it starts and
/// stops without a click, then wrapped in a WAV file's header for the sink.</summary>
public static class CueWave
{
    /// <summary>Samples per second.</summary>
    public const int SampleRate = 44100;

    /// <summary>The peak at volume 100, as a fraction of full scale, leaving headroom below clipping.</summary>
    public const double MaxPeak = 0.8;

    // Each note's fade in and fade out.
    private const int RampMilliseconds = 5;

    /// <summary>The cue's samples at <paramref name="volume"/> (0 to 100). The peak follows the volume's square, so
    /// equal steps of the setting sound like roughly equal steps of loudness; 50 is a quarter of 100's peak.</summary>
    public static short[] Samples(CueTone tone, int volume)
    {
        ArgumentNullException.ThrowIfNull(tone);
        ArgumentOutOfRangeException.ThrowIfLessThan(volume, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        var peak = short.MaxValue * MaxPeak * (volume / 100.0) * (volume / 100.0);
        var lengths = tone.Steps.Select(step => step.Milliseconds * SampleRate / 1000).ToArray();
        var samples = new short[lengths.Sum()];
        var start = 0;
        for (var s = 0; s < lengths.Length; s++)
        {
            var length = lengths[s];
            var ramp = Math.Min(RampMilliseconds * SampleRate / 1000, length / 2);
            for (var i = 0; i < length; i++)
            {
                var edge = Math.Min(i, length - 1 - i);
                // A raised cosine: 0 at the note's first and last sample, 1 from the ramp's end on.
                var envelope = edge >= ramp ? 1.0 : 0.5 * (1 - Math.Cos(Math.PI * edge / ramp));
                var wave = Math.Sin(2 * Math.PI * tone.Steps[s].Hertz * i / SampleRate);
                samples[start + i] = (short)Math.Round(peak * envelope * wave);
            }
            start += length;
        }
        return samples;
    }

    /// <summary>The cue at <paramref name="volume"/> as a whole WAV file: the RIFF header, a PCM format chunk, then
    /// the samples, little-endian.</summary>
    public static byte[] Wav(CueTone tone, int volume)
    {
        var samples = Samples(tone, volume);
        const short channels = 1;
        const short bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;
        var dataBytes = samples.Length * blockAlign;
        using var stream = new MemoryStream(44 + dataBytes);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * blockAlign);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            foreach (var sample in samples)
            {
                writer.Write(sample);
            }
        }
        return stream.ToArray();
    }
}
