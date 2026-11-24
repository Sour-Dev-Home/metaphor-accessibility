using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle feature's keys, each 0 when unbound: the status key and buttons (#631) and the enemy HP keys
/// (#767): the next, previous and all-enemies keys, and on the pad <see cref="EnemyHoldButtons"/> held with the next,
/// previous and all-enemies buttons; the HP and MP key (#692); and the enemy look key (#699). The settings file can
/// rebind each under <c>keys.battle</c> (#661, <see cref="From"/>).</summary>
public sealed record BattleBindings(
    int StatusButtons,
    int StatusKey,
    int EnemyHpKey,
    int EnemyNextKey,
    int EnemyPreviousKey,
    int EnemyHoldButtons,
    int EnemyHpButton,
    int EnemyNextButton,
    int EnemyPreviousButton,
    int HpMpKey,
    int EnemyLookKey = BattleBindings.KeyI)
{
    /// <summary>The I key, code <c>0x49</c>: the enemy look key (#699), free of the game's battle keys read aloud on
    /// 2026-11-22 (below) and of the mod's.</summary>
    public const int KeyI = 0x49;

    /// <summary>L3 in the game's pad mask: XInput's left thumb, <c>0x0002</c> on build 548ddc95…
    /// (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).</summary>
    public const int LeftThumb = 0x0002;

    /// <summary>The H key, code <c>0x48</c>.</summary>
    public const int KeyH = 0x48;

    /// <summary>The M key, code <c>0x4D</c>: the HP and MP key (#692), free of the game's battle keys read aloud on
    /// 2026-11-22 (below) and of the mod's.</summary>
    public const int KeyM = 0x4D;

    /// <summary>The status key is H and L3, the maintainer's choice of 2026-11-21. The enemy HP keys are K, J and L,
    /// and LB held with D-pad Down, Up and Left, the maintainer's decision of 2026-11-23 on #767, mirroring the field
    /// list's: the game's battle keys, read aloud on 2026-11-22, are Q, H, F, E, Tab, N, T and 1 to 5, so F (its
    /// keyboard melee) and R3 (its auto-battle toggle) stay with the game. The codes: K <c>0x4B</c>, J <c>0x4A</c>,
    /// L <c>0x4C</c>; LB <c>0x0400</c>, D-pad Down <c>0x0040</c>, Up <c>0x0010</c>, Left <c>0x0080</c> on game build
    /// 548ddc95… (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md). LB going down in a battle opens Tactics (by
    /// ear, session of 2026-11-23, build 0c122d3 Debug, Steam 18330018, #811), so the chords hold it back until it's let
    /// go (<see cref="BattleEnemyHp.HeldShoulders"/>).</summary>
    public static BattleBindings Default { get; } = new(LeftThumb, KeyH, 0x4C, 0x4B, 0x4A, 0x0400, 0x0080, 0x0040, 0x0010, KeyM);

    /// <summary>The bindings the settings file's <c>keys.battle</c> object names, each missing or unusable one the
    /// default.</summary>
    public static BattleBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new BattleBindings(
            settings.Binding("battle", "statusButtons", Default.StatusButtons),
            settings.Binding("battle", "statusKey", Default.StatusKey),
            settings.Binding("battle", "enemyHpKey", Default.EnemyHpKey),
            settings.Binding("battle", "enemyNextKey", Default.EnemyNextKey),
            settings.Binding("battle", "enemyPreviousKey", Default.EnemyPreviousKey),
            settings.Binding("battle", "enemyHoldButtons", Default.EnemyHoldButtons),
            settings.Binding("battle", "enemyHpButton", Default.EnemyHpButton),
            settings.Binding("battle", "enemyNextButton", Default.EnemyNextButton),
            settings.Binding("battle", "enemyPreviousButton", Default.EnemyPreviousButton),
            settings.Binding("battle", "hpMpKey", Default.HpMpKey),
            settings.Binding("battle", "enemyLookKey", Default.EnemyLookKey));
    }
}
