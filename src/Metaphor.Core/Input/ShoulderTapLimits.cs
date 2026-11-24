using Metaphor.Core.Settings;

namespace Metaphor.Core.Input;

/// <summary>The shoulder taps' timing (#835), in milliseconds: how long a tap is replayed for. A slider in the settings
/// file's <c>limits.shoulders</c>, read on each pad read so a changed file applies at once. The default is a guess the
/// by-ear check on #811 tunes. The hold delay it once held went with the hand-over (#835, the maintainer's decision of
/// 2026-11-23): a held shoulder is never handed to the game.</summary>
public readonly record struct ShoulderTapLimits(int TapMilliseconds)
{
    /// <summary>The tap length: 50 ms, 20 to 200 in steps of 10.</summary>
    public static SliderLimit TapLength { get; } = new("shoulders", "tapLength", 50, 20, 200, 10)
    {
        Spoken = "Shoulder tap length",
        Description = "How long, in milliseconds, a lone `LB` or `RB` press holds the button down for the game when " +
            "you let go, in the field (`shoulderHold`) and in a battle, where the mod keeps a held shoulder for its " +
            "chords, and a lone `LT` or `RT` press while `settingsMenu` or `keyHelp` holds them for its chord. Raise " +
            "it if the game misses taps.",
        Order = 70,
    };

    /// <summary>The default.</summary>
    public static ShoulderTapLimits Default { get; } = new(TapLength.Default);

    /// <summary>The limits <paramref name="settings"/> give, within range.</summary>
    public static ShoulderTapLimits From(ModSettings settings) => new(TapLength.From(settings));
}
