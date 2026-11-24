using System.Buffers.Binary;
using Metaphor.Core.Cues;

namespace Metaphor.NavSounds;

/// <summary>A generated sound's tone colour (#839). The broadband ones reach about 12 kHz, where the outer ear's height
/// and front/back cues lie and where the behind-muffle (#903) cuts
/// (docs/contributors/explanation/elevation-and-front-back-cues.md, step 1).</summary>
public enum Timbre
{
    /// <summary>A plain sine.</summary>
    Sine,

    /// <summary>Every harmonic at 1/n, a band-limited sawtooth: bright and buzzy.</summary>
    Bright,

    /// <summary>The odd harmonics at 1/n, a band-limited square: hollow, like a clarinet.</summary>
    Hollow,
}

/// <summary>The generator's synthesis (#839): sines, noise bursts and a WAV writer, built from IEEE additions,
/// multiplications and divisions only, so the same parameters give the same bytes on every machine. The runtime
/// libraries' <c>Math.Sin</c> and <c>Math.Cos</c> may differ in the last bit between platforms, so a Taylor series
/// stands in for them; nothing reads the clock or an unseeded random source.</summary>
public static class Synth
{
    /// <summary>Samples per second, as the cue player's.</summary>
    public const int SampleRate = CueWave.SampleRate;

    // Each note's fade in and out, as the cue player's.
    private const int RampMilliseconds = 5;

    // A struck note's burst of noise: how long it falls to nothing, and its level against the note's.
    private const int StrikeMilliseconds = 12;
    private const double StrikeLevel = 0.3;

    // The bound of the partial sums of sin(nx)/n, the Wilbraham-Gibbs constant Si(π); half of it bounds the odd
    // harmonics' sums (a square wave's).
    private const double SawBound = 1.8519370519824662;

    /// <summary>The sine of <paramref name="turns"/> whole turns (1 is 2π): folded into a quarter turn, then a Taylor
    /// series to the 15th power, within 1e-11 of the true value.</summary>
    public static double Sin(double turns)
    {
        var t = turns - Math.Floor(turns);
        if (t >= 0.5)
        {
            t -= 1;
        }
        if (t > 0.25)
        {
            t = 0.5 - t;
        }
        else if (t < -0.25)
        {
            t = -0.5 - t;
        }
        var x = 2 * Math.PI * t;
        var square = x * x;
        var term = x;
        var sum = x;
        for (var n = 1; n <= 7; n++)
        {
            term *= -square / ((2 * n) * ((2 * n) + 1));
            sum += term;
        }
        return sum;
    }

    /// <summary>The left and right gains for <paramref name="pan"/> from -1 to +1, equal power as
    /// <see cref="CueWave.PanGains"/>.</summary>
    public static (double Left, double Right) PanGains(double pan)
    {
        var clamped = Math.Clamp(pan, -1, 1);
        var quarter = (clamped + 1) / 8;
        return (Sin(quarter + 0.25), Sin(quarter));
    }

