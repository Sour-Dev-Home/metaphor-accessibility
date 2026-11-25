using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The command wheel read aloud (#252, the maintainer's decision of 2026-11-24). The wheel has buttons and no
/// cursor, so the mod says it whole from what is known of it: <c>keys.battle.commandWheelKey</c> (C) pressed alone says
/// each command with its keyboard key, and LB (<c>enemyHoldButtons</c>) held with <c>commandWheelButton</c> (D-pad Up)
/// says each with its pad button. Each acts as <see cref="BattleKey"/> does: in a battle it is hidden from the game,
/// elsewhere it reaches it; the chord hides only its D-pad button, and the shoulder taps hold LB back
/// (<see cref="HeldShoulders"/>, #811), as the enemy HP chords' do.</summary>
/// <remarks>The pad's buttons are the wheel's on-screen ones (docs/raw-sources/battle-inventory-2026-11-17.md, build
/// 548ddc95…): Archetype Y, Melee X, Guard B, Synthesis A, Retry L3 (the maintainer's note of 2026-11-24 on #925),
/// Auto Battle R3, Analyse LT, Assist RT, Tactics LB, and the D-pad's Formation and Target. Item's and Pass's pad
/// buttons were not read, so they are said by name alone, <c>[NEEDS VERIFICATION]</c>. The keyboard's keys are the
/// game's bindings screen, read aloud on 2026-11-22 (docs/raw-sources/battle-key-bindings-2026-11-22.md), matched to
/// the wheel's names: Skill is Archetype, Wield Arms Melee, Raise Defences Guard, Yield Turn Pass. The wheel is said as
/// a fixed list, so a command the game greys out for a turn is still in it.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHits, BattleFeature.BattleCommandWheel)]
public sealed class CommandWheel : IInputControls
{
    /// <summary>What the keyboard key says.</summary>
    public const string KeyboardLine = "Archetype E, Melee F, Guard H, Synthesis Q, Item 4, Pass N, Retry 1, " +
        "Analyse T, Assist 5, Status 2, Auto Battle 3, Tactics Tab";

    /// <summary>What the pad chord says.</summary>
    public const string PadLine = "Archetype Y, Melee X, Guard B, Synthesis A, Item, Pass, Retry L3, Analyse LT, " +
        "Assist RT, Auto Battle R3, Tactics LB, Formation D-pad up and down, Target D-pad left and right";

    private readonly Func<BattleBindings> bindings;
    private readonly IBattleScene scene;
    private readonly BattleKey key;
    private readonly BattleKey chord;

    /// <summary>The key and chord on the bindings in force, read on each input read (#661).</summary>
    public CommandWheel(IBattleScene scene, IAnnouncer announcer, Func<BattleBindings> bindings)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(bindings);
        this.bindings = bindings;
        this.scene = scene;
        key = new BattleKey(
            () => 0, () => bindings().CommandWheelKey, scene,
            () => announcer.Announce(new Announcement(KeyboardLine, Interrupt: true)));
        chord = new BattleKey(
            () => bindings() is var b && b.CommandWheelButton != 0 ? b.EnemyHoldButtons | b.CommandWheelButton : 0,
            () => 0,
            scene,
            () => announcer.Announce(new Announcement(PadLine, Interrupt: true)),
            hide: () => bindings().CommandWheelButton);
    }

    public int Pad(int pad, int buttons, bool made) => chord.Pad(pad, buttons, made);

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => key.Keys(keys);

    /// <summary>The chord's hold for the shoulder taps (#811): in a battle, the hold buttons while the chord is bound, so
    /// LB is kept from the game (which opens Tactics on it) until it's let go; 0 outside one.</summary>
    public int HeldShoulders()
    {
        var b = bindings();
        return b.CommandWheelButton != 0 && scene.InBattle() ? b.EnemyHoldButtons : 0;
    }
}
