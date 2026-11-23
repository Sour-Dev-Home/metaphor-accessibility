using System.Diagnostics;
using System.Globalization;

namespace Metaphor.Core.Logging;

/// <summary>Times a slow step for its log line (#824), such as a table read from the archive, by the monotonic clock so
/// a system clock change can't skew it.</summary>
public static class StepTimer
{
    /// <summary>The moment a step starts, for <see cref="Took"/>.</summary>
    public static long Start() => Stopwatch.GetTimestamp();

    /// <summary>"in N ms": the whole milliseconds since <paramref name="started"/>.</summary>
    public static string Took(long started) => string.Create(
        CultureInfo.InvariantCulture, $"in {(long)Stopwatch.GetElapsedTime(started).TotalMilliseconds} ms");
}
