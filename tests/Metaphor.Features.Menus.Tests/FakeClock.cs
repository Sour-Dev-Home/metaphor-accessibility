namespace Metaphor.Features.Menus.Tests;

/// <summary>A clock whose timers fire, on the calling thread, when <see cref="Advance"/> reaches their due time, so a
/// test never sleeps.</summary>
internal sealed class FakeClock : TimeProvider
{
    private readonly List<FakeTimer> timers = [];
    private TimeSpan now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        now += by;
        foreach (var timer in timers.ToArray())
        {
            timer.FireIfDue(now);
        }
    }

    private sealed class FakeTimer(FakeClock clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan? due;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
            return true;
        }

        public void FireIfDue(TimeSpan at)
        {
            if (due is { } dueAt && dueAt <= at)
            {
                due = null;
                callback(state);
            }
        }

        public void Dispose() => due = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
