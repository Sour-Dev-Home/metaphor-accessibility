using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>Moving to a screen reader started after the game, and back when it quits (#527). Probes run where the test
/// queues them, so nothing races a thread.</summary>
[Trait("Category", "Unit")]
public sealed class SwitchingAnnouncerTests : IDisposable
{
    private const int Ok = 0;
    private const int Internal = 9; // PRISM_ERROR_INTERNAL: NVDA's RPC failing

    private readonly FakeClock clock = new();
    private readonly List<string> log = [];
    private readonly List<Action> queued = [];
    private readonly FakeBackend oneCore = new("OneCore");
    private FakeBackend? running; // The screen reader findScreenReader would start, if any.
    private int probes;

    private SwitchingAnnouncer Start(ISpeechBackend? screenReader, ISpeechBackend? fallback) =>
        new(screenReader, fallback, Find, log.Add, clock, queued.Add);

    private FakeBackend? Find()
    {
        probes++;
        return running?.FreshCopy();
    }

    private void RunQueued()
    {
        var work = queued.ToList();
        queued.Clear();
        work.ForEach(probe => probe());
    }

    private static Announcement Line(string text) => new(text, Interrupt: false);

    public void Dispose()
    {
        oneCore.Dispose();
        running?.Dispose();
    }

    [Fact]
    public void ScreenReaderStartedLaterGetsTheNextLineAndTheSwitchIsLoggedOnce()
    {
        var speech = Start(null, oneCore);
        speech.Announce(Line("one"));
        running = new FakeBackend("NVDA");
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("two")); // Said on OneCore, then a probe is queued.
        RunQueued();
        speech.Announce(Line("three"));
        clock.Advance(SwitchingAnnouncer.ProbeInterval);
        speech.Announce(Line("four"));

        Assert.Equal(["one", "two"], oneCore.Said);
        Assert.Equal(["three", "four"], running.Copies.Single().Said);
        Assert.Equal(["speech: NVDA"], log);
        Assert.Equal("NVDA", speech.BackendName);
        Assert.Empty(queued); // Healthy on a screen reader: no more probes.
    }

    [Fact]
    public void ScreenReaderQuittingMovesBackToTheFallbackAndSaysTheFailedLineThere()
    {
        var nvda = new FakeBackend("NVDA");
        var speech = Start(nvda, oneCore);
        speech.Announce(Line("one"));
        nvda.Status = Internal; // NVDA quit.
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("two"));
        RunQueued();
        speech.Announce(Line("three"));

        Assert.Equal(["one", "two"], nvda.Said);
        Assert.Equal(["two", "three"], oneCore.Said);
        Assert.Equal(["speech: OneCore"], log);
        Assert.True(nvda.Disposed);
        Assert.False(oneCore.Disposed);
    }

    [Fact]
    public void FailedLineOnAReaderStillRunningIsNotSaidTwice()
    {
        var nvda = new FakeBackend("NVDA") { Status = Internal }; // Spoken, then braille failed.
        running = new FakeBackend("NVDA");
        var speech = Start(nvda, oneCore);
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        RunQueued();
        speech.Announce(Line("two"));

        Assert.Empty(oneCore.Said);
        Assert.Equal(["two"], running.Copies.Single().Said); // A fresh connection, without "one" again.
        Assert.Empty(log);
        Assert.True(nvda.Disposed);
    }

    [Fact]
    public void ProbesAtMostOncePerIntervalWhileOnTheFallback()
    {
        var speech = Start(null, oneCore);

        for (var i = 0; i < 10; i++)
        {
            speech.Announce(Line("line"));
            RunQueued();
        }
        Assert.Equal(0, probes); // The caller probed at start.

        clock.Advance(SwitchingAnnouncer.ProbeInterval);
        for (var i = 0; i < 10; i++)
        {
            speech.Announce(Line("line"));
            RunQueued();
        }
        Assert.Equal(1, probes);
        Assert.Equal(20, oneCore.Said.Count);
    }

    [Fact]
    public void NeverProbesTwiceAtOnce()
    {
        var speech = Start(null, oneCore);
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        clock.Advance(SwitchingAnnouncer.ProbeInterval);
        speech.Announce(Line("two"));

        Assert.Single(queued);
    }

    [Fact]
    public void ProbesNeverRunOnTheSpeakingThread()
    {
        var speech = Start(null, oneCore);
        running = new FakeBackend("NVDA");
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));

        Assert.Equal(0, probes);
        Assert.Single(queued);
    }

    [Fact]
    public void WithNothingAtLoadAScreenReaderStartedLaterIsUsed()
    {
        var speech = Start(null, null);
        Assert.Null(speech.BackendName);
        running = new FakeBackend("NVDA");
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one")); // Nothing to say it on, so it waits for the switch.
        RunQueued();
        speech.Announce(Line("two"));

        Assert.Equal(["one", "two"], running.Copies.Single().Said);
        Assert.Equal(["speech: NVDA"], log);
    }

    [Fact]
    public void ScreenReaderQuittingWithNoFallbackLogsThatNothingSpeaks()
    {
        var nvda = new FakeBackend("NVDA") { Status = Internal };
        var speech = Start(nvda, null);
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        RunQueued();

        Assert.Null(speech.BackendName);
        Assert.Equal([SwitchingAnnouncer.NoBackend], log);
    }

    [Fact]
    public void NoExceptionEscapes()
    {
        var broken = new FakeBackend("NVDA") { Throws = true };
        var speech = new SwitchingAnnouncer(
            broken, oneCore, () => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
            clock, probe => probe());
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        broken.Throws = false;
        broken.Status = Internal;
        speech.Announce(Line("two")); // Fails, and its probe finds nothing, as the finder throws.
        speech.Announce(Line("three"));

        Assert.Equal(["two", "three"], oneCore.Said);
    }

    [Fact]
    public void ANewReaderThrowingMidSwitchReleasesBothReadersAndSettlesOnTheFallback()
    {
        var nvda = new FakeBackend("NVDA");
        var jaws = new FakeBackend("JAWS") { Throws = true }; // Throws saying the failed line during the switch.
        var speech = new SwitchingAnnouncer(nvda, oneCore, () => jaws, log.Add, clock, queued.Add);
        nvda.Status = Internal;
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        RunQueued();

        Assert.True(nvda.Disposed);
        Assert.True(jaws.Disposed);
        Assert.False(oneCore.Disposed);
        Assert.Equal("OneCore", speech.BackendName);
    }

    [Fact]
    public void AnOldReaderThrowingOnReleaseStillSwitchesToTheNewOne()
    {
        var nvda = new FakeBackend("NVDA") { DisposeThrows = true };
        var jaws = new FakeBackend("JAWS");
        var speech = new SwitchingAnnouncer(nvda, oneCore, () => jaws, log.Add, clock, queued.Add);
        nvda.Status = Internal;
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("one"));
        RunQueued();

        Assert.False(jaws.Disposed);
        Assert.Equal("JAWS", speech.BackendName);
        Assert.Equal(["one"], jaws.Said);
    }

    [Fact]
    public void AQueueThatThrowsDoesNotStopLaterProbes()
    {
        var throwing = true;
        var speech = new SwitchingAnnouncer(null, oneCore, Find, log.Add, clock, probe =>
        {
            if (throwing)
            {
                throw new InvalidOperationException();
            }
            probe();
        });
        clock.Advance(SwitchingAnnouncer.ProbeInterval);
        speech.Announce(Line("one"));
        throwing = false;
        running = new FakeBackend("NVDA");
        clock.Advance(SwitchingAnnouncer.ProbeInterval);

        speech.Announce(Line("two"));

        Assert.Equal("NVDA", speech.BackendName);
    }

    [Fact]
    public void NotImplementedOutputFallsBackToSpeak()
    {
        var sapi = new FakeBackend("SAPI") { OutputStatus = OutputRoute.NotImplemented };
        var speech = Start(null, sapi);

        speech.Announce(Line("one"));
        speech.Announce(Line("two"));

        Assert.Equal(["one", "two"], sapi.Said);
        Assert.Equal(1, sapi.OutputCalls);
    }

    [Fact]
    public void StopStopsTheBackendSpeaking()
    {
        var nvda = new FakeBackend("NVDA");
        var speech = Start(nvda, oneCore);
        speech.Announce(Line("one"));

        speech.StopSpeaking();

        Assert.Equal(1, nvda.Stops);
        Assert.Equal(0, oneCore.Stops);
    }

    [Fact]
    public void AStoppedLineThatFailedIsNotSaidOnTheNextBackend()
    {
        var nvda = new FakeBackend("NVDA") { Status = Internal };
        var speech = Start(nvda, oneCore);
        clock.Advance(SwitchingAnnouncer.ProbeInterval);
        speech.Announce(Line("one")); // Fails on a reader that has quit, and queues a probe.

        speech.StopSpeaking();
        RunQueued(); // Finds no reader, so moves to OneCore.

        Assert.Equal("OneCore", speech.BackendName);
        Assert.Empty(oneCore.Said);
    }

    [Fact]
    public void StopWithNothingToSpeakOnOrABackendThatThrowsDoesNothing()
    {
        Start(null, null).StopSpeaking();
        var broken = new FakeBackend("NVDA") { Throws = true };
        var speech = Start(broken, oneCore);

        speech.StopSpeaking();
        broken.Throws = false;
        speech.Announce(Line("one"));

        Assert.Equal(["one"], broken.Said);
    }

    [Fact]
    public void StartingTheFallbackThrowingReleasesTheScreenReaderAlreadyStarted()
    {
        var nvda = new FakeBackend("NVDA");

        Assert.Throws<InvalidOperationException>(() => SwitchingAnnouncer.StartWith(
            () => nvda, () => throw new InvalidOperationException(), Find, log.Add, clock, queued.Add));

        Assert.True(nvda.Disposed);
    }

    [Fact]
    public void ReleasingAfterAFailedStartKeepsTheStartsOwnException()
    {
        var nvda = new FakeBackend("NVDA") { DisposeThrows = true };

        var thrown = Assert.Throws<InvalidOperationException>(() => SwitchingAnnouncer.StartWith(
            () => nvda, () => throw new InvalidOperationException("fallback"), Find, log.Add, clock, queued.Add));

        Assert.Equal("fallback", thrown.Message);
        Assert.True(nvda.Disposed);
    }

    [Fact]
    public void StartWithStartsTheScreenReaderFirstAndSpeaksOnIt()
    {
        var nvda = new FakeBackend("NVDA");
        var started = new List<string>();

        var speech = SwitchingAnnouncer.StartWith(
            () => { started.Add("NVDA"); return nvda; },
            () => { started.Add("OneCore"); return oneCore; },
            Find, log.Add, clock, queued.Add);
        speech.Announce(Line("one"));

        Assert.Equal(["NVDA", "OneCore"], started);
        Assert.Equal(["one"], nvda.Said);
        Assert.False(nvda.Disposed);
        Assert.False(oneCore.Disposed);
    }

    private sealed class FakeClock : TimeProvider
    {
        private long ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public void Advance(TimeSpan by) => ticks += by.Ticks;
    }

    private sealed class FakeBackend(string name) : ISpeechBackend
    {
        public string Name => name;
        public int Status { get; set; } = Ok;
        public int? OutputStatus { get; set; }
        public bool Throws { get; set; }
        public bool Disposed { get; private set; }
        public int OutputCalls { get; private set; }
        public int Stops { get; private set; }
        public List<string> Said { get; } = [];
        public List<FakeBackend> Copies { get; } = [];

        public FakeBackend FreshCopy()
        {
            var copy = new FakeBackend(name);
            Copies.Add(copy);
            return copy;
        }

        public int Output(string text, bool interrupt)
        {
            OutputCalls++;
            return OutputStatus ?? Speak(text, interrupt);
        }

        public int Speak(string text, bool interrupt)
        {
            if (Throws)
            {
                throw new InvalidOperationException();
            }
            Said.Add(text);
            return Status;
        }

        public int StopSpeaking()
        {
            if (Throws)
            {
                throw new InvalidOperationException();
            }
            Stops++;
            return Status;
        }

        public bool DisposeThrows { get; set; }

        public void Dispose()
        {
            Disposed = true;
            if (DisposeThrows)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
