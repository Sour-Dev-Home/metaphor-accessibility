using Metaphor.Core.Battle;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The sound before a number-only hit (#855): the side's hit cue, placed from left to right by the target's
/// place (<see cref="HitLines.Pan"/>), so a player hears which enemy or party member took it.</summary>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHitNumbers)]
public sealed class PlacedHitCue(ICuePlayer cues)
{
    /// <summary>Plays the hit cue for <paramref name="side"/> at place <paramref name="index"/> of
    /// <paramref name="count"/>.</summary>
    public void Play(BattleSide side, int index, int count) =>
        cues.Play(side == BattleSide.Ally ? Cue.AllyHit : Cue.EnemyHit, HitLines.Pan(index, count));
}
