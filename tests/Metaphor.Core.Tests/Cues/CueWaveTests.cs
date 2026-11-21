using System.Text;
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

    [Fact]
    public void TheWavIsSixteenBitMonoPcmHoldingTheSamples()
    {
        var tone = CueCatalogue.For(Cue.EnemyHit);
        var samples = CueWave.Samples(tone, 70);

        var wav = CueWave.Wav(tone, 70);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(wav, 8, 8));
        Assert.Equal(16, BitConverter.ToInt32(wav, 16));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22)); // mono
        Assert.Equal(CueWave.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(CueWave.SampleRate * 2, BitConverter.ToInt32(wav, 28));
        Assert.Equal(2, BitConverter.ToInt16(wav, 32));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(44 + (samples.Length * 2), wav.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], BitConverter.ToInt16(wav, 44 + (2 * i)));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AVolumeOutsideTheRangeIsRefused(int volume)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CueWave.Samples(CueCatalogue.For(Cue.AllyHit), volume));
    }
}
