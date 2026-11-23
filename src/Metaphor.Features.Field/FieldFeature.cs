using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The field project's features, each with its own on/off key in the settings file (#661, #784). A new
/// feature gets a member here as <c>DialogueFeature</c> describes, and a gate in <see cref="FieldModule"/>.</summary>
public enum FieldFeature
{
    /// <summary>The nearby list and its pad steps (#181).</summary>
    [SettingKey("field", "nearby", "Nearby list", Order = 2000, Description = "The nearby list: N, or LB with Down " +
        "and Up on the D-pad.")]
    Nearby,

    /// <summary>The nearby list's categories and their keys (#697, #661).</summary>
    [SettingKey("field", "nearbyCategories", "Nearby categories", Order = 2010, Description = "The nearby list's " +
        "categories: `]` and `[`, or the pad chord if you bind one.")]
    NearbyCategories,

    /// <summary>"Where am I" (#181).</summary>
    [SettingKey("field", "whereAmI", "Where am I", Order = 2020, Description = "\"Where am I\": L, or LB with Left " +
        "on the D-pad.")]
    WhereAmI,

    /// <summary>The guidance beep toward a nearby thing, and its keys (#183).</summary>
    [SettingKey("field", "guidance", "Guidance beep", Order = 2030, Description = "The guidance beep toward a nearby " +
        "thing: G, or LB with Right on the D-pad.")]
    Guidance,

    /// <summary>The guidance beep pausing while the game has control and settling before it returns (#702, #709), not
    /// ending. Separate from the beep while it is tested.</summary>
    [SettingKey("field", "guidancePause", "Guidance pause", Testing = true, Order = 2040, Description = "The beep " +
        "pausing, not ending, while the game has control (a cutscene or a line), and coming back once free roam has " +
        "held. Needs `guidance`.")]
    GuidancePause,

    /// <summary>The callout when a door or chest comes into reach (#644).</summary>
    [SettingKey("field", "reach", "Reach callouts", Order = 2050, Description = "The callout when a door or chest " +
        "comes into reach: \"Door, in reach\".")]
    Reach,

    /// <summary>The callout when a field enemy is stunned (#772).</summary>
    [SettingKey("field", "stunCallouts", "Stun callouts", Order = 2060, Description = "The callout " +
        "when a field enemy near you is stunned: \"Enemy stunned\".")]
    StunCallouts,

    /// <summary>LB and RB held back in free roam until let go, then tapped to the game if no chord came (#811).
    /// </summary>
    [SettingKey("field", "shoulderHold", "Shoulder hold or tap", Testing = true, Order = 2070, Description = "In " +
        "the field, LB and RB reach the game when you let go, so LB with RB (the reader) and LB with the D-pad open " +
        "neither the popularity ranking nor the map. Tapped alone, they still open them.")]
    ShoulderHold,
}
