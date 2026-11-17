using System.Globalization;

namespace Metaphor.Core.Menus;

// Issue #155: what the development-only menu value listeners decode, kept here so it is tested without the game. Which
// getter a menu calls, and what its arguments mean, is a lead until a run shows it
// (docs/raw-sources/menu-value-getters-2026-11-17.md).

/// <summary>One call to a game getter the #155 spike watches: the getter's name (ours), the arguments it was passed and
/// what it returned, as numbers. No game text.</summary>
public sealed record ValueRead(string Function, IReadOnlyList<long> Arguments, long Result)
{
    /// <summary>The log's form: <c>fn=… args=a,b|none result=…</c>, arguments and result in decimal.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"fn={Function} args={(Arguments.Count == 0 ? "none" : string.Join(',', Arguments.Select(a => a.ToString(CultureInfo.InvariantCulture))))} result={Result}");

    /// <summary>What identifies the read apart from its result: the getter and its arguments.</summary>
    internal string Key => Function + ":" + string.Join(',', Arguments.Select(a => a.ToString(CultureInfo.InvariantCulture)));
}

/// <summary>Keeps the #155 log readable when a menu calls a getter every frame: lets a read through when its result
/// differs from the last one for the same getter and arguments, or when the getter wasn't called with those arguments
/// for a quiet spell (a new visit to the screen). Safe to call from any thread.</summary>
public sealed class ValueReadGate(long quietMilliseconds, Func<long> millisecondsNow)
{
    /// <summary>The most keys remembered; past it the gate forgets them all and starts again.</summary>
    public const int MaxKeys = 4096;

    private readonly Lock gate = new();
    private readonly Dictionary<string, (long Result, long LastSeen)> seen = [];

    /// <summary>True when <paramref name="read"/> is worth logging. Every call counts as a sighting, so a getter called
    /// every frame stays quiet until its result changes.</summary>
    public bool LetThrough(ValueRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var now = millisecondsNow();
        var key = read.Key;
        lock (gate)
        {
            var isNew = !seen.TryGetValue(key, out var last) || last.Result != read.Result ||
                now - last.LastSeen >= quietMilliseconds;
            if (seen.Count >= MaxKeys && !seen.ContainsKey(key))
            {
                seen.Clear();
            }
            seen[key] = (read.Result, now);
            return isNew;
        }
    }
}
