using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle project's features, each with its own on/off key in the settings file (#661, #784). A new
/// feature gets a member here as <c>DialogueFeature</c> describes, and a gate in <see cref="BattleModule"/>.</summary>
public enum BattleFeature
{
    /// <summary>Each battle hit said as it lands, the status key and the enemy HP keys (#247, #631, #767).</summary>
    [SettingKey("speech", "battleHits", "Battle hits", Order = 1020, Description = "Each battle hit as it lands, the " +
        "battle status key and the enemy HP keys. Off also leaves those keys to the game.")]
    BattleHits,

    /// <summary>", knocked out", ", defeated" and ", revived" after a hit's number (#254). Separate from the hits.</summary>
    [SettingKey("speech", "battleOutcomes", "Battle outcomes", Order = 1030, Description = "\"Knocked out\", " +
        "\"defeated\" or \"revived\" after a battle hit's number. Needs `battleHits`.")]
    BattleOutcomes,

    /// <summary>Enemy numbering started afresh when a battle ends (#750). Separate from the hits while it is tested.
    /// </summary>
    [SettingKey("speech", "battleNumberReset", "Battle number reset", Testing = true, Order = 1035, Description =
        "Numbers each battle's enemies from \"First enemy\", not on from the battle before. Needs `battleHits`.")]
    BattleNumberReset,

    /// <summary>K and J step through the enemies hit, saying each one's HP, and L says them all (#767). Separate from
    /// the pad's chords, each waiting for its by-ear check.</summary>
    [SettingKey("speech", "battleEnemyKeys", "Battle enemy keys", Order = 1036, Description = "In a " +
        "battle, `K` and `J` step to the next or previous enemy hit and say its HP; `L` says every one's. Needs " +
        "`battleHits`.")]
    BattleEnemyKeys,

    /// <summary>The pad's enemy HP chords (#767): LB held with D-pad Down, Up or Left, as <see cref="BattleEnemyKeys"/>.
    /// LB opens Tactics as it goes down, so it reaches the game only when let go with no chord (#811).</summary>
    [SettingKey("speech", "battleEnemyPad", "Battle enemy pad chords", Testing = true, Order = 1037, Description =
        "In a battle, `LB` held with D-pad `Down`, `Up` or `Left` does what `K`, `J` and `L` do. `LB` tapped alone " +
        "opens Tactics when you let go. Needs `battleHits`.")]
    BattleEnemyPad,

    /// <summary>The "of &lt;max HP&gt;" in an enemy's HP line ("840 of 1200") when a hit has shown the maximum (#854).
    /// Off says the current HP alone, "840 left".</summary>
    [SettingKey("speech", "battleEnemyMaxHp", "Battle enemy max HP", Order = 1038, Description = "The \"of max HP\" " +
        "in an enemy's HP line: \"840 of 1200\". Off says only \"840 left\". Needs `battleEnemyKeys`.")]
    BattleEnemyMaxHp,

    /// <summary>The in-turn situation banner: a boss warning, a stat change (#692, #661).</summary>
    [SettingKey("speech", "battleBanners", "Battle banners", Order = 1070, Description = "The short " +
        "banner during a battle turn: a boss warning, a stat change.")]
    BattleBanners,

    /// <summary>"Your turn, 4 actions" or "Enemy turn" as each side's turn starts (#252).</summary>
    [SettingKey("speech", "battleTurnLine", "Battle turn line", Testing = true, Order = 1072, Description = "Says " +
        "\"Your turn\" with the actions left, or \"Enemy turn\", as each side's turn starts in a battle.")]
    BattleTurnLine,

    /// <summary>"Ambushed" when the enemy strikes first (#626).</summary>
    [SettingKey("speech", "battleStart", "Battle start", Order = 1074, Description = "Says " +
        "\"Ambushed\" when a battle starts with the enemy striking first.")]
    BattleStart,

    /// <summary>"Victory", "Escaped" or "Defeated" as a battle ends (#166).</summary>
    [SettingKey("speech", "battleEnd", "Battle end", Testing = true, Order = 1076, Description = "Says " +
        "\"Victory\", \"Escaped\" or \"Defeated\" as a battle ends.")]
    BattleEnd,
}
