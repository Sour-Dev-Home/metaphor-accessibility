using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The turn line (#252): "Your turn, 4 actions" as the player's turn starts, with ", 1 half" for half icons,
/// and "Enemy turn" as the enemy's does, cutting off any hit lines still queued: queued, it came behind 17 of them by ear
/// on 2026-11-23 (build 4d936b2 Debug, Steam 18330018). A turn starts when the side changes or the full icons rise, as a
/// new turn refills them. Any other change in the player's icons is said, queued, as the new count ("2 actions, 3
/// half", #695) once it has held for <see cref="SettleMilliseconds"/>. The idea of speaking turn-economy changes comes
/// from Arcana, the Persona 3 Reload accessibility mod (ChipsAhoiMcCoy/arcana, Zlib, <c>main</c> at
/// <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>, <c>src/screens/battle.cpp</c>); no code was copied (CREDITS.md). Takes each input read only as a clock: it asks <see cref="IBattleScene"/> at
/// most once every <see cref="AskSceneEveryMilliseconds"/>, and while that says battle reads the icons at most once
/// every <see cref="ReadEveryMilliseconds"/>. Every read reaches the game unchanged. Safe to call from any of the
/// game's threads.</summary>
/// <remarks>The side and icon meanings are from four Debug session logs on build 548ddc95… (Steam build 18330018,
/// docs/findings/battle-turn-side-start-and-end-layouts-logged.md). Not yet heard: whether a battle's first turn is
/// missed when the last battle ended on the same side with the same icons, and whether a mid-turn gain ever adds a full
/// icon, are <c>[NEEDS VERIFICATION]</c> by ear. Each member's own turn ("name's turn, HP") needs a read of the command
/// panel's member, not yet made (docs/findings/battle-command-panel-static.md).</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleTurnLine)]
public sealed class TurnLine : IInputControls
{
    /// <summary>The least time between two questions to the scene.</summary>
    public const long AskSceneEveryMilliseconds = 1000;

    /// <summary>The least time between two reads of the icons in a battle.</summary>
    public const long ReadEveryMilliseconds = 100;

    /// <summary>How long a mid-turn count holds before it is said: a guess to check by ear.</summary>
    public const long SettleMilliseconds = 400;

    private readonly IBattleTurnIcons icons;
    private readonly IBattleScene scene;
    private readonly IAnnouncer announcer;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private long sceneAsked = long.MinValue / 2;
    private long iconsRead = long.MinValue / 2;
    private bool inBattle;
    private TurnIcons? last;
    // The icons last said, and a mid-turn change waiting to settle.
    private TurnIcons? spoken;
    private TurnIcons? pending;
    private long pendingSince;

    /// <summary>Turn lines said through <paramref name="announcer"/>, timed by <paramref name="time"/>.</summary>
    public TurnLine(IBattleTurnIcons icons, IBattleScene scene, IAnnouncer announcer, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        this.icons = icons;
        this.scene = scene;
        this.announcer = announcer;
        this.time = time;
    }

    public int Pad(int pad, int buttons, bool made)
    {
        Tick();
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Tick();
        return keys;
    }

    /// <summary>What a turn's start says.</summary>
    public static string Says(TurnIcons turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        if (turn.Side == TurnIcons.EnemySide)
        {
            return "Enemy turn";
        }
        var actions = turn.Full == 1 ? "1 action" : string.Create(CultureInfo.InvariantCulture, $"{turn.Full} actions");
        return turn.Half > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Your turn, {actions}, {turn.Half} half")
            : $"Your turn, {actions}";
    }

    private void Tick()
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        string? said = null;
        var interrupt = false;
        lock (gate)
        {
            if (now - sceneAsked >= AskSceneEveryMilliseconds)
            {
                sceneAsked = now;
                inBattle = scene.InBattle();
            }
            if (!inBattle || now - iconsRead < ReadEveryMilliseconds)
            {
                return;
            }
            iconsRead = now;
            if (icons.Read() is not { } turn)
            {
                return;
            }
            // A player's side with no icons is a turn ending, not starting.
            var started = (last is not { } before || turn.Side != before.Side || turn.Full > before.Full) &&
                (turn.Side == TurnIcons.EnemySide || turn.Full + turn.Half > 0);
            var changed = last is { } previous && previous != turn;
            last = turn;
            if (started)
            {
                said = Says(turn);
                interrupt = true;
                spoken = turn;
                pending = null;
            }
            else if (changed && turn.Side == TurnIcons.PlayerSide && turn.Full + turn.Half > 0)
            {
                // A mid-turn change (#695) waits until the icons settle, so a spend and a gain read apart say one count.
                pending = turn;
                pendingSince = now;
            }
            else if (pending is { } waiting && waiting == turn && now - pendingSince >= SettleMilliseconds)
            {
                pending = null;
                if (waiting != spoken)
                {
                    said = Count(waiting);
                    spoken = waiting;
                }
            }
        }
        if (said is not null)
        {
            announcer.Announce(new Announcement(said, Interrupt: interrupt));
        }
    }

    /// <summary>What a mid-turn change in the player's icons says: "3 actions, 3 half".</summary>
    public static string Count(TurnIcons turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var actions = turn.Full == 1 ? "1 action" : string.Create(CultureInfo.InvariantCulture, $"{turn.Full} actions");
        return turn.Half > 0 ? string.Create(CultureInfo.InvariantCulture, $"{actions}, {turn.Half} half") : actions;
    }
}
