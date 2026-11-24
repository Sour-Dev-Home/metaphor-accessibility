namespace Metaphor.Core.Cues;

/// <summary>Turns a cue's notes into sound (ADR 0019): 16-bit PCM, each note faded in and out so it starts and stops
/// without a click, placed left to right by equal-power panning.</summary>
public static class CueWave
{
    /// <summary>Samples per second.</summary>
    public const int SampleRate = 44100;

    /// <summary>The peak at volume 100, as a fraction of full scale, leaving headroom below clipping.</summary>
    public const double MaxPeak = 0.8;

    /// <summary>The cutoff of the muffle a cue behind the listener gets (#903): a one-pole low-pass at this frequency
    /// takes the high band, where front and back differ, down by about 6 dB per octave above it. The mod's own choice
    /// until a by-ear check tunes it.</summary>
    public const double MuffleHertz = 1200;

    /// <summary>The highest partial a broadband held tone carries (#839): about where the outer ear's height and
    /// front/back filtering ends (docs/raw-sources/elevation-cues-advice-check-2026-11-24.md).</summary>
    public const double BroadbandHertz = 12000;

    /// <summary>The most partials a broadband held tone sums, so a low tone stays cheap.</summary>
    public const int MaxPartials = 48;

    // The bound of the partial sums of sin(nx)/n, the Wilbraham-Gibbs constant Si(π), so a broadband tone scaled by it
    // never peaks above its sine.
    private const double SawBound = 1.8519370519824662;

    // Each note's fade in and fade out.
    private const int RampMilliseconds = 5;

    /// <summary>The coefficient of a one-pole low-pass with its cutoff at <paramref name="hertz"/>:
    /// <c>1 - e^(-2π·hertz/rate)</c>, 1 (no filtering) at or above half the sample rate.</summary>
    public static double LowPassCoefficient(double hertz) =>
        hertz >= SampleRate / 2.0 ? 1 : 1 - Math.Exp(-2 * Math.PI * Math.Max(hertz, 0) / SampleRate);

    /// <summary>One step of a one-pole low-pass: <paramref name="state"/> moves toward <paramref name="input"/> by
    /// <paramref name="coefficient"/> of the way and is returned. At 1 the input passes exactly.</summary>
    public static double LowPass(double input, double coefficient, ref double state)
    {
        state = coefficient >= 1 ? input : state + (coefficient * (input - state));
        return state;
    }

    /// <summary>How many partials a broadband tone at <paramref name="hertz"/> sums: every harmonic up to
    /// <see cref="BroadbandHertz"/>, at most <see cref="MaxPartials"/>, and at least the fundamental.</summary>
    public static int Partials(double hertz) =>
        hertz <= 0 ? 1 : Math.Clamp((int)(BroadbandHertz / hertz), 1, MaxPartials);

    /// <summary>A broadband wave at <paramref name="phase"/> (radians): the sum of <paramref name="partials"/>
    /// harmonics, each at 1/n of the fundamental, a band-limited sawtooth scaled so it never peaks above 1. One
    /// partial is the plain sine.</summary>
    public static double Broadband(double phase, int partials)
    {
        if (partials <= 1)
        {
            return Math.Sin(phase);
        }
        // sin(nx) by the recurrence sin(nx) = 2cos(x)sin((n-1)x) - sin((n-2)x): one sine and one cosine per sample.
        var twiceCos = 2 * Math.Cos(phase);
        var previous = 0.0;
        var current = Math.Sin(phase);
        var sum = current;
        for (var n = 2; n <= partials; n++)
        {
            var next = (twiceCos * current) - previous;
            previous = current;
            current = next;
            sum += current / n;
        }
        return sum / SawBound;
    }

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
