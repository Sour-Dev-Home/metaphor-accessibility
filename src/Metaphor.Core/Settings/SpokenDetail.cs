namespace Metaphor.Core.Settings;

/// <summary>Detail that several features speak, so one setting covers all of them (#664, ADR 0022). Here, not in a
/// feature project, because no feature may depend on another; the menus' module lists it with its own settings.</summary>
public enum SpokenDetail
{
    /// <summary>The "of total" after an item's position, in lists, menus and choices: "Tonic, 3 of 37". Off says the
    /// item alone, or its number when it has no name (#664).</summary>
    [SettingKey("speech", "positions", "Positions", Order = 900, Description = "The \"of total\" after a list item's " +
        "number, in lists, menus and choices: \"Tonic, 3 of 37\". Off says just the name.")]
    Positions,
}
