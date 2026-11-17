namespace Metaphor.Core.Field;

/// <summary>Lets a caller that runs every frame through at most once per <paramref name="intervalMilliseconds"/>, so a
/// watch carried by a per-frame hook reads memory only that often. The first call is let through. Safe to call from any
/// thread.</summary>
public sealed class IntervalGate(long intervalMilliseconds, Func<long> millisecondsNow)
{
    private readonly Lock gate = new();
    private long? last;

    /// <summary>True when the interval has passed since the last call that returned true.</summary>
    public bool Due()
    {
        var now = millisecondsNow();
        lock (gate)
        {
            if (last is { } before && now - before < intervalMilliseconds)
            {
                return false;
            }
            last = now;
            return true;
        }
    }
}
