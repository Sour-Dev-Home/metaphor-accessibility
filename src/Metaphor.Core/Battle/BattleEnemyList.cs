namespace Metaphor.Core.Battle;

/// <summary>Told the battle's enemies (#750): each enemy's unit record, in the battle package's list order, when the
/// list may have changed. A record is an identity only, the one each hit names (<see cref="BattleHit.Target"/>).</summary>
public interface IBattleEnemyObserver
{
    void Enemies(long package, IReadOnlyList<long> records);
}

/// <summary>An observer that ignores the enemy list: what the package hook reports to when nothing uses it.</summary>
public sealed class NoBattleEnemies : IBattleEnemyObserver
{
    public void Enemies(long package, IReadOnlyList<long> records)
    {
    }
}

/// <summary>Reads a battle unit's HP and MP from its record (#750, #692): an enemy not yet hit, or a party member's MP.
/// </summary>
public interface IBattleUnits
{
    /// <summary>The unit record's HP now, or null when unreadable.</summary>
    int? Hp(long record);

    /// <summary>The unit record's MP now, or null when unreadable.</summary>
    int? Mp(long record);
}

/// <summary>Walks the battle package's enemy list (#750), as kept here so it is tested without the game. On build
/// 548ddc95… the list is at package <c>+0x128</c> (head <see cref="HeadAt"/>, count <see cref="CountAt"/>); a node's
/// next is at <c>+0x0</c> and its action at <c>+0x28</c>, the action's unit at <c>+0x30</c> and the unit's record at
/// <c>+0x8</c> (docs/findings/battle-enemy-list-maps-hits-to-slots-static.md). The list holds the encounter's enemies in
/// its slot order, which is not yet known to be the order on screen
/// (docs/findings/battle-enemy-list-order-and-update-hook-static.md, <c>[NEEDS VERIFICATION]</c>).</summary>
public static class BattleEnemyList
{
    /// <summary>The list's head node, in the package.</summary>
    public const int HeadAt = 0x130;

    /// <summary>The list's count, in the package.</summary>
    public const int CountAt = 0x140;

    /// <summary>The most nodes walked: an encounter has six slots, so more is a misread.</summary>
    public const int MostEnemies = 12;

    private const int NextAt = 0x0;
    private const int ActionAt = 0x28;
    private const int UnitAt = 0x30;
    private const int RecordAt = 0x8;

    /// <summary>Each enemy's unit record in list order, or null when a link can't be read or a record is null.
    /// </summary>
    public static IReadOnlyList<long>? Records(long package, Func<long, long?> readAddress)
    {
        ArgumentNullException.ThrowIfNull(readAddress);
        if (readAddress(package + HeadAt) is not { } node)
        {
            return null;
        }
        var records = new List<long>();
        while (node != 0 && records.Count < MostEnemies)
        {
            if (readAddress(node + ActionAt) is not { } action || action == 0 ||
                readAddress(action + UnitAt) is not { } unit || unit == 0 ||
                readAddress(unit + RecordAt) is not { } record || record == 0 ||
                readAddress(node + NextAt) is not { } next)
            {
                return null;
            }
            records.Add(record);
            node = next;
        }
        return records;
    }
}

/// <summary>Watches the battle package's per-frame update for the enemy list (#750): each frame costs one read, the
/// count, and the list is walked and handed on only when the package is new, the count changed, or the frames stopped
/// for <see cref="GapMilliseconds"/> (between two battles, which may reuse one package). Called on the game's battle
/// thread; not for several threads at once.</summary>
public sealed class BattleEnemyWatch(
    IBattleEnemyObserver observer, Func<long> millisecondsNow, Func<long, long?> readAddress, Func<long, int?> readNumber)
{
    /// <summary>How long without a frame before the next one is taken as a new battle's.</summary>
    public const long GapMilliseconds = 2000;

    private long package;
    private int? count;
    private long lastFrame = long.MinValue / 2;

    /// <summary>One update of <paramref name="framePackage"/>.</summary>
    public void Frame(long framePackage)
    {
        var now = millisecondsNow();
        if (framePackage != package || now - lastFrame > GapMilliseconds)
        {
            // Walked again even at the same count, until a walk succeeds.
            package = framePackage;
            count = null;
        }
        lastFrame = now;
        if (readNumber(framePackage + BattleEnemyList.CountAt) is not { } read || read == count)
        {
            return;
        }
        if (BattleEnemyList.Records(framePackage, readAddress) is not { } records)
        {
            return;
        }
        count = read;
        observer.Enemies(framePackage, records);
    }
}
