using System.Buffers.Binary;
using Metaphor.Core.Battle;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests;

/// <summary>The enemy tables (#164, #166) in the layout of docs/findings/enemy-tables-affinities-exp-drops.md. Made-up
/// labels, rows and numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class EnemyTablesTests
{
    /// <summary>A row of the made-up enemy table.</summary>
    public sealed record Enemy(int MaxHp, int Exp = 0, int ArchetypeExp = 0, int Magla = 0, int Money = 0, int Level = 1,
        int CommonDrop = 0, int CommonRate = 0);

    /// <summary>An enemy table of <paramref name="rows"/>.</summary>
    public static byte[] Enemies(params Enemy[] rows)
    {
        var bytes = new byte[rows.Length * EnemyTables.EnemyRowSize];
        for (var id = 0; id < rows.Length; id++)
        {
            var row = bytes.AsSpan(id * EnemyTables.EnemyRowSize, EnemyTables.EnemyRowSize);
            BinaryPrimitives.WriteUInt32LittleEndian(row[0x4..], (uint)rows[id].Level);
            BinaryPrimitives.WriteUInt32LittleEndian(row[0x8..], (uint)rows[id].MaxHp);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x26..], (ushort)rows[id].Exp);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x28..], (ushort)rows[id].ArchetypeExp);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x2a..], (ushort)rows[id].Magla);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x2c..], (ushort)rows[id].Money);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x32..], (ushort)rows[id].CommonDrop);
            BinaryPrimitives.WriteUInt16LittleEndian(row[0x34..], (ushort)rows[id].CommonRate);
        }
        return bytes;
    }

    /// <summary>An affinity table: each row's flags by element, the rest plain.</summary>
    public static byte[] Affinities(int rows, params (int Id, AffinityElement Element, AffinityFlags Flags)[] set)
    {
        var bytes = new byte[rows * EnemyTables.AffinityRowSize];
        foreach (var (id, element, flags) in set)
        {
            bytes[id * EnemyTables.AffinityRowSize + (int)element * 2 + 1] = (byte)flags;
        }
        return bytes;
    }

    /// <summary>An id table: each id's <c>EnemyName</c> label, null for a placeholder's empty label.</summary>
    public static byte[] Ids(params string?[] labels)
    {
        var bytes = new byte[EnemyTables.IdHeaderSize + labels.Length * EnemyTables.IdRowSize];
        "ENEMY\0\0\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)labels.Length);
        for (var id = 0; id < labels.Length; id++)
        {
            Convert.FromHexString(MessageLibrary.LabelHash(labels[id] ?? ""))
                .CopyTo(bytes, EnemyTables.IdHeaderSize + id * EnemyTables.IdRowSize);
        }
        return bytes;
    }

    /// <summary>A skill table of element bytes, one per skill id.</summary>
    public static byte[] SkillElements(params byte[] elements)
    {
        var bytes = new byte[elements.Length * 0x22];
        for (var id = 0; id < elements.Length; id++)
        {
            bytes[id * 0x22] = elements[id];
        }
        return bytes;
    }

    private static string Hash(string label) => MessageLibrary.LabelHash(label);

    /// <summary>Three ids: a placeholder, and two that share a name with different max HP.</summary>
    public static EnemyTables Sample() => EnemyTables.Parse(
        Enemies(
            new Enemy(110),
            new Enemy(1440, Exp: 160, ArchetypeExp: 60, Magla: 350, Money: 125, Level: 67, CommonDrop: 0x4000 | 167,
                CommonRate: 4),
            new Enemy(1320, Exp: 150),
            new Enemy(1440, Exp: 90)),
        Affinities(4,
            (1, AffinityElement.Strike, AffinityFlags.Weak), (1, AffinityElement.Slash, AffinityFlags.Resist),
            (1, AffinityElement.Electric, AffinityFlags.Null)),
        Ids(null, "EnemyName_sword_Bone", "EnemyName_sword_Bone", "EnemyName_Slime"),
        SkillElements(0, 3, 19, 4))!;

    [Fact]
    public void ARowHasItsLevelHpRewardsDropAndAffinities()
    {
        var row = Sample().Row(1);

        Assert.NotNull(row);
        Assert.Equal((67, 1440, 160, 60, 350, 125), (row.Level, row.MaxHp, row.Exp, row.ArchetypeExp, row.Magla, row.Money));
        Assert.Equal(new EnemyDrop(4, 167, 4), row.Common);
        Assert.Null(row.Rare);
        Assert.Equal(AffinityFlags.Weak, row.Affinity(AffinityElement.Strike).Flags);
        Assert.Equal(AffinityFlags.Null, row.Affinity(AffinityElement.Electric).Flags);
        Assert.Equal(AffinityFlags.None, row.Affinity(AffinityElement.Fire).Flags);
        Assert.Null(Sample().Row(4));
    }

    [Fact]
    public void ANameGivesItsIdsAndThePlaceholderHasNone()
    {
        var tables = Sample();

        Assert.Equal([1, 2], tables.IdsOf(Hash("EnemyName_sword_Bone").ToLowerInvariant()));
        Assert.Empty(tables.IdsOf(Hash("")));
        Assert.Equal(3, tables.Named);
    }

    [Fact]
    public void MaxHpPicksAmongTheNamesSeenElseAloneWhenUnique()
    {
        var tables = Sample();

        // 1440 is ids 1 and 3: the name seen picks 1; with no name it is ambiguous.
        Assert.Equal(new EnemyIdentity(1, "name+hp", 2), tables.Identify(1440, [Hash("EnemyName_sword_Bone")]));
        Assert.Null(tables.Identify(1440, []));
        Assert.Equal(new EnemyIdentity(2, "hp", 1), tables.Identify(1320, []));
        // The placeholder isn't named, so its HP names nothing.
        Assert.Null(tables.Identify(110, []));
    }

    [Fact]
    public void ASkillHasADamageElementOrNone()
    {
        var tables = Sample();

        Assert.Equal(AffinityElement.Fire, tables.ElementOf(1));
        Assert.Equal(AffinityElement.Ice, tables.ElementOf(3));
        Assert.Null(tables.ElementOf(2));
        Assert.Null(tables.ElementOf(0));
        Assert.Null(tables.ElementOf(99));
    }

    [Fact]
    public void TablesThatArentWholeRowsOrABadIdHeaderParseToNothing()
    {
        var ids = Ids("EnemyName_A");
        Assert.Null(EnemyTables.Parse(new byte[EnemyTables.EnemyRowSize + 1], Affinities(1), ids, []));
        Assert.Null(EnemyTables.Parse(Enemies(new Enemy(1)), new byte[3], ids, []));
        var badMagic = (byte[])ids.Clone();
        badMagic[0] = (byte)'X';
        Assert.Null(EnemyTables.Parse(Enemies(new Enemy(1)), Affinities(1), badMagic, []));
        Assert.Null(EnemyTables.Parse(Enemies(new Enemy(1)), Affinities(1), ids.AsSpan(0, ids.Length - 1), []));
        Assert.NotNull(EnemyTables.Parse(Enemies(new Enemy(1)), Affinities(1), ids, []));
    }
}
