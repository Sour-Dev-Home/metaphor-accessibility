using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle;

/// <summary>Opens each battle by saying how many enemies it has (#251): "Battle: 3 enemies". Queued after whatever is
/// being said, so the line that led into the fight isn't cut off. The enemies' names, and an advantage or ambush, join
/// the line once the battle-state reader finds them (#247).</summary>
public sealed class BattleStartLine : IBattleObserver
{
    private readonly IAnnouncer announcer;

    public BattleStartLine(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    public void Started(BattleStarted battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var enemies = battle.EnemyCount == 1 ? "1 enemy" : $"{battle.EnemyCount} enemies";
        announcer.Announce(new Announcement($"Battle: {enemies}", Interrupt: false));
    }
}
