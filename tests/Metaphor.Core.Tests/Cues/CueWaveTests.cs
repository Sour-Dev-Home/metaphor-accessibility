using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>The cues' tones (#636): short, click-free, distinct, and scaled by the volume setting.</summary>
[Trait("Category", "Unit")]
public sealed class CueWaveTests
{
    public static TheoryData<Cue> EveryCue => [.. Enum.GetValues<Cue>()];

    [Theory]
    [MemberData(nameof(EveryCue))]
    public void EveryCueIsWellUnder200Milliseconds(Cue cue)
    {
        var samples = CueWave.Samples(CueCatalogue.For(cue), 100);

        Assert.InRange(samples.Length * 1000 / CueWave.SampleRate, 50, 150);
    }

    [Theory]
    [MemberData(nameof(EveryCue))]
    public void EveryStepStartsAndEndsSilent(Cue cue)
    {
        var tone = CueCatalogue.For(cue);
        var samples = CueWave.Samples(tone, 100);

        var start = 0;
        foreach (var step in tone.Steps)
        {
            var length = step.Milliseconds * CueWave.SampleRate / 1000;
            Assert.Equal(0, samples[start]);
            Assert.InRange(samples[start + length - 1], (short)-200, (short)200);
            start += length;
        }
        Assert.Equal(samples.Length, start);
    }

    [Theory]
    [MemberData(nameof(EveryCue))]
    public void NoSampleJumpsFurtherThanASineCan(Cue cue)
    {
        // A click is a jump the tone's own slope can't make: at full scale a sine of f Hz moves at most
        // 2πf/SampleRate of its peak per sample.
        var tone = CueCatalogue.For(cue);
        var samples = CueWave.Samples(tone, 100);
        var fastest = tone.Steps.Max(step => step.Hertz);
        var limit = (short.MaxValue * CueWave.MaxPeak * 2 * Math.PI * fastest / CueWave.SampleRate) * 1.2;

        for (var i = 1; i < samples.Length; i++)
        {
            Assert.True(Math.Abs(samples[i] - samples[i - 1]) <= limit, $"jump at sample {i}");
        }
    }

    [Fact]
    public void TheVolumeScalesThePeak()
    {
        var tone = CueCatalogue.For(Cue.AllyHit);

        var loud = CueWave.Samples(tone, 100).Max(s => Math.Abs((int)s));
        var soft = CueWave.Samples(tone, 50).Max(s => Math.Abs((int)s));

        Assert.InRange(loud, (int)(short.MaxValue * CueWave.MaxPeak * 0.95), (int)(short.MaxValue * CueWave.MaxPeak) + 1);
        Assert.InRange(soft, loud / 5, loud / 3);
        Assert.All(CueWave.Samples(tone, 0), s => Assert.Equal(0, s));
    }

