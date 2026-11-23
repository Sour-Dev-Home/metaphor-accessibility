using System.Reflection;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Settings;

/// <summary>Every feature that speaks, plays a cue or takes a key, each with its own on/off key in the settings file
/// (#661). On by default. A new feature gets a member here with its <see cref="SettingKeyAttribute"/>, a
/// <see cref="FeatureSettingAttribute"/> on its classes, a gate in the wiring and a line in
/// <c>docs/players/reference/settings.md</c>; the architecture test says so when one is missing. A member still
/// waiting for its by-ear check carries <c>Testing = true</c> on its key and is off until the check passes (#783).
/// </summary>
public enum Feature
{
    /// <summary>Dialogue lines, choices and the dialogue keys (#58, #95).</summary>
    [SettingKey("speech", "dialogue", "Dialogue")]
    Dialogue,

    /// <summary>The menus' focus and the details key (#94, #97, #214).</summary>
    [SettingKey("speech", "menus", "Menus")]
    Menus,

    /// <summary>The Calendar's day grid (#193). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "calendar", "Calendar", Testing = true)]
    Calendar,

    /// <summary>The save and load screens' slots (#622). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "saveSlots", "Save slots", Testing = true)]
    SaveSlots,

    /// <summary>The System rows with no pinned setting ID saying their value, by the ID learned at run time (#770).
    /// Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "learnedSettingValues", "Learned setting values", Testing = true)]
    LearnedSettingValues,

    /// <summary>The name entry screen (#585).</summary>
    [SettingKey("speech", "nameEntry", "Name entry")]
    NameEntry,

    /// <summary>The Equipment screen's focus (#217). Separate from the menus while it is tested.</summary>
    [SettingKey("speech", "equipmentMenu", "Equipment menu", Testing = true)]
    EquipmentMenu,

    /// <summary>The travel map's destinations (#545).</summary>
    [SettingKey("speech", "travelMap", "Travel map")]
    TravelMap,

    /// <summary>The place name the field shows on arriving (#586).</summary>
    [SettingKey("speech", "placeNames", "Place names")]
    PlaceNames,

    /// <summary>The title screen's notice of a newer release (#576). The request itself is <c>checkForUpdates</c>.
    /// </summary>
    [SettingKey("speech", "updateNotice", "Update notice")]
    UpdateNotice,

    /// <summary>Tutorial pop-ups and the repeat of their page (#118).</summary>
    [SettingKey("speech", "tutorials", "Tutorials")]
    Tutorials,

    /// <summary>Each battle hit said as it lands, the status key and the enemy HP readout (#247, #631).</summary>
    [SettingKey("speech", "battleHits", "Battle hits")]
    BattleHits,

    /// <summary>Battle's skill and item lists, row by row (#247). Separate from the menus.</summary>
    [SettingKey("speech", "battleLists", "Battle lists")]
    BattleLists,

    /// <summary>The in-turn situation banner: a boss warning, a stat change, "Not enough MP!" (#692, #661).</summary>
    [SettingKey("speech", "battleBanners", "Battle banners", Testing = true)]
    BattleBanners,

    /// <summary>The nearby list and its pad steps (#181).</summary>
    [SettingKey("field", "nearby", "Nearby list")]
    Nearby,

    /// <summary>"Where am I" (#181).</summary>
    [SettingKey("field", "whereAmI", "Where am I")]
    WhereAmI,

    /// <summary>The guidance beep toward a nearby thing, and its keys (#183).</summary>
    [SettingKey("field", "guidance", "Guidance beep")]
    Guidance,

    /// <summary>The guidance beep pausing while the game has control and settling before it returns (#702, #709), not
    /// ending. Separate from the beep while it is tested.</summary>
    [SettingKey("field", "guidancePause", "Guidance pause", Testing = true)]
    GuidancePause,

    /// <summary>The callout when a door or chest comes into reach (#644).</summary>
    [SettingKey("field", "reach", "Reach callouts")]
    Reach,

    /// <summary>Skill and condition names said for their tags in a line (#108). Separate from the dialogue while it
    /// is tested; off, a tag says its stand-in.</summary>
    [SettingKey("speech", "termNames", "Term names", Testing = true)]
    TermNames,

    ///<summary>A choice saying "Choice, N options", the options spoken as focus moves, not every option up front
    /// (#730). Separate from the dialogue while it is tested.</summary>
    [SettingKey("speech", "choiceCount", "Choice count", Testing = true)]
    ChoiceCount,

    /// <summary>A choice's options held until the select window opens, not said at once after the prompt (#744).
    /// Separate from the dialogue while it is tested.</summary>
    [SettingKey("speech", "choiceDelay", "Choice delay", Testing = true)]
    ChoiceDelay,

    /// <summary>The first-launch welcome (#617). Whether it has been said is <c>welcomed</c>.</summary>
    [SettingKey("speech", "welcome", "Welcome")]
    Welcome,

    /// <summary>The mark key, which writes a marker to the report log (#597).</summary>
    [SettingKey("report", "markKey", "Mark key")]
    MarkKey,

    /// <summary>The report log's list of screens and focus moves the mod said nothing for (#597).</summary>
    [SettingKey("report", "silentScreens", "Silent screen list")]
    SilentScreens,
}

/// <summary>Where a <see cref="Feature"/> or a <see cref="Cue"/> sits in the settings file: the key
/// <paramref name="name"/> inside the object <paramref name="group"/>, said as <paramref name="spoken"/> when it
/// changes.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SettingKeyAttribute(string group, string name, string spoken) : Attribute
{
    /// <summary>True for a feature that merged before its by-ear check (#783): off in a normal install unless the
    /// settings file turns it on, on in a session build. Passing the check is a pull request that removes this.</summary>
    public bool Testing { get; init; }

    public string Group { get; } = group;

    public string Name { get; } = name;

    public string Spoken { get; } = spoken;
}

/// <summary>Marks a feature's class with the setting that turns it off (#661). The architecture test requires it on
/// every feature class that speaks, plays a cue or takes a key; the wiring gates the class with the same feature.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FeatureSettingAttribute(params Feature[] features) : Attribute
{
    public IReadOnlyList<Feature> Features { get; } = features;
}

/// <summary>A setting's place in the file, read from its <see cref="SettingKeyAttribute"/>.</summary>
public sealed record SettingKey(string Group, string Name, string Spoken, bool Testing = false)
{
    /// <summary><c>group.name</c>, as log lines name it.</summary>
    public string Path => $"{Group}.{Name}";

    public static SettingKey Of(Feature feature) => Of(typeof(Feature), feature.ToString());

    public static SettingKey Of(Cue cue) => Of(typeof(Cue), cue.ToString());

    private static SettingKey Of(Type type, string member)
    {
        var attribute = type.GetField(member)?.GetCustomAttribute<SettingKeyAttribute>()
            ?? throw new ArgumentOutOfRangeException(nameof(member), member, "no settings key");
        return new(attribute.Group, attribute.Name, attribute.Spoken, attribute.Testing);
    }
}
