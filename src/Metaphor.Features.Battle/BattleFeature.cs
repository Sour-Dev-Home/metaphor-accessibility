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

    /// <summary>A hit's number alone, with a sound for whose it was (#855): the side's hit sound, placed left to right
    /// by the enemy's place in the battle's list or the party member's slot.</summary>
    [SettingKey("speech", "battleHitNumbers", "Battle hit numbers only", Testing = true, Order = 1032, Description =
        "Each battle hit says only its number, and the hit sound plays from the left for the first enemy or party " +
        "member through to the right for the last, so an attack on every enemy stays short. Needs `battleHits`.")]
    BattleHitNumbers,

    /// <summary>The battle's enemy list from its start (#750, #253): hits numbered by the list, and the enemy HP keys
    /// stepping through every enemy, hit or not, with "n of total".</summary>
    [SettingKey("speech", "battleEnemyList", "Battle enemy list", Testing = true, Order = 1034, Description =
        "Numbers a battle's enemies from its start, so the first hit already says \"Second enemy\", and `K`, `J` and " +
        "`L` take in every enemy, hit or not. Needs `battleHits`.")]
    BattleEnemyList,

    /// <summary>The enemy HP keys starting from the enemy the target cursor is on (#253), read through the cursor's
    /// vtable slot.</summary>
    [SettingKey("speech", "battleEnemyTarget", "Battle targeted enemy first", Testing = true, Order = 1033,
        Description = "After the target cursor moves to an enemy, `K` or `J` says that enemy first, then steps on " +
        "from it, and `L` starts from it. Needs `battleEnemyList`.")]
    BattleEnemyTarget,

    /// <summary>The enemy the target cursor moves to said with its HP (#253), from the cursor's vtable slot.</summary>
    [SettingKey("speech", "battleTargetSpeech", "Battle target cursor", Testing = true, Order = 1031, Description =
        "As the target cursor moves to an enemy, says it with its HP: \"Second enemy, 840 of 1320, 2 of 3\". Needs " +
        "`battleHits` and `battleEnemyList`.")]
    BattleTargetSpeech,

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

    /// <summary>A key that says the party's HP and MP in a battle (#692): <c>M</c>, no pad button yet.</summary>
    [SettingKey("speech", "battleHpMp", "Battle HP and MP key", Testing = true, Order = 1039, Description = "In a " +
        "battle, `M` says each party member's HP and MP. Needs `battleHits`.")]
    BattleHpMp,

    /// <summary>A skill's HP or MP cost after its name in the battle skill and Synthesis lists (#692 (b), #260), from
    /// the game's skill tables.</summary>
    [SettingKey("speech", "battleSkillCosts", "Battle skill costs", Testing = true, Order = 1041, Description =
        "Says what a skill costs after its name in the battle skill list and Synthesis list, such as \"4 MP\" or " +
        "\"15 percent HP\". The Synthesis list says its turn icons too. Needs `battleLists`.")]
    BattleSkillCosts,

    /// <summary>The battle Synthesis list's rows, as the skill list's (#260).</summary>
    [SettingKey("speech", "battleSynthesis", "Battle Synthesis list", Testing = true, Order = 1042, Description =
        "Says each Synthesis in battle by name with its position, and `F` reads its description. Needs " +
        "`battleLists`.")]
    BattleSynthesis,

    /// <summary>An enemy's found affinities after its line on the enemy keys and the target cursor (#164, #255), from
    /// the game's enemy tables.</summary>
    [SettingKey("speech", "battleAffinities", "Battle affinities", Testing = true, Order = 1043, Description =
        "After an enemy's HP on `K`, `J` and the target cursor, says the affinities you have found by hitting it " +
        "with a skill: \"weak to Fire, resists Ice\". Needs `battleEnemyKeys` or `battleTargetSpeech`.")]
    BattleAffinities,

    /// <summary>Only the affinities found in play, as the screen shows them (the maintainer's decision of 2026-11-24 on
    /// #164). Off says every affinity in the game's tables, found or not.</summary>
    [SettingKey("speech", "battleAffinitiesFoundOnly", "Battle affinities found only", Order = 1044, Description =
        "The affinities line says only what you have found in play this session. Off reveals every affinity from " +
        "the game's tables, found or not. Needs `battleAffinities`.")]
    BattleAffinitiesFoundOnly,

    /// <summary>The Order menu's Party screen (#258): each battle member's name, HP and MP.</summary>
    [SettingKey("speech", "battlePartyScreen", "Battle party screen", Testing = true, Order = 1049, Description =
        "On the battle Order menu's Party screen, says each member's name, HP and MP with their position.")]
    BattlePartyScreen,

    /// <summary>"Row changed" when a party member's Formation row changes in battle (#259).</summary>
    [SettingKey("speech", "battleFormation", "Battle formation", Testing = true, Order = 1078, Description =
        "In a battle, says \"row changed\" with the member when a party member moves between the front and back " +
        "row.")]
    BattleFormation,

    /// <summary>The in-turn situation banner: a boss warning, a stat change (#692, #661).</summary>
    [SettingKey("speech", "battleBanners", "Battle banners", Order = 1070, Description = "The short " +
        "banner during a battle turn: a boss warning, a stat change.")]
    BattleBanners,

    /// <summary>The name in a banner such as "Bow Scourge Malmorta cannot move due to Stun!" (#739), from message
    /// variable slot 0.</summary>
    [SettingKey("speech", "battleBannerNames", "Battle banner names", Testing = true, Order = 1071, Description =
        "Says who a battle banner is about: \"Bow Scourge Malmorta cannot move due to Stun!\". Off leaves the name " +
        "out. Needs `battleBanners`.")]
    BattleBannerNames,

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

    /// <summary>"Gained 240 money and 30 MAG" after a won battle (#166), from the purse at its start and end.</summary>
    [SettingKey("speech", "battleRewards", "Battle rewards", Testing = true, Order = 1077, Description = "After a " +
        "won battle, says the money and MAG it gained: \"Gained 240 money and 30 MAG\".")]
    BattleRewards,

    /// <summary>The EXP and archetype EXP of the defeated enemies by the game's enemy tables, added to the rewards line
    /// (#166). Whether the results screen scales them is <c>[NEEDS VERIFICATION]</c>.</summary>
    [SettingKey("speech", "battleRewardExp", "Battle reward EXP", Testing = true, Order = 1079, Description = "Adds " +
        "the EXP and archetype EXP of the enemies you beat, from the game's enemy tables, to the rewards line: " +
        "\"Gained 240 money, 30 MAG, 160 EXP and 60 archetype EXP\". Needs `battleRewards`.")]
    BattleRewardExp,
}
