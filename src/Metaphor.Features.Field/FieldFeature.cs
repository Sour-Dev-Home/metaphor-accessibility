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
    [SettingKey("field", "guidancePause", "Guidance pause", Order = 2040, Description = "The beep " +
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
    [SettingKey("field", "shoulderHold", "Shoulder hold or tap", Order = 2070, Description = "In " +
        "the field, LB and RB reach the game when you let go, so LB with RB (the reader) and LB with the D-pad open " +
        "neither the popularity ranking nor the map. Tapped alone, they still open them.")]
    ShoulderHold,

    /// <summary>The camera-tilt warning and the tilt line in "where am I" (#828).</summary>
    [SettingKey("field", "cameraTilt", "Camera tilt", Order = 2080, Description = "\"Camera looking " +
        "down\" or \"Camera looking up\" when the camera tilts past a limit, \"Camera level\" when it comes back, and " +
        "the camera's tilt at the end of \"where am I\". The limits are in `limits`.")]
    CameraTilt,

    /// <summary>The sonar's pings for nearby people, objects and exits (#827).</summary>
    [SettingKey("field", "sonar", "Sonar", Testing = true, Order = 2090, Description = "A short sound for the " +
        "people, objects and exits nearby, one at a time, nearest first (`field.sonarPings` for more): a sound of its " +
        "own for each kind, on the side it is on screen, louder when nearer. It waits while the guidance beep guides " +
        "or a route ping sounds.")]
    Sonar,

    /// <summary>The wall tones ahead, behind, left and right of the camera (#830).</summary>
    [SettingKey("field", "wallTones", "Wall tones", Order = 2100, Description = "A low steady tone " +
        "for each wall near you, ahead, behind, left and right of the screen, louder as the wall nears. Silent on maps " +
        "the mod has no walking map for, which it says once. The range and volume are in `limits`.")]
    WallTones,

    /// <summary>The route ping toward the place the nearby list steps to (#831).</summary>
    [SettingKey("field", "routePings", "Route pings", Testing = true, Order = 2110, Description = "A ping from the " +
        "direction of each place the nearby list steps to: two high notes when the way is straight, two rising notes " +
        "from the next corner when the way bends, one low note when there is no path. Its volume is in `limits`.")]
    RoutePings,

    /// <summary>The sonar, wall tones and route pings playing the mod's sound files instead of plain tones (#841).
    /// </summary>
    [SettingKey("field", "soundFiles", "Navigation sound files", Testing = true, Order = 2120, Description = "The " +
        "sonar, wall tones and route pings play the mod's own sound files, in `data/sounds` in the mod folder, instead " +
        "of plain tones. A missing or damaged file plays its plain tone.")]
    SoundFiles,

    /// <summary>The sound and line when a field enemy notices you, and the sound when it gives up (#649).</summary>
    [SettingKey("field", "enemyAwareness", "Enemy awareness", Testing = true, Order = 2130, Description = "A sharp " +
        "sound from the enemy's side and \"Enemy noticed you\" when a field enemy near you notices you, and a softer " +
        "falling sound when it gives up the search. Each sound has its own key in `sounds`.")]
    EnemyAwareness,

    /// <summary>"Guide me to the objective": the guidance beep toward the current objective's marker (#652).</summary>
    [SettingKey("field", "objectiveGuidance", "Objective guidance", Order = 2140, Description = "O " +
        "starts the guidance beep toward the current objective's marker on this map, or a waypoint the mod's data " +
        "gives; G stops it. Says \"Objective guidance isn't ready for this area\" on a map the game gives no minimap, " +
        "where the mod can't place the marker.")]
    ObjectiveGuidance,

    /// <summary>The callouts when a strike of yours lands on a field enemy (#696).</summary>
    [SettingKey("field", "fieldStrikes", "Field strike callouts", Order = 2150, Description = "\"Hit\" " +
        "when a strike of yours takes HP from a field enemy near you, and \"Defeated\" when it takes the last.")]
    FieldStrikes,

    /// <summary>The top-left corner advice from Gallica and the party (#587).</summary>
    [SettingKey("field", "cornerAdvice", "Corner advice", Order = 2160, Description = "The advice " +
        "Gallica and the party give in the top-left corner as you roam, with the speaker's name, after the line " +
        "before it.")]
    CornerAdvice,

    /// <summary>The navigation sounds muffled when they come from behind the camera (#903).</summary>
    [SettingKey("field", "behindMuffle", "Behind muffle", Testing = true, Order = 2170, Description = "The sonar, " +
        "route pings, wall tones and guidance beep sound duller and a little quieter when they come from behind " +
        "the screen's facing, so front and back differ. The angle is in `limits`.")]
    BehindMuffle,

    /// <summary>The sonar's and route ping's last sound playing again when the camera turns (#827, #831).</summary>
    [SettingKey("field", "turnPings", "Turn pings", Testing = true, Order = 2180, Description = "When you turn the " +
        "camera, the sonar's last ping and a recent route ping play again from their new side, so turning tells you " +
        "whether a sound is ahead or behind. The angle is in `limits`.")]
    TurnPings,

    /// <summary>The navigation sounds moving toward the centre the farther above or below you they are (#650).</summary>
    [SettingKey("field", "heightPan", "Height pan", Testing = true, Order = 2190, Description = "The sonar, route " +
        "pings and guidance beep sit nearer the centre the farther above or below you their source is.")]
    HeightPan,

    /// <summary>"Above" or "below" in the nearby list for a thing on another level (#650).</summary>
    [SettingKey("field", "nearbyHeight", "Nearby height", Testing = true, Order = 2200, Description = "The nearby " +
        "list says \"above\" or \"below\" after the direction of a thing well above or below you, such as on " +
        "another floor.")]
    NearbyHeight,

    /// <summary>Compass points in the nearby list instead of directions from the screen (#700).</summary>
    [SettingKey("field", "compass", "Compass directions", Testing = true, Order = 2210, Description = "The nearby " +
        "list says compass points, such as \"north-east\", from the map's own axes instead of directions from the " +
        "screen, so they stay the same however the camera turns. North is the top of the game's map.")]
    Compass,

    /// <summary>Scan by direction: RB and the right stick, or the scan key (#701).</summary>
    [SettingKey("field", "stickScan", "Scan by direction", Testing = true, Order = 2220, Description = "Hold RB " +
        "and push the right stick: the nearest thing that way is said, such as \"Left: Chest, 3 steps left\", and " +
        "the camera stays still. U says the nearest thing ahead, left, right and behind.")]
    StickScan,

    /// <summary>Auto-walk toward the guidance beep (#184).</summary>
    [SettingKey("field", "autoWalk", "Auto-walk", Testing = true, Order = 2230, Description = "While the " +
        "guidance beep guides, H, or LB with Y on the pad, walks you toward it by pushing the left stick for you. " +
        "Any button, key or stick you press stops it, and so does arriving, a line, a menu, a battle or a new area. " +
        "Needs a controller connected.")]
    AutoWalk,

    /// <summary>The map cursor with bookmarks (#877): a cursor the player moves over the map to hear what is where.
    /// </summary>
    [SettingKey("field", "mapCursor", "Map cursor", Testing = true, Order = 2240, Description = "Hold V and use " +
        "the arrow keys, or hold LB and push the right stick, to move a cursor over the map 5 steps at a time: it " +
        "says what is there, open floor or wall, and how far and which way it is from you. Let go and it is back on " +
        "you. While it is out, B (LB with R3) bookmarks the spot for this map, K and J (LB with D-pad Down and Up) " +
        "jump to this map's bookmarks, and G (LB with D-pad Right) guides you there with the beep.")]
    MapCursor,
}
