using Metaphor.Core.Battle;

namespace Metaphor.Core.Tests;

/// <summary>The battle package's enemy list (#750): walked from its head through each node's action and unit to the
/// unit record, and handed on only when the package, the count or a gap in the frames says it may have changed.
/// Made-up addresses in a fake memory.</summary>
[Trait("Category", "Unit")]
public sealed class BattleEnemyListTests
{
    private const long Package = 0x1000;

    private readonly Dictionary<long, long> pointers = [];
    private readonly Dictionary<long, int> ints = [];
    private readonly List<(long Package, IReadOnlyList<long> Records)> seen = [];
    private long now;

    private long? Pointer(long address) => pointers.TryGetValue(address, out var value) ? value : null;

    private int? Int32(long address) => ints.TryGetValue(address, out var value) ? value : null;

    // Builds the list at the package: node n at 0x2000 + n×0x100, its action, unit and record after it.
    private void List(long package, params long[] records)
    {
        pointers[package + BattleEnemyList.HeadAt] = records.Length == 0 ? 0 : package + 0x2000;
        for (var i = 0; i < records.Length; i++)
        {
            var node = package + 0x2000 + i * 0x100;
            pointers[node] = i + 1 < records.Length ? node + 0x100 : 0;
            pointers[node + 0x28] = node + 0x40;
            pointers[node + 0x40 + 0x30] = node + 0x80;
            pointers[node + 0x80 + 0x8] = records[i];
        }
        ints[package + BattleEnemyList.CountAt] = records.Length;
    }

    private BattleEnemyWatch Watch() => new(
        new Recorder(seen), () => now, address => Pointer(address), address => Int32(address));

    [Fact]
    public void WalksTheListInOrderToEachUnitRecord()
    {
        List(Package, 0xA0, 0xB0, 0xC0);

        Assert.Equal([0xA0L, 0xB0, 0xC0], BattleEnemyList.Records(Package, Pointer));
    }

    [Fact]
    public void ABrokenLinkEndsTheWalkWithNoList()
    {
        List(Package, 0xA0, 0xB0);
        pointers.Remove(Package + 0x2100 + 0x28);

        Assert.Null(BattleEnemyList.Records(Package, Pointer));
    }

    [Fact]
    public void ALoopingListStopsAtTheMostEnemies()
    {
        List(Package, 0xA0);
        pointers[Package + 0x2000] = Package + 0x2000;

        Assert.Equal(BattleEnemyList.MostEnemies, BattleEnemyList.Records(Package, Pointer)!.Count);
    }

    [Fact]
    public void HandsTheListOnOnceAPackageAppearsThenOnlyWhenTheCountChanges()
    {
        var watch = Watch();
        List(Package, 0xA0, 0xB0);

        watch.Frame(Package);
        now += 16;
        watch.Frame(Package);
        List(Package, 0xB0);
        now += 16;
        watch.Frame(Package);

        Assert.Equal([[0xA0L, 0xB0], [0xB0L]], seen.Select(list => list.Records));
    }

    [Fact]
    public void ANewPackageOrAGapInTheFramesHandsItOnAgain()
    {
        var watch = Watch();
        List(Package, 0xA0);
        List(0x9000, 0xD0);

        watch.Frame(Package);
        now += 16;
        watch.Frame(0x9000);
        now += BattleEnemyWatch.GapMilliseconds + 1;
        watch.Frame(0x9000);

        Assert.Equal([Package, 0x9000L, 0x9000], seen.Select(list => list.Package));
    }

    [Fact]
    public void AnUnreadableCountHandsNothingOn()
    {
        var watch = Watch();

        watch.Frame(Package);

        Assert.Empty(seen);
    }

    private sealed class Recorder(List<(long, IReadOnlyList<long>)> seen) : IBattleEnemyObserver
    {
        public void Enemies(long package, IReadOnlyList<long> records) => seen.Add((package, records));
    }
}
