using Metaphor.Core.Cues;

namespace Metaphor.Core.Tests.Cues;

/// <summary>The cue channels (#637): cues overlap on channels of their own, a held tone changes pitch, place and
/// loudness without a click, and the channel count is fixed.</summary>
[Trait("Category", "Unit")]
public sealed class CueMixerTests
{
    private readonly List<string> logged = [];

    [Fact]
    public void TwoCuesOverlapOnChannelsOfTheirOwn()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var ally = Rendered(Cue.AllyHit, pan: -1);
        var enemy = Rendered(Cue.EnemyHit, pan: 1);

        Assert.True(mixer.Play(ally));
        Assert.True(mixer.Play(enemy));
        mixer.Pump();

        // Both are queued at once, each on its own stream: neither waits for the other to finish.
        Assert.Equal(2, device.Opened.Count);
        Assert.All(device.Opened, output => Assert.True(output.Queued > 0));
        Assert.Equal(Prefix(ally, device.Opened[0].Frames.Length), device.Opened[0].Frames);
        Assert.Equal(Prefix(enemy, device.Opened[1].Frames.Length), device.Opened[1].Frames);
        Assert.Empty(logged);
    }

    [Fact]
    public void ACueIsDroppedWhileEveryCueChannelIsBusyAndPlaysOnceOneIsFree()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var cue = Rendered(Cue.EnemyHit, pan: 0);
        for (var i = 0; i < CueMixer.CueChannels; i++)
        {
            Assert.True(mixer.Play(cue));
        }
        mixer.Pump();

        Assert.False(mixer.Play(cue));

        // Play the first channel's cue to its end: the channel is free again.
        while (mixer.Pump() && device.Opened[0].Queued > 0)
        {
            device.Opened[0].Finish(CueMixer.Slots);
        }
        Assert.True(mixer.Play(cue));
        Assert.Equal(CueMixer.CueChannels, device.Opened.Count);
    }

    [Fact]
    public void ACueIsWrittenWholeAcrossSlotsAsTheyAreReleased()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var cue = Rendered(Cue.AllyHit, pan: 0.5);
        mixer.Play(cue);

        while (mixer.Pump())
        {
            device.Opened[0].Finish(1);
        }

        Assert.Equal(cue, device.Opened[0].Frames);
        Assert.All(device.Opened[0].Buffers, buffer => Assert.InRange(buffer.Length, 2, CueMixer.FramesPerSlot * 2));
        Assert.Empty(device.Opened[0].Violations);
    }

    [Fact]
    public void AHeldToneMovesFromLeftToRightWithoutABreak()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        mixer.Hold(ToneChannel.Guidance, new HeldTone(440, -1, 100), 100);
        mixer.Pump();
        var output = device.Opened.Single();

        // Fully left: the right ear hears nothing.
        Assert.All(Right(output.Buffers[1]), sample => Assert.Equal(0, sample));
        Assert.True(Peak(Left(output.Buffers[1])) > 0.9 * CueWave.Peak(100));

        mixer.Hold(ToneChannel.Guidance, new HeldTone(440, 1, 100), 100);
        output.Finish(CueMixer.Slots);
        mixer.Pump();
        var glide = output.Buffers[CueMixer.Slots];
        var settled = output.Buffers[CueMixer.Slots + 1];

        // The first new buffer glides across; the next is fully right.
        Assert.True(Peak(Right(glide)) > 0);
        Assert.All(Left(settled), sample => Assert.Equal(0, sample));
        Assert.True(Peak(Right(settled)) > 0.9 * CueWave.Peak(100));
        AssertNoClick(output.Frames, 440);
    }

    [Fact]
    public void AHeldToneChangesPitchWithoutAClick()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        mixer.Hold(ToneChannel.Wall, new HeldTone(300, 0, 100), 100);
        mixer.Pump();
        var output = device.Opened.Single();

        mixer.Hold(ToneChannel.Wall, new HeldTone(900, 0, 100), 100);
        output.Finish(CueMixer.Slots);
        mixer.Pump();

        // 20 ms at 300 Hz is 6 cycles, 12 crossings; at 900 Hz, 18 cycles.
        Assert.InRange(Crossings(Left(output.Buffers[1])), 11, 13);
        Assert.InRange(Crossings(Left(output.Buffers[CueMixer.Slots])), 35, 37);
        AssertNoClick(output.Frames, 900);
    }

    [Fact]
    public void AHeldToneFadesInAndStopsWithAFadeOut()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        mixer.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100), 100);
        mixer.Pump();
        var output = device.Opened.Single();

        mixer.Silence(ToneChannel.Guidance);
        output.Finish(CueMixer.Slots);
        var busy = mixer.Pump();

        // One fading buffer after the stop, then nothing more.
        Assert.True(busy);
        Assert.Equal(CueMixer.Slots + 1, output.Buffers.Count);
        Assert.Equal(0, output.Frames[0]);
        Assert.InRange(Math.Abs((int)output.Frames[^1]), 0, 1);
        output.Finish(CueMixer.Slots);
        Assert.False(mixer.Pump());
        Assert.Equal(CueMixer.Slots + 1, output.Buffers.Count);
    }

    [Fact]
    public void AHeldToneIsScaledByItsVolumeAndTheSetting()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        mixer.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 50), 50);
        mixer.Pump();

        var steady = device.Opened.Single().Buffers[1];
        var expected = CueWave.Peak(50) * 0.25 * Math.Sqrt(0.5);
        Assert.InRange(Peak(Left(steady)), expected * 0.95, expected + 1);
    }

    [Fact]
    public void EachToneChannelAndCueChannelIsItsOwnStreamAndThereAreNoMore()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var cue = Rendered(Cue.AllyHit, pan: 0);
        for (var i = 0; i < 10; i++)
        {
            mixer.Play(cue);
        }
        foreach (var channel in Enum.GetValues<ToneChannel>())
        {
            mixer.Hold(channel, new HeldTone(440, 0, 100), 100);
        }
        mixer.Pump();

        Assert.Equal(CueMixer.ChannelCount, device.Opened.Count);
        Assert.Equal(CueMixer.CueChannels + Enum.GetValues<ToneChannel>().Length, CueMixer.ChannelCount);
    }

    [Fact]
    public void DisposingClosesEveryStreamAfterResettingIt()
    {
        var device = new FakeWaveDevice();
        var mixer = new CueMixer(device, logged.Add);
        mixer.Play(Rendered(Cue.AllyHit, pan: 0));
        mixer.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100), 100);
        mixer.Pump();

        mixer.Dispose();

        Assert.All(device.Opened, output =>
        {
            Assert.True(output.Closed);
            Assert.Equal("close", output.Events[^1]);
            Assert.Contains("reset", output.Events);
            Assert.Empty(output.Violations);
        });
    }

    [Fact]
    public void ADeviceThatFailsIsLoggedOnceAndTriedAgainLater()
    {
        var device = new FakeWaveDevice { FailuresLeft = 2 };
        using var mixer = new CueMixer(device, logged.Add);
        var cue = Rendered(Cue.AllyHit, pan: 0);

        mixer.Play(cue);
        Assert.False(mixer.Pump());
        mixer.Play(cue);
        Assert.False(mixer.Pump());
        mixer.Play(cue);
        Assert.True(mixer.Pump());

        Assert.Single(device.Opened);
        Assert.Equal(["sound cue failed to play (no device); later failures are not logged"], logged);
    }

    [Fact]
    public void AHeldSamplePlaysInPlaceOfTheSineAtTheSamePeakAndThenFinishes()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        // Two and a half buffers of a sound whose every sample is at its peak.
        var frames = (CueMixer.FramesPerSlot * 5) / 2;
        var sample = TestWav.Sample([.. Enumerable.Repeat((short)1000, frames)]);

        mixer.Hold(ToneChannel.Sonar, new HeldTone(440, -1, 100, sample), 100, restart: true);
        mixer.Pump();
        var output = device.Opened.Single();

        // Fully left at the sine's peak once the fade-in buffer is past, and three writes: the last one the half left.
        Assert.Equal(3, output.Buffers.Count);
        Assert.All(Left(output.Buffers[1]), s => Assert.InRange(s, CueWave.Peak(100) - 1, CueWave.Peak(100) + 1));
        Assert.All(Right(output.Buffers[1]), s => Assert.Equal(0, s));
        Assert.Equal(CueMixer.FramesPerSlot, output.Buffers[2].Length);

        // Played through, the same hold doesn't start it again; a new hold does.
        output.Finish(CueMixer.Slots);
        Assert.False(mixer.Pump());
        mixer.Hold(ToneChannel.Sonar, new HeldTone(440, -1, 100, sample), 100);
        Assert.False(mixer.Pump());
        mixer.Hold(ToneChannel.Sonar, new HeldTone(440, -1, 100, sample), 100, restart: true);
        Assert.True(mixer.Pump());
        Assert.Equal(6, output.Buffers.Count);
    }

    [Fact]
    public void ALoopingSampleGoesRoundWithoutRestartingWhileTheSameSoundIsHeld()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        // A ramp a third of a buffer long, so a loop's seam lands inside each buffer.
        var length = CueMixer.FramesPerSlot / 3;
        var sample = TestWav.Sample([.. Enumerable.Range(1, length).Select(i => (short)i)]);
        var tone = new HeldTone(0, 0, 100, sample, Loops: true);

        mixer.Hold(ToneChannel.Wall, tone, 100, restart: true);
        mixer.Pump();
        mixer.Hold(ToneChannel.Wall, tone, 100, restart: true);
        var output = device.Opened.Single();
        output.Finish(CueMixer.Slots);
        mixer.Pump();

        // The ramp repeats at its own length across every buffer, the second hold included.
        var left = Left(output.Frames);
        Assert.True(left.Length > 4 * length);
        for (var i = CueMixer.FramesPerSlot; i + length < left.Length; i++)
        {
            Assert.Equal(left[i], left[i + length]);
        }
    }

    [Fact]
    public void ASilencedSampleFadesOutAndASineTakesItsChannelBack()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var sample = TestWav.Sample(TestWav.Sine(CueWave.SampleRate));
        mixer.Hold(ToneChannel.Route, new HeldTone(0, 0, 100, sample, Loops: true), 100, restart: true);
        mixer.Pump();
        var output = device.Opened.Single();

        mixer.Silence(ToneChannel.Route);
        output.Finish(CueMixer.Slots);
        Assert.True(mixer.Pump());
        Assert.InRange(Math.Abs((int)output.Frames[^1]), 0, 1);
        output.Finish(CueMixer.Slots);
        Assert.False(mixer.Pump());

        mixer.Hold(ToneChannel.Route, new HeldTone(440, 0, 100), 100);
        Assert.True(mixer.Pump());
        Assert.InRange(Crossings(Left(output.Buffers[^1])), 15, 25);
    }

    [Fact]
    public void AMuffledSoundLosesItsHighBandInBothEarsAndGlidesBackOpen()
    {
        // #903: a looping 8 kHz sound held behind comes through the muffle at under a fifth, the same in both ears.
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        var sample = TestWav.Sample(TestWav.Sine(CueMixer.FramesPerSlot * 4, 8000, 10000));
        var muffled = new HeldTone(440, 0, 100, sample, Loops: true, Muffled: true);

        mixer.Hold(ToneChannel.WallBehind, muffled, 100, restart: true);
        mixer.Pump();
        var output = device.Opened.Single();
        var open = CueWave.Peak(100) * Math.Sqrt(0.5);

        Assert.InRange(Peak(Left(output.Buffers[2])), open * 0.1, open * 0.2);
        Assert.All(Left(output.Buffers[2]).Zip(Right(output.Buffers[2])), ears => Assert.InRange(ears.First - ears.Second, -1, 1));

        // Held again unmuffled, it glides open over one buffer and is whole after it.
        output.Finish(CueMixer.Slots);
        mixer.Hold(ToneChannel.WallBehind, muffled with { Muffled = false }, 100);
        mixer.Pump();
        Assert.InRange(Peak(Left(output.Buffers[^1])), open - 2, open + 2);
    }

    [Fact]
    public void ABroadbandHeldToneIsNotAPlainSineAndStaysWithinItsPeak()
    {
        var device = new FakeWaveDevice();
        using var mixer = new CueMixer(device, logged.Add);
        mixer.Hold(ToneChannel.Sonar, new HeldTone(659, 0, 100, Broadband: true), 100);
        mixer.Hold(ToneChannel.Route, new HeldTone(659, 0, 100), 100);
        mixer.Pump();

        var broadband = Left(device.Opened[0].Buffers[1]);
        var sine = Left(device.Opened[1].Buffers[1]);
        var open = CueWave.Peak(100) * Math.Sqrt(0.5);
        Assert.NotEqual(sine, broadband);
        Assert.All(broadband, s => Assert.InRange(s, -open - 1, open + 1));
        // A sawtooth crosses zero once a cycle as a sine does, but its steepest step is many times the sine's.
        Assert.True(MaxStep(broadband) > 4 * MaxStep(sine));
    }

    private static int MaxStep(short[] samples) =>
        Enumerable.Range(1, samples.Length - 1).Max(i => Math.Abs(samples[i] - samples[i - 1]));

    private static short[] Rendered(Cue cue, double pan) =>
        CueWave.Stereo(CueWave.Samples(CueCatalogue.For(cue), 100), pan);

    private static short[] Prefix(short[] frames, int length) => frames[..length];

    private static short[] Left(short[] frames) => [.. frames.Where((_, i) => i % 2 == 0)];

    private static short[] Right(short[] frames) => [.. frames.Where((_, i) => i % 2 == 1)];

    private static double Peak(short[] samples) => samples.Max(sample => Math.Abs((int)sample));

    private static int Crossings(short[] samples) =>
        Enumerable.Range(1, samples.Length - 1).Count(i => Math.Sign(samples[i - 1]) != Math.Sign(samples[i]) &&
            samples[i] != 0);

    // A click is a jump between neighbouring samples larger than the steepest sine at this pitch can make.
    private static void AssertNoClick(short[] frames, double maxHertz)
    {
        var limit = (CueWave.Peak(100) * 2 * Math.PI * maxHertz / CueWave.SampleRate) + 2;
        foreach (var ear in new[] { Left(frames), Right(frames) })
        {
            for (var i = 1; i < ear.Length; i++)
            {
                Assert.True(Math.Abs(ear[i] - ear[i - 1]) <= limit, $"a jump of {ear[i] - ear[i - 1]} at sample {i}");
            }
        }
    }
}
