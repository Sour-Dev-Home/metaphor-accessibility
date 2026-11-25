using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The status key (#631, the maintainer's choice of 2026-11-21): in a battle, B on the keyboard pressed alone,
/// or L3 on the pad held alone for <see cref="HoldMilliseconds"/>, says every party member "name HP of max" ("of max"
/// once a hit has shown it), then the enemies this battle has hit, as <see cref="HitLines"/> labels them, with the HP
/// each had left, and the press is hidden from the game. An L3 tap reaches the game (#925, the maintainer's decision of
/// 2026-11-24: Retry is on L3): the shoulder taps (<see cref="ShoulderTaps"/>, #811) hold L3 back while
/// <see cref="HeldButtons"/> says so and hand it on as a tap on release, of the shoulders' tap length, unless the hold
/// spoke. Outside a battle the key does nothing and reaches the game: L3 recentres the world map's cursor (heard by the
/// maintainer, 2026-11-21). Any other read, a chord with B or L3 included, says nothing. A press is an up-to-down edge
/// on any pad or the keyboard, so a mirroring second pad can't fire it again; whether a battle is on is asked once, at
/// that edge, and holds until every source lets go. A status button the taps can't hold back (not L3, a shoulder or a
/// trigger) speaks as it goes down instead. Safe to call from any of the game's threads.</summary>
/// <remarks>L3 is the game's mask bit <c>0x0002</c>, XInput's left thumb, and B is key code <c>0x42</c>, on build
/// 548ddc95… (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md). The game uses L3 in a battle for Retry, heard on
/// that build in the by-ear session of 2026-11-24 (docs/findings/battle-retry-is-on-l3.md). The hold relies on the
/// input frame asking the taps before the controls on each read (<see cref="InputFrame.Pad"/>), so a release after the
/// hold spoke is dropped before the press is forgotten.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHits)]
public sealed class BattleStatus : IInputControls
{
    /// <summary>L3 in the game's pad mask, the default status button.</summary>
    public const int LeftThumb = BattleBindings.LeftThumb;

    /// <summary>The B key, the default status key (#1007).</summary>
    public const int KeyB = BattleBindings.KeyB;

    /// <summary>How long L3 is held alone before it speaks rather than tap the game, by default: 300 ms, the shoulders'
    /// old hold delay (#835), whose setting went with #863. A guess the by-ear check on #925 tunes.</summary>
    public const int HoldMilliseconds = 300;

    /// <summary>The hold time as a slider (#941): 300 ms, 100 to 1,000 in steps of 50.</summary>
    public static SliderLimit HoldLimit { get; } = new("battle", "statusHold", HoldMilliseconds, 100, 1000, 50)
    {
        Spoken = "Status hold time",
        Description = "How long, in milliseconds, `L3` is held alone in a battle before it says the party's HP " +
            "(`battleHits`) instead of reaching the game as a tap (Retry). Raise it if a quick press says the " +
            "status.",
        Order = 75,
    };

    /// <summary>The hold time now, read at each pad read (<see cref="HoldLimit"/>); the default when unset.</summary>
    public Func<int> HoldTime { get; init; } = () => HoldMilliseconds;

    private readonly IBattleParty party;
    private readonly HitLines hits;
    private readonly IBattleScene scene;
    private readonly TimeProvider time;
    private readonly Func<BattleBindings> bindings;
    private readonly Action speak;
    private readonly BattleKey press;
    private readonly Lock gate = new();
    private readonly HashSet<int> holding = [];
    private long heldFrom;
    private bool inBattle;
    private bool chord;
    private bool spoke;

    /// <summary>The status key on the default bindings, B and L3.</summary>
    public BattleStatus(IBattleParty party, HitLines hits, IBattleScene scene, IAnnouncer announcer)
        : this(party, hits, scene, announcer, () => BattleBindings.Default)
    {
    }

    /// <summary>The status key on the bindings in force, read on each input read (#661): <c>keys.battle.statusKey</c>
    /// and <c>statusButtons</c>.</summary>
    public BattleStatus(
        IBattleParty party, HitLines hits, IBattleScene scene, IAnnouncer announcer, Func<BattleBindings> bindings)
        : this(party, hits, scene, announcer, bindings, TimeProvider.System)
    {
    }

    /// <summary>The status key as above, its hold timed by <paramref name="time"/>.</summary>
    public BattleStatus(
        IBattleParty party, HitLines hits, IBattleScene scene, IAnnouncer announcer, Func<BattleBindings> bindings,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        this.party = party;
        this.hits = hits;
        this.scene = scene;
        this.time = time;
        this.bindings = bindings;
        speak = () => announcer.Announce(new Announcement(Status(), Interrupt: true));
        press = new BattleKey(() => bindings().StatusButtons, () => bindings().StatusKey, scene, speak);
    }

    /// <summary>The status buttons the shoulder taps hold back now (#925, <see cref="ShoulderTaps"/>): in a battle, a
    /// status button they can hold, until a hold has spoken, which drops its tap; 0 otherwise.</summary>
    public int HeldButtons()
    {
        var bound = bindings().StatusButtons;
        if (!Holdable(bound))
        {
            return 0;
        }
        lock (gate)
        {
            if (spoke)
            {
                return 0;
            }
        }
        return scene.InBattle() ? bound : 0;
    }

    public int Pad(int pad, int buttons, bool made)
    {
        var bound = bindings().StatusButtons;
        if (!Holdable(bound))
        {
            return press.Pad(pad, buttons, made);
        }
        if (made && Hold(pad, buttons, bound))
        {
            speak();
        }
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => press.Keys(keys);

    /// <summary>What the key says now.</summary>
    public string Status()
    {
        var members = party.Members().OrderBy(member => member.Slot).Select(member => member.HpMax is { } max
            ? hits.MemberSays(member.Slot, member.Unit, string.Create(CultureInfo.InvariantCulture, $"{member.Hp} of {max}"))
            : hits.MemberSays(member.Slot, member.Unit, string.Create(CultureInfo.InvariantCulture, $"{member.Hp} HP")));
        var enemies = hits.KnownEnemies()
            .Select(enemy => string.Create(CultureInfo.InvariantCulture, $"{enemy.Label}, {enemy.Hp}"));
        var parts = new[] { string.Join(", ", members) }.Where(part => part.Length > 0).Concat(enemies).ToList();
        return parts.Count > 0 ? string.Join(". ", parts) : "No party status";
    }

    private static bool Holdable(int bound) => bound != 0 && (bound & ~ShoulderTaps.Holdable) == 0;

    // Whether this read is the one a hold speaks on.
    private bool Hold(int source, int buttons, int bound)
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        lock (gate)
        {
            var held = (buttons & bound) == bound;
            var before = holding.Count > 0;
            if (held)
            {
                holding.Add(source);
            }
            else
            {
                holding.Remove(source);
            }
            if (holding.Count == 0)
            {
                inBattle = false;
                spoke = false;
                return false;
            }
            if (!before)
            {
                heldFrom = now;
                inBattle = scene.InBattle();
                chord = false;
                spoke = false;
            }
            chord |= held && buttons != bound;
            if (!inBattle || chord || spoke || !held || now - heldFrom < HoldTime())
            {
                return false;
            }
            spoke = true;
            return true;
        }
    }
}
