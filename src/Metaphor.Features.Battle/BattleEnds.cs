using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Ends a battle for the hit lines when the game leaves the battle scene (#750), so each battle numbers its
/// enemies from the first: by ear on 2026-11-22 (Steam build 18330018) a battle started within the hit lines' quiet gap
/// went on from the last one's count. Takes each input read only as a clock: while the hit lines remember enemies it
/// asks <see cref="IBattleScene"/> at most once every <see cref="AskEveryMilliseconds"/>, and two answers in a row of
/// no battle end it, so one odd read doesn't. Every read reaches the game unchanged. Safe to call from any of the game's
/// threads.</summary>
/// <remarks>That the scene reads as a battle for the whole of one, and not between two fought back to back, is
/// <c>[NEEDS VERIFICATION]</c> by ear (<see cref="BattleScene"/>).</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleNumberReset)]
public sealed class BattleEnds : IInputControls
{
    /// <summary>The least time between two questions to the scene.</summary>
    public const long AskEveryMilliseconds = 1000;

    private readonly HitLines hits;
    private readonly IBattleScene scene;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private long lastAsked = long.MinValue / 2;
    private int outOfBattle;

    /// <summary>Ends <paramref name="hits"/>' battle when <paramref name="scene"/> stops reading as one, timed by
    /// <paramref name="time"/>.</summary>
    public BattleEnds(HitLines hits, IBattleScene scene, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(time);
        this.hits = hits;
        this.scene = scene;
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

    private void Tick()
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        lock (gate)
        {
            if (now - lastAsked < AskEveryMilliseconds)
            {
                return;
            }
            lastAsked = now;
            if (!hits.RemembersEnemies)
            {
                outOfBattle = 0;
                return;
            }
            outOfBattle = scene.InBattle() ? 0 : outOfBattle + 1;
            if (outOfBattle < 2)
            {
                return;
            }
            outOfBattle = 0;
        }
        hits.BattleOver();
    }
}
