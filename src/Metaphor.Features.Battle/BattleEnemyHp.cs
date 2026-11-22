using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;

namespace Metaphor.Features.Battle;

/// <summary>The details key in a battle (#631, the maintainer's call of 2026-11-22): the menus' details binding, F or the
/// pad's details button, pressed alone in a battle, says the HP each enemy this battle has hit has left, "of max" when
/// a hit has shown it ("First enemy, 840 of 1200. Second enemy, 50 left"), labelled as <see cref="HitLines"/> labels
/// them, and is hidden from the game. It acts as <see cref="BattleKey"/> does, so outside a battle the key reaches the
/// game. The wiring puts it after the menu controls, which take the key first while a battle list is shown.</summary>
/// <remarks>The design is the targeted enemy's HP alone, but which enemy is targeted is <c>[NEEDS VERIFICATION]</c>:
/// the target cursor has no cursor manager and is told its slot only through a virtual call
/// (docs/findings/battle-target-cursor-index-static.md), and how a slot maps to a hit's unit record isn't known. Until
/// it is, every known enemy is said, and each press is logged (Debug builds) with each enemy's record address, to match
/// against a target read in the next session.</remarks>
public sealed class BattleEnemyHp : IInputControls
{
    private readonly HitLines hits;
    private readonly IAnnouncer announcer;
    private readonly Action<string>? log;
    private readonly BattleKey press;

    /// <summary>The key on <paramref name="buttons"/> (the game's pad mask) and <paramref name="key"/> (a virtual-key
    /// code); <paramref name="log"/> hears each press's candidate fields, or null for none.</summary>
    public BattleEnemyHp(
        int buttons, int key, HitLines hits, IBattleScene scene, IAnnouncer announcer, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        this.hits = hits;
        this.announcer = announcer;
        this.log = log;
        press = new BattleKey(buttons, key, scene, Pressed);
    }

    public int Pad(int pad, int buttons, bool made) => press.Pad(pad, buttons, made);

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => press.Keys(keys);

    private void Pressed()
    {
        var enemies = hits.KnownEnemies();
        log?.Invoke(string.Create(CultureInfo.InvariantCulture, $"enemies={enemies.Count}") + string.Concat(
            enemies.Select(enemy => string.Create(CultureInfo.InvariantCulture,
                $" {enemy.Label} record=0x{enemy.Target:x} hp={enemy.Hp} max={(enemy.HpMax is { } m ? m.ToString(CultureInfo.InvariantCulture) : "?")}"))));
        var said = enemies.Count == 0
            ? "No enemy hit yet"
            : string.Join(". ", enemies.Select(enemy => enemy.HpMax is { } max
                ? string.Create(CultureInfo.InvariantCulture, $"{enemy.Label}, {enemy.Hp} of {max}")
                : string.Create(CultureInfo.InvariantCulture, $"{enemy.Label}, {enemy.Hp} left")));
        announcer.Announce(new Announcement(said, Interrupt: true));
    }
}