    [Fact]
    public void AnAllyHitFallsAndAnEnemyHitRises()
    {
        var ally = CueCatalogue.For(Cue.AllyHit).Steps;
        var enemy = CueCatalogue.For(Cue.EnemyHit).Steps;

        Assert.True(ally[^1].Hertz < ally[0].Hertz);
        Assert.True(enemy[^1].Hertz > enemy[0].Hertz);
        // Every enemy pitch is above every ally pitch, so even one heard alone is told apart.
        Assert.True(enemy.Min(step => step.Hertz) > ally.Max(step => step.Hertz));
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0.70710678, 0.70710678)]
    [InlineData(1, 0, 1)]
    [InlineData(-0.5, 0.92387953, 0.38268343)]
    [InlineData(-7, 1, 0)]
    [InlineData(double.NaN, 0.70710678, 0.70710678)]
    public void PanningIsEqualPowerFromLeftToRight(double pan, double left, double right)
    {
        var gains = CueWave.PanGains(pan);

        Assert.Equal(left, gains.Left, 6);
        Assert.Equal(right, gains.Right, 6);
    }

    [Fact]
    public void APannedSoundKeepsItsPowerWherever()
    {
        for (var pan = -1.0; pan <= 1.0; pan += 0.125)
        {
            var (left, right) = CueWave.PanGains(pan);
            Assert.Equal(1, (left * left) + (right * right), 9);
        }
    }

    [Fact]
    public void StereoInterleavesTheSamplesScaledByEachEarsGain()
    {
        var stereo = CueWave.Stereo([1000, -2000], -0.5);

        Assert.Equal([924, 383, -1848, -765], stereo);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AVolumeOutsideTheRangeIsRefused(int volume)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CueWave.Samples(CueCatalogue.For(Cue.AllyHit), volume));
    }

    [Fact]
    public void TheMuffleKeepsTheLowBandAndTakesTheHighBandDown()
    {
        // #903: a one-pole low-pass at the muffle's cutoff passes 200 Hz almost whole and cuts 8 kHz to under a fifth.
        var coefficient = CueWave.LowPassCoefficient(CueWave.MuffleHertz);

        Assert.Equal(1 - Math.Exp(-2 * Math.PI * CueWave.MuffleHertz / CueWave.SampleRate), coefficient, 12);
        Assert.InRange(Filtered(200, coefficient), 0.95, 1.0);
        Assert.InRange(Filtered(8000, coefficient), 0.1, 0.2);
    }

    [Fact]
    public void AnOpenFilterPassesTheWaveExactly()
    {
        var state = 0.3;

        Assert.Equal(1, CueWave.LowPassCoefficient(CueWave.SampleRate));
        Assert.Equal(0.7, CueWave.LowPass(0.7, 1, ref state));
        Assert.Equal(0.7, state);
    }

    [Fact]
    public void ABroadbandToneCarriesHarmonicsUpToTwelveKilohertzWithoutPeakingAboveItsSine()
    {
        // #839: every harmonic up to 12 kHz, capped for low tones; one partial is the sine itself.
        Assert.Equal(18, CueWave.Partials(659));
        Assert.Equal(CueWave.MaxPartials, CueWave.Partials(131));
        Assert.Equal(1, CueWave.Partials(20000));
        Assert.Equal(Math.Sin(1.1), CueWave.Broadband(1.1, 1));

        var wave = Enumerable.Range(0, 4410).Select(i => CueWave.Broadband(2 * Math.PI * 659 * i / CueWave.SampleRate, 18))
            .ToArray();
        Assert.All(wave, sample => Assert.InRange(sample, -1, 1));
        // The 9th harmonic, near 6 kHz, is there at about 1/9 of the fundamental (a sine has none).
        Assert.InRange(Amplitude(wave, 659 * 9) / Amplitude(wave, 659), 0.09, 0.13);
    }

    // The steady amplitude of a unit sine at hertz after the filter, from the last of 0.1 s.
    private static double Filtered(double hertz, double coefficient)
    {
        var state = 0.0;
        var peak = 0.0;
        for (var i = 0; i < CueWave.SampleRate / 10; i++)
        {
            var output = CueWave.LowPass(Math.Sin(2 * Math.PI * hertz * i / CueWave.SampleRate), coefficient, ref state);
            if (i > CueWave.SampleRate / 20)
            {
                peak = Math.Max(peak, Math.Abs(output));
            }
        }
        return peak;
    }

    // The amplitude of one frequency in the samples, by correlation with its sine and cosine.
    private static double Amplitude(double[] samples, double hertz)
    {
        var (sin, cos) = (0.0, 0.0);
        for (var i = 0; i < samples.Length; i++)
        {
            var angle = 2 * Math.PI * hertz * i / CueWave.SampleRate;
            sin += samples[i] * Math.Sin(angle);
            cos += samples[i] * Math.Cos(angle);
        }
        return 2 * Math.Sqrt((sin * sin) + (cos * cos)) / samples.Length;
    }
}
