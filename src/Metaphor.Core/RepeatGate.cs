namespace Metaphor.Core;

/// <summary>Keeps a spike log readable when the game calls a function every frame: says whether a record is worth
/// writing, false when the same key was let through less than a window ago. Safe to call from any thread.</summary>
public sealed class RepeatGate(long windowMilliseconds, Func<long> millisecondsNow)
{
    /// <summary>The most keys remembered; past it the gate forgets them all and starts again.</summary>
    public const int MaxKeys = 4096;

    private readonly object gate = new();
    private readonly Dictionary<(long, long), long> lastLetThrough = [];

    /// <summary>True when the key (two numbers a listener picks) wasn't let through within the window, and it now is.
    /// </summary>
    public bool LetThrough(long first, long second)
    {
        var now = millisecondsNow();
        lock (gate)
        {
            if (lastLetThrough.TryGetValue((first, second), out var last) && now - last < windowMilliseconds)
            {
                return false;
            }
            if (lastLetThrough.Count >= MaxKeys)
            {
                lastLetThrough.Clear();
            }
            lastLetThrough[(first, second)] = now;
            return true;
        }
    }
}
