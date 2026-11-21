using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Cues;

/// <summary>Playing cues off the caller's thread (#636): the settings decide whether and how loud, the caller never
/// waits, and a backlog is dropped rather than played late.</summary>
[Trait("Category", "Unit")]
public sealed class CuePlayerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly List<string> logged = [];

    [Fact]
    public void ACueIsPlayedAsItsWavAtTheSetVolume()
    {
        var sink = new FakeSink();
        var settings = ModSettings.Defaults with { SoundCueVolume = 30 };
        using (var player = new CuePlayer(sink, () => settings, logged.Add))
        {
            player.Play(Cue.EnemyHit);
            player.Play(Cue.AllyHit);
        }

        Assert.Equal(
            [CueWave.Wav(CueCatalogue.For(Cue.EnemyHit), 30), CueWave.Wav(CueCatalogue.For(Cue.AllyHit), 30)],
            sink.Played);
    }

    [Fact]
    public void CuesOffPlaysNothing()
    {
        var sink = new FakeSink();
        var settings = ModSettings.Defaults with { SoundCues = false };
        using (var player = new CuePlayer(sink, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
        }

        Assert.Empty(sink.Played);
    }

    [Fact]
    public void VolumeZeroPlaysNothing()
    {
        var sink = new FakeSink();
        var settings = ModSettings.Defaults with { SoundCueVolume = 0 };
        using (var player = new CuePlayer(sink, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
        }

        Assert.Empty(sink.Played);
    }

    [Fact]
    public void TheSettingIsReadAtEachCue()
    {
        var sink = new FakeSink();
        var settings = ModSettings.Defaults with { SoundCues = false };
        using (var player = new CuePlayer(sink, () => settings, logged.Add))
        {
            player.Play(Cue.AllyHit);
            settings = ModSettings.Defaults;
            player.Play(Cue.EnemyHit);
        }

        Assert.Equal([CueWave.Wav(CueCatalogue.For(Cue.EnemyHit), ModSettings.Defaults.SoundCueVolume)], sink.Played);
    }

    [Fact]
    public void TheCallerNeverWaitsAndABacklogIsDropped()
    {
        using var gate = new ManualResetEventSlim();
        var sink = new FakeSink(gate);
        var player = new CuePlayer(sink, () => ModSettings.Defaults, logged.Add);

        player.Play(Cue.AllyHit);
        Assert.True(sink.Entered.Wait(Wait), "the first cue never reached the sink");
        // The sink is stuck on the first cue: these return at once, and only the queue's worth are kept.
        for (var i = 0; i < 10; i++)
        {
            player.Play(Cue.EnemyHit);
        }
        gate.Set();
        player.Dispose();

        Assert.Equal(1 + CuePlayer.Backlog, sink.Played.Count);
    }

    [Fact]
    public void AFailingSinkIsLoggedOnceAndPlayingGoesOn()
    {
        var sink = new FakeSink { FailuresLeft = 2 };
        using (var player = new CuePlayer(sink, () => ModSettings.Defaults, logged.Add))
        {
            // One at a time, so none is dropped as a backlog.
            player.Play(Cue.AllyHit);
            Assert.True(sink.Calls.Wait(Wait));
            player.Play(Cue.AllyHit);
            Assert.True(sink.Calls.Wait(Wait));
            player.Play(Cue.EnemyHit);
        }

        Assert.Single(sink.Played);
        Assert.Equal(["sound cue failed to play (no device); later failures are not logged"], logged);
    }

    [Fact]
    public void PlayingAfterDisposeIsIgnored()
    {
        var sink = new FakeSink();
        var player = new CuePlayer(sink, () => ModSettings.Defaults, logged.Add);
        player.Dispose();

        player.Play(Cue.AllyHit);

        Assert.Empty(sink.Played);
    }

    private sealed class FakeSink(ManualResetEventSlim? gate = null) : ICueSink
    {
        private readonly List<byte[]> played = [];

        public ManualResetEventSlim Entered { get; } = new();

        /// <summary>Released once each call has finished, played or failed.</summary>
        public SemaphoreSlim Calls { get; } = new(0);

        public int FailuresLeft { get; set; }

        public IReadOnlyList<byte[]> Played
        {
            get
            {
                lock (played)
                {
                    return [.. played];
                }
            }
        }

        public void Play(byte[] wav)
        {
            Entered.Set();
            if (gate is not null && !gate.Wait(Wait))
            {
                throw new TimeoutException("the test never opened the gate");
            }
            try
            {
                if (FailuresLeft > 0)
                {
                    FailuresLeft--;
                    throw new InvalidOperationException("no device");
                }
                lock (played)
                {
                    played.Add(wav);
                }
            }
            finally
            {
                Calls.Release();
            }
        }
    }
}
