namespace Metaphor.Core.Cues;

/// <summary>Turns a cue's notes into sound (ADR 0019): 16-bit PCM, each note faded in and out so it starts and stops
/// without a click, placed left to right by equal-power panning.</summary>
public static class CueWave
{
    /// <summary>Samples per second.</summary>
    public const int SampleRate = 44100;

    /// <summary>The peak at volume 100, as a fraction of full scale, leaving headroom below clipping.</summary>
    public const double MaxPeak = 0.8;

    // Each note's fade in and fade out.
    private const int RampMilliseconds = 5;

    /// <summary>The peak sample at <paramref name="volume"/> (0 to 100). It follows the volume's square, so equal
    /// steps of the setting sound like roughly equal steps of loudness; 50 is a quarter of 100's peak.</summary>
    public static double Peak(int volume)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(volume, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        return short.MaxValue * MaxPeak * (volume / 100.0) * (volume / 100.0);
    }

    /// <summary>The left and right gains for <paramref name="pan"/>, from -1 (left) through 0 (centre) to +1 (right):
    /// the cosine and sine of a quarter turn scaled by the pan, so their squares always sum to 1 and a sound keeps its
    /// loudness as it moves. A pan outside the range is clamped; one that isn't a number is the centre.</summary>
    public static (double Left, double Right) PanGains(double pan)
    {
        var clamped = double.IsNaN(pan) ? 0 : Math.Clamp(pan, -1, 1);
        var angle = (clamped + 1) * Math.PI / 4;
        return (Math.Cos(angle), Math.Sin(angle));
    }

    /// <summary>The cue's mono samples at <paramref name="volume"/> (0 to 100).</summary>
    public static short[] Samples(CueTone tone, int volume)
    {
        ArgumentNullException.ThrowIfNull(tone);
        var peak = Peak(volume);
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

    /// <summary>Mono <paramref name="samples"/> placed at <paramref name="pan"/> as interleaved left and right
    /// frames.</summary>
    public static short[] Stereo(ReadOnlySpan<short> samples, double pan)
    {
        var (left, right) = PanGains(pan);
        var frames = new short[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            frames[2 * i] = (short)Math.Round(samples[i] * left);
            frames[(2 * i) + 1] = (short)Math.Round(samples[i] * right);
        }
        return frames;
    }
}
