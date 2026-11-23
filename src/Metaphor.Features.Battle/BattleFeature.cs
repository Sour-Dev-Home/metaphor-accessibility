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
    [SettingKey("speech", "battleEnemyKeys", "Battle enemy keys", Testing = true, Order = 1036, Description = "In a " +
        "battle, `K` and `J` step to the next or previous enemy hit and say its HP; `L` says every one's. Needs " +
        "`battleHits`.")]
    BattleEnemyKeys,

    /// <summary>The pad's enemy HP chords (#767): LB held with D-pad Down, Up or Left, as <see cref="BattleEnemyKeys"/>.
    /// Whether holding LB in a battle does its own action first is <c>[NEEDS VERIFICATION]</c> by ear.</summary>
    [SettingKey("speech", "battleEnemyPad", "Battle enemy pad chords", Testing = true, Order = 1037, Description =
        "In a battle, `LB` held with D-pad `Down`, `Up` or `Left` does what `K`, `J` and `L` do. Needs `battleHits`.")]
    BattleEnemyPad,

    /// <summary>The in-turn situation banner: a boss warning, a stat change, "Not enough MP!" (#692, #661).</summary>
    [SettingKey("speech", "battleBanners", "Battle banners", Testing = true, Order = 1070, Description = "The short " +
        "banner during a battle turn: a boss warning, a stat change, \"Not enough MP!\".")]
    BattleBanners,
}
