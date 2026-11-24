using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The enemy HP keys in a battle (#631, #767; the maintainer's decision of 2026-11-23): the next and previous
/// keys (<c>keys.battle.enemyNextKey</c> and <c>enemyPreviousKey</c>, K and J) step through the enemies this battle has
/// hit, in the order <see cref="HitLines"/> numbers them, and say the one stepped to ("Second enemy, 50 left"); the
/// all-enemies key (<c>enemyHpKey</c>, L) says every one in a line ("First enemy, 840 of 1200. Second enemy, 50
/// left"), "of max" when a hit has shown it. On the pad, <c>enemyHoldButtons</c> (LB) held with the next, previous and
/// all-enemies buttons (D-pad Down, Up and Left) does the same, mirroring the field list. <see cref="Keyboard"/> and
/// <see cref="Pad"/> are separate controls, so each can wait for its own by-ear check. Each key acts as
/// <see cref="BattleKey"/> does: pressed alone in a battle it is hidden from the game, elsewhere it reaches it; a chord
/// hides only its D-pad button, and the shoulder taps hold LB back (#811, <see cref="HeldShoulders"/>). A step keeps to the enemy, not its place, so a newly hit enemy doesn't move it, and a
/// battle's end forgets it. The wiring puts these after the menu controls, which take a key first while a battle list
/// is shown.</summary>
/// <remarks>The design is the targeted enemy's HP alone, but which enemy is targeted is <c>[NEEDS VERIFICATION]</c>:
/// the target cursor has no cursor manager and is told its slot only through a virtual call
/// (docs/findings/battle-target-cursor-index-static.md), and how a slot maps to a hit's unit record isn't known. Until
/// it is, the keys step through the known enemies, and each all-enemies press is logged (Debug builds) with each
/// enemy's record address, to match against a target read in the next session.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHits, BattleFeature.BattleEnemyKeys, BattleFeature.BattleEnemyPad)]
public sealed class BattleEnemyHp
{
    private const string NoEnemy = "No enemy hit yet";

    private readonly Func<BattleBindings> bindings;
    private readonly IBattleScene scene;
    private readonly HitLines hits;
    private readonly IAnnouncer announcer;
    private readonly Action<string>? log;
    private readonly Lock gate = new();
    private long? current;
    private long currentBattle;

    /// <summary>The keys on the bindings in force, read on each input read (#661); <paramref name="log"/> hears each
    /// all-enemies press's candidate fields, or null for none.</summary>
    public BattleEnemyHp(
        Func<BattleBindings> bindings, HitLines hits, IBattleScene scene, IAnnouncer announcer,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        this.bindings = bindings;
        this.scene = scene;
        this.hits = hits;
        this.announcer = announcer;
        this.log = log;
        Keyboard = new ChainedControls(
            new ChainedControls(
                new BattleKey(() => 0, () => bindings().EnemyNextKey, scene, () => Step(1)),
                new BattleKey(() => 0, () => bindings().EnemyPreviousKey, scene, () => Step(-1))),
            new BattleKey(() => 0, () => bindings().EnemyHpKey, scene, All));
        Pad = new ChainedControls(
            new ChainedControls(
                Chord(bindings, b => b.EnemyNextButton, scene, () => Step(1)),
                Chord(bindings, b => b.EnemyPreviousButton, scene, () => Step(-1))),
            Chord(bindings, b => b.EnemyHpButton, scene, All));
    }

    /// <summary>The keyboard's keys, K, J and L by default.</summary>
    public IInputControls Keyboard { get; }

    /// <summary>The pad's chords, LB held with D-pad Down, Up and Left by default.</summary>
    public IInputControls Pad { get; }

    /// <summary>The pad chords' hold for the shoulder taps (#811): in a battle, the hold buttons while any chord is
    /// bound, so LB is kept from the game (which opens Tactics on it) until it's let go; 0 outside one.</summary>
    public int HeldShoulders()
    {
        var b = bindings();
        var bound = b.EnemyHpButton != 0 || b.EnemyNextButton != 0 || b.EnemyPreviousButton != 0;
        return bound && scene.InBattle() ? b.EnemyHoldButtons : 0;
    }

    // The hold buttons with one more, hiding only that one; unbound when that one is.
    private static BattleKey Chord(
        Func<BattleBindings> bindings, Func<BattleBindings, int> button, IBattleScene scene, Action pressed) =>
        new(
            () => bindings() is var b && button(b) != 0 ? b.EnemyHoldButtons | button(b) : 0,
            () => 0,
            scene,
            pressed,
            hide: () => button(bindings()));

    private void Step(int by)
    {
        var battle = hits.Battle;
        var enemies = hits.KnownEnemies();
        if (enemies.Count == 0)
        {
            Say(NoEnemy);
            return;
        }
        KnownEnemy enemy;
        lock (gate)
        {
            var at = current is { } target && currentBattle == battle ? IndexOf(enemies, target) : -1;
            var next = at < 0
                ? (by > 0 ? 0 : enemies.Count - 1)
                : ((at + by) % enemies.Count + enemies.Count) % enemies.Count;
            enemy = enemies[next];
            current = enemy.Target;
            currentBattle = battle;
        }
        Say(Hp(enemy));
    }

    private void All()
    {
        var enemies = hits.KnownEnemies();
        log?.Invoke(string.Create(CultureInfo.InvariantCulture, $"enemies={enemies.Count}") + string.Concat(
            enemies.Select(enemy => string.Create(CultureInfo.InvariantCulture,
                $" {enemy.Label} record=0x{enemy.Target:x} hp={enemy.Hp} max={(enemy.HpMax is { } m ? m.ToString(CultureInfo.InvariantCulture) : "?")}"))));
        Say(enemies.Count == 0 ? NoEnemy : string.Join(". ", enemies.Select(Hp)));
    }

    private static int IndexOf(IReadOnlyList<KnownEnemy> enemies, long target)
    {
        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].Target == target)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Whether a line says "of max HP" when the maximum is known (#854); on by default, read at each line.
    /// </summary>
    public Func<bool> SayMaxHp { get; init; } = () => true;

    private string Hp(KnownEnemy enemy) => enemy.HpMax is { } max && SayMaxHp()
        ? string.Create(CultureInfo.InvariantCulture, $"{enemy.Label}, {enemy.Hp} of {max}")
        : string.Create(CultureInfo.InvariantCulture, $"{enemy.Label}, {enemy.Hp} left");

    private void Say(string text) => announcer.Announce(new Announcement(text, Interrupt: true));
}
