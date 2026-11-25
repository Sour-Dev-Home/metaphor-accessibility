using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle project's keys, for the help key and the settings menu's key list (#868, #754). Every binding
/// <see cref="BattleBindings.From"/> reads is here, with its default from <see cref="BattleBindings.Default"/>; a test
/// fails when one isn't.</summary>
public static class BattleKeys
{
    private static readonly BattleBindings Defaults = BattleBindings.Default;

    /// <summary>Every key the project lists, all in a battle, each behind the setting that gates its control.</summary>
    public static IReadOnlyList<KeyEntry> All { get; } =
    [
        new(KeyPlaces.Battle, BattleFeature.BattleHits, new(
            "Say each party member's HP and the enemies hit so far",
            new("battle", "statusKey", Defaults.StatusKey), new("battle", "statusButtons", Defaults.StatusButtons))),
        new(KeyPlaces.Battle, BattleFeature.BattleHpMp, new(
            "Say each party member's HP and MP", new("battle", "hpMpKey", Defaults.HpMpKey))),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyKeys, new(
            "Next enemy and its HP", new("battle", "enemyNextKey", Defaults.EnemyNextKey))),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyKeys, new(
            "Enemy before", new("battle", "enemyPreviousKey", Defaults.EnemyPreviousKey))),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyKeys, new(
            "Every enemy's HP", new("battle", "enemyHpKey", Defaults.EnemyHpKey))),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyLooks, new(
            "What the enemy looks like", new("battle", "enemyLookKey", Defaults.EnemyLookKey))),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyPad, new(
            "Next enemy and its HP", Button: new("battle", "enemyNextButton", Defaults.EnemyNextButton), Hold: Hold)),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyPad, new(
            "Enemy before", Button: new("battle", "enemyPreviousButton", Defaults.EnemyPreviousButton), Hold: Hold)),
        new(KeyPlaces.Battle, BattleFeature.BattleEnemyPad, new(
            "Every enemy's HP", Button: new("battle", "enemyHpButton", Defaults.EnemyHpButton), Hold: Hold)),
        new(KeyPlaces.Battle, BattleFeature.BattleCommandWheel, new(
            "Say the command wheel's commands and their keys", new("battle", "commandWheelKey", Defaults.CommandWheelKey),
            new("battle", "commandWheelButton", Defaults.CommandWheelButton), Hold)),
    ];

    private static BoundKey Hold => new("battle", "enemyHoldButtons", Defaults.EnemyHoldButtons);
}
