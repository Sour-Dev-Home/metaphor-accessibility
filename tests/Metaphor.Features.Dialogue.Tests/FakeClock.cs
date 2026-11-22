namespace Metaphor.Features.Dialogue.Tests;

/// <summary>A clock that moves only when a test says so, and runs each timer whose time has come as it moves, on the
/// calling thread, so a test of a delay never waits or races.</summary>
internal sealed class FakeClock : TimeProvider
{
    private readonly List<FakeTimer> timers = [];
    private long ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        var target = ticks + by.Ticks;
        while (timers.Where(t => t.Due <= target).MinBy(t => t.Due) is { } next)
        {
            ticks = Math.Max(ticks, next.Due);
            next.Fire();
        }
        ticks = target;
    }

    private sealed class FakeTimer(FakeClock clock, TimerCallback callback, object? state) : ITimer
    {
        public long Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.timers.Remove(this);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                Due = clock.ticks + dueTime.Ticks;
                clock.timers.Add(this);
            }
            return true;
        }

        public void Fire()
        {
            clock.timers.Remove(this);
            callback(state);
        }

        public void Dispose() => clock.timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
