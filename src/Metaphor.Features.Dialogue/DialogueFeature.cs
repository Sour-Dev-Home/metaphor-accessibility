using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>The dialogue project's features, each with its own on/off key in the settings file (#661, #784). A new
/// feature gets a member here with its <see cref="SettingKeyAttribute"/> (a description and an <c>Order</c> unique across
/// the projects), a <c>[FeatureSetting]</c> on its classes and a gate in <see cref="DialogueModule"/>; the architecture
/// test says so when one is missing. A member still waiting for its by-ear check carries <c>Testing = true</c> and is
/// off until the check passes (#783).</summary>
public enum DialogueFeature
{
    /// <summary>Dialogue lines, choices and the dialogue keys (#58, #95).</summary>
    [SettingKey("speech", "dialogue", "Dialogue", Order = 1000, Description = "Dialogue lines and reply choices. Off " +
        "also leaves the repeat and reader keys to the game in dialogue.")]
    Dialogue,

    /// <summary>Skill and condition names said for their tags in a line (#108). Separate from the dialogue while it
    /// is tested; off, a tag says its stand-in.</summary>
    [SettingKey("speech", "termNames", "Term names", Order = 1180, Description = "The skill and " +
        "condition names a line's tags stand for. Off says the tag's stand-in.")]
    TermNames,

    /// <summary>Archetype, enemy and Royal Virtue names said for their tags in a line (#108, #83), pinned by the lines
    /// around them but not yet heard. Separate from the term names while it is tested.</summary>
    [SettingKey("speech", "moreTermNames", "More term names", Testing = true, Order = 1181, Description = "The " +
        "Archetype, enemy and Royal Virtue names a line's tags stand for. Off, an Archetype says its stand-in and the " +
        "others are left out. Needs `termNames`.")]
    MoreTermNames,

    /// <summary>A name tag whose name can't be read saying "name", reported in Debug builds, rather than being dropped
    /// (#107). Separate from the dialogue while it is tested.</summary>
    [SettingKey("speech", "nameStandIn", "Name stand-in", Testing = true, Order = 1183, Description = "A name a " +
        "line's tag stands for that the mod can't read said as \"name\", rather than left out.")]
    NameStandIn,

    /// <summary>Button icons saying the button the screen draws on a controller, and "key" on the keyboard (#109).
    /// Separate from the dialogue while it is tested.</summary>
    [SettingKey("speech", "buttonNames", "Button names", Order = 1185, Description = "A button " +
        "icon in a line said as the controller button it shows, where known, or as \"key\" after keyboard input. " +
        "Needs `termNames`.")]
    ButtonNames,

    /// <summary>A choice saying "Choice, N options", the options spoken as focus moves, not every option up front
    /// (#730). Separate from the dialogue.</summary>
    [SettingKey("speech", "choiceCount", "Choice count", Order = 1190, Description = "A reply choice saying how many " +
        "options there are, with each option said as the focus moves, rather than every option up front. Needs " +
        "`dialogue`.")]
    ChoiceCount,

    /// <summary>A choice's options held until the select window opens, not said at once after the prompt (#744).
    /// Separate from the dialogue while it is tested.</summary>
    [SettingKey("speech", "choiceDelay", "Choice delay", Order = 1200, Description = "A reply " +
        "choice's options waiting until they are on screen, not said straight after the prompt. Needs `dialogue`.")]
    ChoiceDelay,

    /// <summary>A dialogue window closing stopping the line still being spoken (#676). Separate from the dialogue
    /// while it is tested.</summary>
    [SettingKey("speech", "windowCloseStop", "Window close stop", Order = 1210, Description = "A " +
        "closed dialogue window stopping the line still being spoken. Needs `dialogue`.")]
    WindowCloseStop,

    /// <summary>The context keys in dialogue (#879): stop the speech, who is speaking, the line on screen. Its own
    /// setting while it is tested.</summary>
    [SettingKey("speech", "dialogueKeys", "Dialogue keys", Testing = true, Order = 1213, Description = "In " +
        "dialogue, the keys that stop the speech, say who is speaking and say the line on screen, with the reader on " +
        "or off.")]
    DialogueKeys,

    /// <summary>The game's day-count lines and arrival narration, spoken when the window shows them (#908). Its own
    /// setting while it is tested.</summary>
    [SettingKey("speech", "narration", "Narration", Testing = true, Order = 1217, Description = "The day count " +
        "(\"1 day remains\") and the narration that opens a place, said without a speaker, after the line being " +
        "said rather than over it. Needs `dialogue`.")]
    Narration,

    /// <summary>The game's message log read line by line as the player moves through it (#685). Its own setting while
    /// it is tested.</summary>
    [SettingKey("speech", "messageLog", "Message log", Testing = true, Order = 1215, Description = "The game's " +
        "message log, the backlog of recent dialogue, saying its name and each line's position as you move through " +
        "it. The lines' words are still to come.")]
    MessageLog,
}
