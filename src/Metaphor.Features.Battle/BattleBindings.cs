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
    int EnemyLookKey = BattleBindings.KeyI,
    int CommandWheelKey = BattleBindings.KeyC,
    int CommandWheelButton = BattleBindings.DpadUp)
{
    /// <summary>The I key, code <c>0x49</c>: the enemy look key (#699), free of the game's battle keys read aloud on
    /// 2026-11-22 (below) and of the mod's.</summary>
    public const int KeyI = 0x49;

    /// <summary>The C key, code <c>0x43</c>: the command wheel key (#252), free of the game's battle keys read aloud on
    /// 2026-11-22 (docs/raw-sources/battle-key-bindings-2026-11-22.md) and of the mod's.</summary>
    public const int KeyC = 0x43;

    /// <summary>D-pad Up in the game's pad mask, <c>0x0010</c> on build 548ddc95…
    /// (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md): held with <see cref="EnemyHoldButtons"/> (LB), the
    /// command wheel's chord, the maintainer's decision of 2026-11-24 on #252. It was the enemy before's chord, which is
    /// on D-pad Left since #1007.</summary>
    public const int DpadUp = 0x0010;

    /// <summary>L3 in the game's pad mask: XInput's left thumb, <c>0x0002</c> on build 548ddc95…
    /// (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).</summary>
    public const int LeftThumb = 0x0002;

    /// <summary>The B key, code <c>0x42</c>: the status key since #1007, free of the game's battle keys read aloud on
    /// 2026-11-22 (docs/raw-sources/battle-key-bindings-2026-11-22.md) and of the mod's. It was H, the game's Raise
    /// Defences (Guard), which the status key hid from the game in a battle.</summary>
    public const int KeyB = 0x42;

    /// <summary>The M key, code <c>0x4D</c>: the HP and MP key (#692), free of the game's battle keys read aloud on
    /// 2026-11-22 (below) and of the mod's.</summary>
    public const int KeyM = 0x4D;

    /// <summary>The status key is B and L3: L3 the maintainer's choice of 2026-11-21, B since H, the game's Guard, moved
    /// off it (the maintainer's decision of 2026-11-24 on #1007). The enemy HP keys are K, J and L,
    /// and LB held with D-pad Down, Up and Left, the maintainer's decision of 2026-11-23 on #767, mirroring the field
    /// list's: the game's battle keys, read aloud on 2026-11-22, are Q, H, F, E, Tab, N, T and 1 to 5, so F (its
    /// keyboard melee) and R3 (its auto-battle toggle) stay with the game. The codes: K <c>0x4B</c>, J <c>0x4A</c>,
    /// L <c>0x4C</c>; LB <c>0x0400</c>, D-pad Down <c>0x0040</c>, Up <c>0x0010</c>, Left <c>0x0080</c> on game build
    /// 548ddc95… (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md). LB going down in a battle opens Tactics (by
    /// ear, session of 2026-11-23, build 0c122d3 Debug, Steam 18330018, #811), so the chords hold it back until it's let
    /// go (<see cref="BattleEnemyHp.HeldShoulders"/>). D-pad Up with LB is the command wheel's since #252; the enemy
    /// before is LB with D-pad Left and every enemy's HP LB with D-pad Right (<c>0x0020</c>, same source), the
    /// maintainer's decision of 2026-11-24 on #1007.</summary>
    public static BattleBindings Default { get; } =
        new(LeftThumb, KeyB, 0x4C, 0x4B, 0x4A, 0x0400, DpadRight, DpadDown, DpadLeft, KeyM);

    /// <summary>D-pad Down, Left and Right in the game's pad mask, <c>0x0040</c>, <c>0x0080</c> and <c>0x0020</c> on
    /// build 548ddc95… (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md): with LB, the next enemy, the enemy
    /// before and every enemy's HP.</summary>
    public const int DpadDown = 0x0040;

    /// <inheritdoc cref="DpadDown"/>
    public const int DpadLeft = 0x0080;

    /// <inheritdoc cref="DpadDown"/>
    public const int DpadRight = 0x0020;

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
            settings.Binding("battle", "enemyLookKey", Default.EnemyLookKey),
            settings.Binding("battle", "commandWheelKey", Default.CommandWheelKey),
            settings.Binding("battle", "commandWheelButton", Default.CommandWheelButton));
    }
}
