using Metaphor.Core.Settings;

namespace Metaphor.Core.Input;

/// <summary>The shoulder taps' timing (#835), in milliseconds: how long a lone LB or RB is held before the game gets it
/// as a hold, and how long a tap is replayed for. Both are sliders in the settings file's <c>limits.shoulders</c>, read
/// on each pad read so a changed file applies at once. The defaults are a guess the by-ear check on #835 tunes.
/// </summary>
public readonly record struct ShoulderTapLimits(int HoldDelayMilliseconds, int TapMilliseconds)
{
    /// <summary>The hold delay: 300 ms, 100 to 1000 in steps of 50.</summary>
    public static SliderLimit HoldDelay { get; } = new("shoulders", "holdDelay", 300, 100, 1000, 50);

    /// <summary>The tap length: 50 ms, 20 to 200 in steps of 10.</summary>
    public static SliderLimit TapLength { get; } = new("shoulders", "tapLength", 50, 20, 200, 10);

    /// <summary>Both defaults.</summary>
    public static ShoulderTapLimits Default { get; } = new(HoldDelay.Default, TapLength.Default);

    /// <summary>The limits <paramref name="settings"/> give, each within its range.</summary>
    public static ShoulderTapLimits From(ModSettings settings) => new(HoldDelay.From(settings), TapLength.From(settings));
}
