using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The battle feature's keys, each 0 when unbound: the status key and buttons (#631) and the enemy HP readout's
/// (#767). The settings file can rebind each under <c>keys.battle</c> (#661, <see cref="From"/>).</summary>
public sealed record BattleBindings(int StatusButtons, int StatusKey, int EnemyHpButtons, int EnemyHpKey)
{
    /// <summary>L3 in the game's pad mask: XInput's left thumb, <c>0x0002</c> on build 548ddc95…
    /// (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).</summary>
    public const int LeftThumb = 0x0002;

    /// <summary>The H key, code <c>0x48</c>.</summary>
    public const int KeyH = 0x48;

    /// <summary>The status key is H and L3, the maintainer's choice of 2026-11-21. The enemy HP readout is unbound until
    /// #767 finds keys the game leaves free: the menus' details binding it borrowed, F and R3, are the game's keyboard
    /// melee and its auto-battle toggle, which must always reach the game.</summary>
    public static BattleBindings Default { get; } = new(LeftThumb, KeyH, 0, 0);

    /// <summary>The bindings the settings file's <c>keys.battle</c> object names, each missing or unusable one the
    /// default.</summary>
    public static BattleBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new BattleBindings(
            settings.Binding("battle", "statusButtons", Default.StatusButtons),
            settings.Binding("battle", "statusKey", Default.StatusKey),
            settings.Binding("battle", "enemyHpButtons", Default.EnemyHpButtons),
            settings.Binding("battle", "enemyHpKey", Default.EnemyHpKey));
    }
}