    /// <summary>Notes one after another (a 0 Hz note is a gap), each faded in and out, at <paramref name="volume"/> (0
    /// to 100) as the cue player scales it, as mono samples in -1 to 1 of the peak, in <paramref name="timbre"/>. With
    /// <paramref name="struck"/> each note starts with a short burst of noise from <paramref name="seed"/>, a strike
    /// that reaches the whole band.</summary>
    public static double[] Notes(
        IEnumerable<ToneStep> steps, int volume, Timbre timbre = Timbre.Sine, bool struck = false, uint seed = 1)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var peak = CueWave.Peak(volume) / short.MaxValue;
        var random = new Noise(seed);
        var strike = StrikeMilliseconds * SampleRate / 1000;
        var samples = new List<double>();
        foreach (var step in steps)
        {
            var length = step.Milliseconds * SampleRate / 1000;
            var ramp = Math.Min(RampMilliseconds * SampleRate / 1000, length / 2);
            for (var i = 0; i < length; i++)
            {
                var edge = Math.Min(i, length - 1 - i);
                var envelope = edge >= ramp ? 1.0 : Square(Sin(edge / (4.0 * ramp)));
                var wave = Wave(timbre, step.Hertz, i);
                if (struck && i < strike)
                {
                    wave += StrikeLevel * (1 - ((double)i / strike)) * random.Next();
                }
                samples.Add(step.Hertz <= 0 ? 0 : peak * envelope * wave);
            }
        }
        return [.. samples];
    }

    /// <summary>A tone that loops without a seam: <paramref name="seconds"/> of a whole-number pitch in
    /// <paramref name="timbre"/>, whose partials are whole-number multiples of it, so every loop ends on a whole cycle,
    /// with no fade.</summary>
    public static double[] Loop(int hertz, int seconds, int volume, Timbre timbre = Timbre.Sine)
    {
        var peak = CueWave.Peak(volume) / short.MaxValue;
        var samples = new double[seconds * SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = peak * Wave(timbre, hertz, i);
        }
        return samples;
    }

    /// <summary>Sample <paramref name="i"/> of a wave at <paramref name="hertz"/> in <paramref name="timbre"/>, from -1
    /// to 1: a sine, or the sum of its harmonics up to <see cref="CueWave.BroadbandHertz"/>, each at 1/n, scaled by the
    /// bound of its partial sums so it never peaks above the sine (#839).</summary>
    public static double Wave(Timbre timbre, double hertz, int i)
    {
        if (hertz <= 0)
        {
            return 0;
        }
        if (timbre == Timbre.Sine)
        {
            return Sin(hertz * i / SampleRate);
        }
        var step = timbre == Timbre.Hollow ? 2 : 1;
        var sum = 0.0;
        for (var n = 1; n * hertz <= CueWave.BroadbandHertz || n == 1; n += step)
        {
            sum += Sin(n * hertz * i / SampleRate) / n;
        }
        return sum / (timbre == Timbre.Hollow ? SawBound / 2 : SawBound);
    }

    /// <summary>One second of short bursts, looping without a seam: each burst <paramref name="burstMilliseconds"/>
    /// of seeded noise, ring-modulated by <paramref name="hertz"/> when above 0, fading out across the burst. The gap
    /// between burst starts is <paramref name="gapMilliseconds"/>, and with <paramref name="jitter"/> each gap is
    /// stretched or shrunk by up to that share, from the same seeded noise.</summary>
    public static double[] Bursts(
        uint seed, int burstMilliseconds, double gapMilliseconds, double jitter, double hertz, int volume)
    {
        var peak = CueWave.Peak(volume) / short.MaxValue;
        var random = new Noise(seed);
        var samples = new double[SampleRate];
        var burst = burstMilliseconds * SampleRate / 1000;
        var start = 0.0;
        while (start < samples.Length - burst)
        {
            var at = (int)start;
            for (var i = 0; i < burst; i++)
            {
                var fade = 1 - ((double)i / burst);
                var carrier = hertz > 0 ? Sin(hertz * (at + i) / SampleRate) : 1;
                samples[at + i] = peak * fade * carrier * random.Next();
            }
            var stretch = 1 + (jitter * random.Next());
            start += gapMilliseconds * SampleRate / 1000 * stretch;
        }
        return samples;
    }

    /// <summary>Mono <paramref name="samples"/> placed at <paramref name="pan"/>, as a 16-bit stereo WAV file.</summary>
    public static byte[] Wav(IReadOnlyList<double> samples, double pan)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var (left, right) = PanGains(pan);
        const int channels = 2;
        const int bytesPerSample = 2;
        var data = samples.Count * channels * bytesPerSample;
        var file = new byte[44 + data];
        var span = file.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + data);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], SampleRate * channels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], channels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], bytesPerSample * 8);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], data);
        for (var i = 0; i < samples.Count; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + (i * 4))..], Pcm(samples[i] * left));
            BinaryPrimitives.WriteInt16LittleEndian(span[(46 + (i * 4))..], Pcm(samples[i] * right));
        }
        return file;
    }

    private static double Square(double x) => x * x;

    private static short Pcm(double sample) =>
        (short)Math.Clamp(Math.Round(sample * short.MaxValue), short.MinValue, short.MaxValue);

    /// <summary>A seeded xorshift generator: the same seed gives the same values, from -1 to 1.</summary>
    private sealed class Noise(uint seed)
    {
        private uint state = seed == 0 ? 0x9E3779B9u : seed;

        public double Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state / (double)uint.MaxValue * 2) - 1;
        }
    }
}
