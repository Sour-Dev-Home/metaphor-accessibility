using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says how many enemies start a battle stunned (#772, #626): "All 3 enemies stunned", "1 of 3 enemies
/// stunned". The battle's enemy list comes just before its start wipe (<see cref="BattleStartLine.Layout"/>); as the
/// wipe plays, each enemy record's condition bits are read once and those with <see cref="StunBit"/> counted. Nothing is
/// said when none is stunned. Safe to call from several threads.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): bit <c>0x40000</c> of the record's <c>+0x1c</c> was set on every
/// enemy of a battle opened by a field stun, in the 2026-11-23 and 2026-11-24 sessions, and on none of an ambush
/// (docs/findings/battle-enemy-record-table-id-and-start-stun.md). That it marks a stun and not another start state, and
/// a battle with only some enemies stunned, are <c>[NEEDS VERIFICATION]</c>. Which enemies, by name or place, isn't
/// said: the list's order is not the screen's (#750).</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleStartStun)]
public sealed class BattleStartStun : IBattleEnemyObserver, ILayoutAnimationObserver
{
    /// <summary>The condition bit an enemy stunned at the start had.</summary>
    public const int StunBit = 0x40000;

    /// <summary>How long before the start wipe the battle's list may come: it came 1.1 s and 2.1 s before in the
    /// 2026-11-24 log; an older list is another battle's.</summary>
    public const long ListMilliseconds = 10_000;

    private readonly IBattleUnits units;
    private readonly IAnnouncer announcer;
    private readonly Func<long> clock;
    private readonly Lock gate = new();
    private IReadOnlyList<long>? records;
    private long listAt;

    public BattleStartStun(IBattleUnits units, IAnnouncer announcer, Func<long> clock)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(clock);
        this.units = units;
        this.announcer = announcer;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [BattleStartLine.Layout];

    /// <summary>Keeps the battle's latest enemy list until its start wipe.</summary>
    public void Enemies(long package, IReadOnlyList<long> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var now = clock();
        lock (gate)
        {
            this.records = [.. records];
            listAt = now;
        }
    }

    /// <summary>Counts the stunned enemies as the start wipe plays, once a list.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout != BattleStartLine.Layout)
        {
            return;
        }
        IReadOnlyList<long>? list;
        var now = clock();
        lock (gate)
        {
            list = records is not null && now - listAt <= ListMilliseconds ? records : null;
            records = null;
        }
        if (list is null || list.Count == 0)
        {
            return;
        }
        var stunned = list.Count(record => units.Conditions(record) is { } bits && (bits & StunBit) != 0);
        if (Says(stunned, list.Count) is { } said)
        {
            announcer.Announce(new Announcement(said, Interrupt: false));
        }
    }

    /// <summary>What <paramref name="stunned"/> of <paramref name="total"/> enemies stunned says, or null for none.
    /// </summary>
    public static string? Says(int stunned, int total) => stunned <= 0 ? null
        : total == 1 ? "Enemy stunned"
        : stunned == total ? string.Create(CultureInfo.InvariantCulture, $"All {total} enemies stunned")
        : string.Create(CultureInfo.InvariantCulture, $"{stunned} of {total} enemies stunned");
}
