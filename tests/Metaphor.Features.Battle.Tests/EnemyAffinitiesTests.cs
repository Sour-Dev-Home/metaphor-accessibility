using System.Buffers.Binary;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>Found affinities (#164, #255; the maintainer's decision of 2026-11-24): a move banner's skill element and a
/// hit on an enemy mark it found for that enemy id, which its max HP and the battle's banner names pick, and the enemy
/// keys and the target line add what was found. Table EXP for the rewards (#166). Made-up tables only.</summary>
[Trait("Category", "Unit")]
public sealed class EnemyAffinitiesTests
{
    private const int Number = 1;
    private const int Fire = 1;
    private const int Ice = 2;
    private const int Heal = 3;
    private const long Bone = 0xA0;
    private const long Slime = 0xB0;
    private const int BoneSerial = 500;

    private readonly ManualTime time = new();
    private readonly HitLines hits;
    private readonly EnemyAffinities affinities;
    private bool foundOnly = true;

    public EnemyAffinitiesTests()
    {
        hits = new HitLines(new SilentAnnouncer(), time, _ => null) { ListOn = () => true };
        var tables = Tables();
        affinities = new EnemyAffinities(
            () => tables, () => hits.Battle, () => time.GetUtcNow().ToUnixTimeMilliseconds(),
            serial => serial == BoneSerial ? "EnemyName_Bone" : null)
        {
            On = () => true,
            FoundOnly = () => foundOnly,
        };
        hits.Enemies(0x5000, [Bone, Slime]);
    }

    // Ids 1 and 2 share the name Bone; 1 and 3 share max HP 1440. Bone (id 1): weak to Fire, resists Ice, nulls
    // Electric. Skills: 1 Fire, 2 Ice, 3 healing.
    private static EnemyTables Tables()
    {
        int[] maxHp = [110, 1440, 1320, 1440];
        int[] exp = [0, 160, 150, 90];
        var enemy = new byte[maxHp.Length * EnemyTables.EnemyRowSize];
        for (var id = 0; id < maxHp.Length; id++)
        {
            var row = enemy.AsSpan(id * EnemyTables.EnemyRowSize);
            BinaryPrimitives.WriteUInt32LittleEndian(row[0x8..], (uint)maxHp[id]);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x26..], (ushort)exp[id]);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x28..], (ushort)(exp[id] / 2));
        }
        var affinity = new byte[maxHp.Length * EnemyTables.AffinityRowSize];
        var bone = EnemyTables.AffinityRowSize;
        affinity[bone + (int)AffinityElement.Fire * 2 + 1] = (byte)AffinityFlags.Weak;
        affinity[bone + (int)AffinityElement.Ice * 2 + 1] = (byte)AffinityFlags.Resist;
        affinity[bone + (int)AffinityElement.Electric * 2 + 1] = (byte)AffinityFlags.Null;
        string?[] labels = [null, "EnemyName_Bone", "EnemyName_Bone", "EnemyName_Slime"];
        var ids = new byte[EnemyTables.IdHeaderSize + labels.Length * EnemyTables.IdRowSize];
        "ENEMY\0\0\0"u8.CopyTo(ids);
        BinaryPrimitives.WriteUInt32LittleEndian(ids.AsSpan(8), (uint)labels.Length);
        for (var id = 0; id < labels.Length; id++)
        {
            Convert.FromHexString(MessageLibrary.LabelHash(labels[id] ?? ""))
                .CopyTo(ids, EnemyTables.IdHeaderSize + id * EnemyTables.IdRowSize);
        }
        var skills = new byte[4 * 0x22];
        skills[Fire * 0x22] = (byte)AffinityElement.Fire;
        skills[Ice * 0x22] = (byte)AffinityElement.Ice;
        skills[Heal * 0x22] = 19;
        return EnemyTables.Parse(enemy, affinity, ids, skills)!;
    }

    private void Hit(long record, int change, int left, int max, int kind = Number)
    {
        var hit = new BattleHit(record, change, left, kind, BattleSide.Enemy, HpMax: max);
        hits.Observe(hit);
        affinities.Observe(hit);
    }

    private string? Says(long record) => affinities.Says(hits.AllEnemies().Single(enemy => enemy.Target == record));

    [Fact]
    public void ASkillsHitsFindItsElementOnThatEnemyOnly()
    {
        affinities.Named(BoneSerial);
        Assert.Null(Says(Bone));

        affinities.Move(Fire);
        Hit(Bone, -300, 1140, 1440);
        affinities.Move(Ice);
        Hit(Bone, -50, 1090, 1440);

        Assert.Equal("weak to Fire, resists Ice", Says(Bone));
    }

    [Fact]
    public void RevealAllSaysTheWholeTableUnfound()
    {
        affinities.Named(BoneSerial);
        Hit(Bone, -10, 1430, 1440);
        foundOnly = false;

        Assert.Equal("weak to Fire, resists Ice, nulls Electric", Says(Bone));
    }

    [Fact]
    public void ABasicAttackAHealOrAHitAfterThePartyWasHitFindsNothing()
    {
        affinities.Named(BoneSerial);
        affinities.Move(0);
        Hit(Bone, -10, 1430, 1440);
        affinities.Move(Heal);
        Hit(Bone, -10, 1420, 1440);
        affinities.Move(Fire);
        var partyHit = new BattleHit(0x10, -20, 100, Number, BattleSide.Ally, 0, 200);
        affinities.Observe(partyHit);
        Hit(Bone, -10, 1410, 1440);

        Assert.Null(Says(Bone));
    }

    [Fact]
    public void AHitLongAfterTheMoveFindsNothing()
    {
        affinities.Named(BoneSerial);
        affinities.Move(Fire);
        time.Advance(EnemyAffinities.MoveMilliseconds + 1);
        Hit(Bone, -300, 1140, 1440);

        Assert.Null(Says(Bone));
    }

    [Fact]
    public void WhatWasFoundStaysForTheNextBattle()
    {
        affinities.Named(BoneSerial);
        affinities.Move(Fire);
        Hit(Bone, -300, 1140, 1440);
        hits.BattleOver();
        hits.Enemies(0x6000, [0xC0]);
        affinities.Named(BoneSerial);
        Hit(0xC0, -1, 1439, 1440);

        Assert.Equal("weak to Fire", Says(0xC0));
    }

    [Fact]
    public void AnEnemyNoNameOrHpTellsApartSaysNothing()
    {
        // 1440 is ids 1 and 3 and no name was seen.
        affinities.Move(Fire);
        Hit(Bone, -300, 1140, 1440);
        foundOnly = false;

        Assert.Null(Says(Bone));
    }

    [Fact]
    public void RewardsSumTheTableOverTheEnemiesOrNoneWhenOneIsUnknown()
    {
        affinities.Named(BoneSerial);
        Hit(Bone, -1440, 0, 1440);
        Assert.Null(affinities.Rewards(hits.AllEnemies()));

        Hit(Slime, -1320, 0, 1320);
        Assert.Equal((310, 155, 0, 0), affinities.Rewards(hits.AllEnemies()));
    }

    [Fact]
    public void AnEnemyRecordsTableIdTellsItApartWithNoNameOrHp()
    {
        // #921: the 2026-11-24 battle's third enemy was never told apart by name or HP, so its rewards were none.
        var units = new TableIds { [Bone] = 1, [Slime] = 3 };
        var withIds = new EnemyAffinities(
            Tables, () => hits.Battle, () => time.GetUtcNow().ToUnixTimeMilliseconds(), _ => null)
        {
            On = () => true,
            FoundOnly = () => false,
            Units = units,
        };

        Assert.Equal((250, 125, 0, 0), withIds.Rewards(hits.AllEnemies()));
        Assert.Equal("weak to Fire, resists Ice, nulls Electric", withIds.Says(hits.AllEnemies()[0]));
    }

    [Fact]
    public void ATableIdTheHitsMaxHpContradictsIsNotTrusted()
    {
        var units = new TableIds { [Bone] = 2 };
        var withIds = new EnemyAffinities(
            Tables, () => hits.Battle, () => time.GetUtcNow().ToUnixTimeMilliseconds(),
            serial => serial == BoneSerial ? "EnemyName_Bone" : null)
        {
            On = () => true,
            FoundOnly = () => false,
            Units = units,
        };
        withIds.Named(BoneSerial);
        var hit = new BattleHit(Bone, -10, 1430, Number, BattleSide.Enemy, HpMax: 1440);
        hits.Observe(hit);
        withIds.Observe(hit);

        // Id 2 has 1,320 HP; the hit showed 1,440, so the name and HP tell it apart as id 1.
        Assert.Equal("weak to Fire, resists Ice, nulls Electric", withIds.Says(hits.AllEnemies()[0]));
    }

    [Fact]
    public void TheEnemyKeysAndTheTargetLineAddTheAffinities()
    {
        affinities.Named(BoneSerial);
        affinities.Move(Fire);
        Hit(Bone, -300, 1140, 1440);
        var announcer = new RecordingAnnouncer();
        var line = new TargetLine(hits, announcer, time) { Affinities = affinities.Says };
        var enemyHp = new BattleEnemyHp(
            () => BattleBindings.Default, hits, new AlwaysInBattle(), announcer)
        {
            ListOn = () => true,
            Affinities = affinities.Says,
        };

        line.Focused(0x9000, 0);
        enemyHp.Keyboard.Keys([BattleBindings.Default.EnemyNextKey]);
        enemyHp.Keyboard.Keys([]);

        Assert.Equal(
            ["First enemy, 1140 of 1440, 1 of 2, weak to Fire", "First enemy, 1140 of 1440, 1 of 2, weak to Fire"],
            announcer.Said.Select(said => said.Text));
    }

    [Fact]
    public void DescribeGroupsByAffinity()
    {
        var entries = new AffinityEntry[19];
        entries[(int)AffinityElement.Strike] = new(0, AffinityFlags.Weak);
        entries[(int)AffinityElement.Light] = new(0, AffinityFlags.Weak);
        entries[(int)AffinityElement.Dark] = new(0, AffinityFlags.Drain);
        entries[(int)AffinityElement.Wind] = new(0, AffinityFlags.Repel);
        var row = new EnemyRow(1, 1, 1, 0, 0, 0, 0, null, null, entries);

        Assert.Equal("weak to Strike and Light, repels Wind, drains Dark", EnemyAffinities.Describe(row, -1));
        Assert.Null(EnemyAffinities.Describe(row, 0));
    }

    private sealed class AlwaysInBattle : IBattleScene
    {
        public bool InBattle() => true;
    }

    // Each record's table id, as the hook reads it at record +0x8; no HP read.
    private sealed class TableIds : Dictionary<long, int>, IBattleUnits
    {
        public int? Hp(long record) => null;

        public int? Mp(long record) => null;

        public int? TableId(long record) => TryGetValue(record, out var id) ? id : null;
    }
}
