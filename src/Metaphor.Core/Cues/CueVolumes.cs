using Metaphor.Core.Settings;

namespace Metaphor.Core.Cues;

/// <summary>The volume of each kind of the mod's own sounds as a share of the master <c>soundCueVolume</c> (#852), each
/// a slider in the settings file's <c>limits.cues</c>, 0 to 100, read when a cue starts and while a tone is held. All
/// default to 100, so every kind plays at the master volume as before. The wall tones and the route pings keep their
/// own levels, <c>limits.field.wallVolume</c> and <c>routePingVolume</c>, which the features apply as they build each
/// tone.</summary>
public static class CueVolumes
{
    /// <summary>The field sonar's pings (#827).</summary>
    public static SliderLimit Sonar { get; } = new("cues", "sonarVolume", 100, 0, 100, 5);

    /// <summary>The guidance beep toward a nearby thing (#183).</summary>
    public static SliderLimit Guidance { get; } = new("cues", "guidanceVolume", 100, 0, 100, 5);

    /// <summary>The one-shot cues, such as the hit sounds and the mark sound.</summary>
    public static SliderLimit Other { get; } = new("cues", "otherVolume", 100, 0, 100, 5);

    /// <summary>The volume, 0 to 100, <paramref name="settings"/> give the kind of sound on <paramref name="channel"/>;
    /// 100 for a channel whose kind has its own level in the field's limits.</summary>
    public static int For(ToneChannel channel, ModSettings settings) => channel switch
    {
        ToneChannel.Sonar => Sonar.From(settings),
        ToneChannel.Guidance => Guidance.From(settings),
        _ => 100,
    };
}
