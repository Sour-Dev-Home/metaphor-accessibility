using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Cues;

/// <summary>Playing cues off the caller's thread (#636, #637): the settings decide whether and how loud, the caller
/// never waits, a backlog is dropped rather than played late, and held tones follow their latest request.</summary>
[Trait("Category", "Unit")]
public sealed class CuePlayerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly List<string> logged = [];

    [Fact]
    public void ACueIsPlayedAtTheSetVolumeAndPlace()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Defaults with { SoundCueVolume = 30 };
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.EnemyHit, 0.5);
        }

        Assert.Equal(Expected(Cue.EnemyHit, 30, 0.5), device.Opened.Single().Frames);
    }

    [Fact]
    public void APlainCueIsPlayedInTheCentre()
    {
        var device = new FakeWaveDevice { Instant = true };
        using (var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add))
        {
            player.Play(Cue.AllyHit);
        }

        Assert.Equal(
            Expected(Cue.AllyHit, ModSettings.Defaults.SoundCueVolume, 0), device.Opened.Single().Frames);
    }

    [Fact]
    public void CuesOffPlaysNothing()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Defaults with { SoundCues = false };
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
            player.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));
        }

        Assert.Empty(device.Opened);
    }

    // Each cue has its own setting (#661): one turned off is silent while the other still plays.
    [Fact]
    public void ACueTurnedOffAloneIsSilentAndTheOtherPlays()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Parse(
            """{ "sounds": { "allyHit": { "on": false } } }""", SettingCatalogue.Empty, logged.Add);
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
            player.Play(Cue.EnemyHit);
        }

        Assert.Equal(
            Expected(Cue.EnemyHit, ModSettings.Defaults.SoundCueVolume, 0), device.Opened.Single().Frames);
    }

    // A cue's own volume is a share of the sound cue volume: 50 of 60 plays at 30.
    [Fact]
    public void ACuesOwnVolumeScalesTheSoundCueVolume()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Parse(
            """{ "soundCueVolume": 60, "sounds": { "enemyHit": { "volume": 50 } } }""", SettingCatalogue.Empty,
            logged.Add);
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.EnemyHit);
        }

        Assert.Equal(Expected(Cue.EnemyHit, 30, 0), device.Opened.Single().Frames);
    }

    // #852: one volume per kind of sound under the master; the one-shot cues' is limits.cues.otherVolume.
    [Fact]
    public void TheOtherCuesVolumeScalesTheOneShotCuesUnderTheMaster()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Parse(
            """{ "soundCueVolume": 60, "limits": { "cues": { "otherVolume": 50 } } }""", SettingCatalogue.Empty,
            logged.Add);
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.EnemyHit);
        }

        Assert.Equal(Expected(Cue.EnemyHit, 30, 0), device.Opened.Single().Frames);
    }

    [Fact]
    public void ASonarVolumeOfZeroSilencesOnlyTheSonar()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Parse(
            """{ "limits": { "cues": { "sonarVolume": 0 } } }""", SettingCatalogue.Empty, logged.Add);
        using var player = new CuePlayer(device, () => settings, logged.Add);

        player.Hold(ToneChannel.Sonar, new HeldTone(440, 0, 100));
        player.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));

        Assert.True(device.Writes.Wait(Wait));
        Assert.Single(device.Opened);
    }

    [Theory]
    [InlineData(ToneChannel.Sonar, 70)]
    [InlineData(ToneChannel.Guidance, 40)]
    [InlineData(ToneChannel.Wall, 100)]
    [InlineData(ToneChannel.Route, 100)]
    public void EachKindsVolumeComesFromItsOwnLimit(ToneChannel channel, int expected)
    {
        var settings = ModSettings.Parse(
            """{ "limits": { "cues": { "sonarVolume": 70, "guidanceVolume": 40, "otherVolume": 10 } } }""",
            SettingCatalogue.Empty, logged.Add);

        Assert.Equal(expected, CueVolumes.For(channel, settings));
        Assert.Equal(10, CueVolumes.Other.From(settings));
    }

    [Fact]
    public void EveryKindDefaultsToTheMasterVolumeSoNothingChanges() =>
        Assert.All(
            [CueVolumes.Sonar, CueVolumes.Guidance, CueVolumes.Other],
            limit => Assert.Equal(100, limit.From(ModSettings.Defaults)));

    [Fact]
    public void VolumeZeroPlaysNothing()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Defaults with { SoundCueVolume = 0 };
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
        }

        Assert.Empty(device.Opened);
    }

    [Fact]
    public void TheSettingIsReadAtEachCue()
    {
        var device = new FakeWaveDevice { Instant = true };
        var settings = ModSettings.Defaults with { SoundCues = false };
        using (var player = new CuePlayer(device, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
            settings = ModSettings.Defaults;
            player.Play(Cue.EnemyHit);
        }

        Assert.Equal(
            Expected(Cue.EnemyHit, ModSettings.Defaults.SoundCueVolume, 0), device.Opened.Single().Frames);
    }

    [Fact]
    public void TheCallerNeverWaitsAndABacklogIsDropped()
    {
        using var entered = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var device = new FakeWaveDevice
        {
            Instant = true,
            BeforeWrite = () =>
            {
                entered.Set();
                if (!gate.Wait(Wait))
                {
                    throw new TimeoutException("the test never opened the gate");
                }
            },
        };
        var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add);

        player.Play(Cue.AllyHit);
        Assert.True(entered.Wait(Wait), "the first cue never reached the device");
        // The player's thread is stuck writing the first cue: these return at once, and only the backlog is kept.
        for (var i = 0; i < 10; i++)
        {
            player.Play(Cue.EnemyHit);
        }
        gate.Set();
        player.Dispose();

        // Of the backlog, one finds the second cue channel free; the rest find both busy and are dropped.
        Assert.Equal(2, device.Opened.Count);
        Assert.Equal(Expected(Cue.EnemyHit, ModSettings.Defaults.SoundCueVolume, 0), device.Opened[1].Frames);
    }

    [Fact]
    public void AHeldToneSoundsUntilStoppedAndFollowsItsLatestChange()
    {
        var device = new FakeWaveDevice { Instant = true };
        using var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add);

        player.Hold(ToneChannel.Guidance, new HeldTone(440, -1, 100));
        Assert.True(device.Writes.Wait(Wait));
        player.Hold(ToneChannel.Guidance, new HeldTone(440, 1, 100));
        var output = device.Opened.Single();
        // Wait for a buffer that is fully right: the change reached the stream.
        Assert.True(SpinWait.SpinUntil(() => output.Buffers.ToArray().Any(FullyRight), Wait));
        player.Silence(ToneChannel.Guidance);
        player.Dispose();

        Assert.Single(device.Opened);
        Assert.True(output.Closed);
        Assert.InRange(Math.Abs((int)output.Frames[^1]), 0, 1);
    }

    [Fact]
    public void ASampleThatDoesntLoopPlaysOnceForEachHold()
    {
        var device = new FakeWaveDevice { Instant = true };
        var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add);
        // One buffer's worth, so each play is one write.
        var sample = TestWav.Sample([.. Enumerable.Repeat((short)1000, CueMixer.FramesPerSlot)]);

        player.Hold(ToneChannel.Sonar, new HeldTone(440, 0, 100, sample));
        Assert.True(device.Writes.Wait(Wait));
        // Another channel's tone makes the player go over the sonar's held sound again: it doesn't start over.
        player.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));
        Assert.True(device.Writes.Wait(Wait));
        var sonar = device.Opened[0];
        Assert.Single(sonar.Buffers);
        player.Hold(ToneChannel.Sonar, new HeldTone(440, 0, 100, sample));
        Assert.True(SpinWait.SpinUntil(() => sonar.Buffers.Count == 2, Wait));
        player.Dispose();

        Assert.Equal(2, sonar.Buffers.Count);
    }

    [Fact]
    public void DisposingFadesOutAHeldToneAndClosesItsChannel()
    {
        var device = new FakeWaveDevice { Instant = true };
        var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add);
        player.Hold(ToneChannel.Wall, new HeldTone(220, 0, 100));
        Assert.True(device.Writes.Wait(Wait));

        player.Dispose();

        var output = device.Opened.Single();
        Assert.True(output.Closed);
        Assert.InRange(Math.Abs((int)output.Frames[^1]), 0, 1);
        Assert.Empty(output.Violations);
    }

    [Fact]
    public void ACueWithNoSoundIsLoggedAndLaterCuesStillPlay()
    {
        var device = new FakeWaveDevice { Instant = true };
        using (var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add))
        {
            player.Play((Cue)99);
            player.Play(Cue.AllyHit);
        }

        Assert.Equal(
            Expected(Cue.AllyHit, ModSettings.Defaults.SoundCueVolume, 0), device.Opened.Single().Frames);
        Assert.Single(logged);
    }

    [Fact]
    public void PlayingAfterDisposeIsIgnored()
    {
        var device = new FakeWaveDevice { Instant = true };
        var player = new CuePlayer(device, () => ModSettings.Defaults, logged.Add);
        player.Dispose();

        player.Play(Cue.AllyHit);
        player.Hold(ToneChannel.Guidance, new HeldTone(440, 0, 100));

        Assert.Empty(device.Opened);
    }

    private static short[] Expected(Cue cue, int volume, double pan) =>
        CueWave.Stereo(CueWave.Samples(CueCatalogue.For(cue), volume), pan);

    private static bool FullyRight(short[] buffer) =>
        buffer.Where((_, i) => i % 2 == 0).All(sample => sample == 0) &&
        buffer.Where((_, i) => i % 2 == 1).Any(sample => sample != 0);
}
