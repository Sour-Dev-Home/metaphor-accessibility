using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Logging;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Battle;

/// <summary>The ten damage elements, in the order of an affinity row's first ten entries and of <c>Skill.TBL</c>'s
/// element byte (docs/raw-sources/metaphor-010-templates-tables-2026-11-17.md,
/// docs/raw-sources/metaphor-010-templates-skill-element-2026-11-24.md).</summary>
public enum AffinityElement
{
    Slash,
    Pierce,
    Strike,
    Fire,
    Ice,
    Electric,
    Wind,
    Light,
    Dark,
    Almighty,
}

/// <summary>An affinity entry's flags byte: bit 0 null, 1 repel, 2 drain, 3 weak, 4 resist, 5 ailment immune, 6 ailment
/// guaranteed, 7 double ailment chance (docs/raw-sources/metaphor-010-templates-tables-2026-11-17.md).</summary>
[Flags]
[SuppressMessage("Design", "CA1028", Justification = "The game's flags byte.")]
[SuppressMessage("Naming", "CA1711", Justification = "A flags enum.")]
public enum AffinityFlags : byte
{
    None = 0,
    Null = 1,
    Repel = 2,
    Drain = 4,
    Weak = 8,
    Resist = 16,
    AilmentImmune = 32,
    AilmentGuaranteed = 64,
    DoubleAilmentChance = 128,
}

/// <summary>One of an enemy's 19 affinities: the multiplier in units of 5 % (0 or 20 is plain) and the flags.</summary>
public readonly record struct AffinityEntry(int Multiplier, AffinityFlags Flags);

/// <summary>A drop an enemy may leave: the item kind (the <c>u16</c>'s high 4 bits), the item id (its low 12) and the
/// rate in percent.</summary>
public sealed record EnemyDrop(int Kind, int Item, int Rate);

/// <summary>An enemy's <c>Enemy.TBL</c> row with its affinities (#164, #166): level <c>+0x4</c>, max HP <c>+0x8</c>,
/// EXP <c>+0x26</c>, archetype EXP <c>+0x28</c>, magla <c>+0x2a</c>, money <c>+0x2c</c>, the common drop at
/// <c>+0x32</c> (rate <c>+0x34</c>) and the rare at <c>+0x36</c> (rate <c>+0x38</c>), on game build 548ddc95…
/// (docs/findings/enemy-tables-affinities-exp-drops.md).</summary>
public sealed record EnemyRow(
    int Id, int Level, int MaxHp, int Exp, int ArchetypeExp, int Magla, int Money, EnemyDrop? Common, EnemyDrop? Rare,
    IReadOnlyList<AffinityEntry> Affinities)
{
    /// <summary>The entry for a damage element, or a plain one when the row is short.</summary>
    public AffinityEntry Affinity(AffinityElement element) =>
        (int)element < Affinities.Count ? Affinities[(int)element] : default;
}

/// <summary>Which enemy id a battle unit is, and how it was told (#164): <c>name+hp</c> when a name seen this battle
/// and the max HP agree on one id, <c>hp</c> when the max HP alone names one.</summary>
public sealed record EnemyIdentity(int Id, string By, int Candidates);

/// <summary>The enemy tables from the game's archive (#164, #255, #166), read offline on game build 548ddc95… (Steam
/// build 18330018) (docs/findings/enemy-tables-affinities-exp-drops.md): <c>Enemy.TBL</c>, 801 rows of <c>0x64</c>
/// bytes; <c>EnemyAffinity.TBL</c>, 801 rows of 19 two-byte entries; <c>EnemyID.bin</c>, an 8-byte <c>ENEMY</c> magic,
/// a <c>u32</c> count, a <c>u32</c> 0, then <c>0x12</c>-byte rows of the <c>EnemyName</c> label's MD5 and an
/// <c>s16</c>, the row's index being the enemy id. With <c>Skill.TBL</c> too, each skill's element byte at <c>+0x0</c>
/// (docs/raw-sources/metaphor-010-templates-skill-element-2026-11-24.md). Only rows whose label isn't the empty MD5
/// are named; the others are placeholders.</summary>
public sealed class EnemyTables
{
    /// <summary>Where the game keeps the enemy table in its archive.</summary>
    public const string EnemyTablePath = "COMMON/battle/table/Enemy.TBL";

    /// <summary>Where the game keeps the enemy affinity table in its archive.</summary>
    public const string AffinityTablePath = "COMMON/battle/table/EnemyAffinity.TBL";

    /// <summary>Where the game keeps the enemy id table in its archive.</summary>
    public const string IdTablePath = "COMMON/init/data/EnemyID.bin";

    /// <summary>The size of one <c>Enemy.TBL</c> row.</summary>
    public const int EnemyRowSize = 0x64;

    /// <summary>The size of one <c>EnemyAffinity.TBL</c> row: 19 entries of two bytes.</summary>
    public const int AffinityRowSize = 0x26;

    /// <summary>The size of one <c>EnemyID.bin</c> row.</summary>
    public const int IdRowSize = 0x12;

    /// <summary>The size of <c>EnemyID.bin</c>'s header: magic, count, 0.</summary>
    public const int IdHeaderSize = 0x10;

    /// <summary>The largest table read: the game's are 80,100, 30,438 and 14,146 bytes.</summary>
    public const int MaxSize = 256 * 1024;

    private const int AffinityEntries = AffinityRowSize / 2;
    private const int HashSize = 16;
    private const int SkillRowSize = 0x22;
    private const int ElementCount = 10;

    private static readonly byte[] IdMagic = "ENEMY\0\0\0"u8.ToArray();
    private static readonly string EmptyLabel = MessageLibrary.LabelHash("");
    private static readonly string NoHash = new('0', HashSize * 2);

    private readonly EnemyRow[] rows;
    private readonly Dictionary<string, List<int>> idsByName;
    private readonly HashSet<int> named;
    private readonly byte[] skillElements;

    private EnemyTables(EnemyRow[] rows, Dictionary<string, List<int>> idsByName, byte[] skillElements)
    {
        this.rows = rows;
        this.idsByName = idsByName;
        named = [.. idsByName.Values.SelectMany(ids => ids)];
        this.skillElements = skillElements;
    }

    /// <summary>How many enemy rows were read.</summary>
    public int Rows => rows.Length;

    /// <summary>How many rows have a name.</summary>
    public int Named => named.Count;

    /// <summary>The row of enemy <paramref name="id"/>, or null past the tables.</summary>
    public EnemyRow? Row(int id) => id >= 0 && id < rows.Length ? rows[id] : null;

    /// <summary>The ids whose <c>EnemyName</c> label hashes to <paramref name="labelHash"/> (32 hex digits, as
    /// <see cref="MessageLibrary.LabelHash"/> gives), in id order; empty for none.</summary>
    public IReadOnlyList<int> IdsOf(string labelHash)
    {
        ArgumentNullException.ThrowIfNull(labelHash);
        return idsByName.TryGetValue(labelHash.ToUpperInvariant(), out var ids) ? ids : [];
    }

    /// <summary>The element of skill <paramref name="skill"/> by its <c>Skill.TBL</c> row, or null for a skill past the
    /// table, id 0, or an element that isn't one of the ten damage elements (healing, support, ailment).</summary>
    public AffinityElement? ElementOf(int skill) =>
        skill > 0 && skill < skillElements.Length && skillElements[skill] < ElementCount
            ? (AffinityElement)skillElements[skill]
            : null;

    /// <summary>Which named id has max HP <paramref name="maxHp"/>: the one among the names in
    /// <paramref name="namesSeen"/> (label hashes) when they narrow it to one, else the one with that max HP when there
    /// is only one; null when neither names one.</summary>
    public EnemyIdentity? Identify(int maxHp, IReadOnlyCollection<string> namesSeen)
    {
        ArgumentNullException.ThrowIfNull(namesSeen);
        var candidates = named.Where(id => rows[id].MaxHp == maxHp).Order().ToList();
        var byName = namesSeen.SelectMany(IdsOf).Where(candidates.Contains).Distinct().ToList();
        if (byName.Count == 1)
        {
            return new EnemyIdentity(byName[0], "name+hp", candidates.Count);
        }
        return candidates.Count == 1 ? new EnemyIdentity(candidates[0], "hp", 1) : null;
    }

    /// <summary>What was read, for the log.</summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"enemy tables: {Rows} enemies, {Named} named, {idsByName.Count} names, {skillElements.Length} skill elements");

    /// <summary>The tables, or null when one isn't whole rows, is larger than <see cref="MaxSize"/>, or the id table's
    /// header doesn't match. <paramref name="skillTable"/> may be empty, which leaves every skill without an element.
    /// </summary>
    public static EnemyTables? Parse(
        ReadOnlySpan<byte> enemyTable, ReadOnlySpan<byte> affinityTable, ReadOnlySpan<byte> idTable,
        ReadOnlySpan<byte> skillTable)
    {
        if (!Whole(enemyTable, EnemyRowSize) || !Whole(affinityTable, AffinityRowSize) ||
            skillTable.Length > MaxSize || skillTable.Length % SkillRowSize != 0 ||
            IdRows(idTable) is not { } idRows)
        {
            return null;
        }
        var count = Math.Min(enemyTable.Length / EnemyRowSize, affinityTable.Length / AffinityRowSize);
        var rows = new EnemyRow[count];
        for (var id = 0; id < count; id++)
        {
            rows[id] = ReadRow(
                id, enemyTable.Slice(id * EnemyRowSize, EnemyRowSize),
                affinityTable.Slice(id * AffinityRowSize, AffinityRowSize));
        }
        Dictionary<string, List<int>> idsByName = [];
        for (var id = 0; id < Math.Min(idRows, count); id++)
        {
            var hash = Convert.ToHexString(idTable.Slice(IdHeaderSize + id * IdRowSize, HashSize));
            if (hash == NoHash || hash == EmptyLabel)
            {
                continue;
            }
            if (!idsByName.TryGetValue(hash, out var ids))
            {
                idsByName[hash] = ids = [];
            }
            ids.Add(id);
        }
        var elements = new byte[skillTable.Length / SkillRowSize];
        for (var skill = 0; skill < elements.Length; skill++)
        {
            elements[skill] = skillTable[skill * SkillRowSize];
        }
        return new EnemyTables(rows, idsByName, elements);
    }

    private static bool Whole(ReadOnlySpan<byte> table, int rowSize) =>
        table.Length > 0 && table.Length <= MaxSize && table.Length % rowSize == 0;

    // The id table's row count, when its header and size agree.
    private static int? IdRows(ReadOnlySpan<byte> table)
    {
        if (table.Length < IdHeaderSize || table.Length > MaxSize || !table[..IdMagic.Length].SequenceEqual(IdMagic))
        {
            return null;
        }
        var count = BinaryPrimitives.ReadUInt32LittleEndian(table[IdMagic.Length..]);
        return count <= MaxSize / IdRowSize && IdHeaderSize + (long)count * IdRowSize == table.Length ? (int)count : null;
    }

    private static EnemyRow ReadRow(int id, ReadOnlySpan<byte> row, ReadOnlySpan<byte> affinity)
    {
        var entries = new AffinityEntry[AffinityEntries];
        for (var i = 0; i < AffinityEntries; i++)
        {
            entries[i] = new AffinityEntry(affinity[i * 2], (AffinityFlags)affinity[i * 2 + 1]);
        }
        return new EnemyRow(
            id, (int)BinaryPrimitives.ReadUInt32LittleEndian(row[0x4..]),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(row[0x8..]), U16(row, 0x26), U16(row, 0x28), U16(row, 0x2a),
            U16(row, 0x2c), Drop(row, 0x32), Drop(row, 0x36), entries);
    }

    private static int U16(ReadOnlySpan<byte> row, int at) => BinaryPrimitives.ReadUInt16LittleEndian(row[at..]);

    // A drop's u16, item id in the low 12 bits and kind in the high 4, then its rate; none when the u16 is 0.
    private static EnemyDrop? Drop(ReadOnlySpan<byte> row, int at) => U16(row, at) is var drop and not 0
        ? new EnemyDrop(drop >> 12, drop & 0xFFF, U16(row, at + 2))
        : null;
}

/// <summary>The enemy tables once they have loaded (#164, #166), read from the player's own archive off the game's
/// thread, so no battle line waits on them. Null until then, and when they couldn't be read.</summary>
public sealed class EnemyTableSource
{
    private readonly Func<Stream> open;
    private readonly Action<string> log;
    private volatile EnemyTables? tables;
    private int started;

    /// <summary>Reads from the archive <paramref name="open"/> opens, read-only, once <see cref="Start"/> is called,
    /// telling <paramref name="log"/> what came of it: a line naming no folder.</summary>
    public EnemyTableSource(Func<Stream> open, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(log);
        this.open = open;
        this.log = log;
    }

    /// <summary>The tables, or null while they load or when they couldn't be read.</summary>
    public EnemyTables? Tables => tables;

    /// <summary>Starts the read on the thread pool, the first time only; returns it, or a finished task after.</summary>
    public Task Start() => Interlocked.Exchange(ref started, 1) == 0 ? Task.Run(Load) : Task.CompletedTask;

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves the tables empty; it must not reach the game.")]
    private void Load()
    {
        var timer = StepTimer.Start();
        try
        {
            using var archive = open();
            var enemy = CpkArchive.Lookup(archive, EnemyTables.EnemyTablePath, EnemyTables.MaxSize);
            var affinity = CpkArchive.Lookup(archive, EnemyTables.AffinityTablePath, EnemyTables.MaxSize);
            var ids = CpkArchive.Lookup(archive, EnemyTables.IdTablePath, EnemyTables.MaxSize);
            var skills = CpkArchive.Lookup(archive, SkillCosts.SkillTablePath, EnemyTables.MaxSize);
            if (enemy.Bytes is null || affinity.Bytes is null || ids.Bytes is null)
            {
                log($"enemy tables: Enemy.TBL {enemy.Status}, EnemyAffinity.TBL {affinity.Status}, EnemyID.bin {ids.Status}");
                return;
            }
            if (EnemyTables.Parse(enemy.Bytes, affinity.Bytes, ids.Bytes, skills.Bytes ?? []) is not { } read)
            {
                log(string.Create(CultureInfo.InvariantCulture,
                    $"enemy tables: tables of {enemy.Bytes.Length}, {affinity.Bytes.Length} and {ids.Bytes.Length} bytes don't parse"));
                return;
            }
            tables = read;
            log($"{read.Describe()} {StepTimer.Took(timer)}");
        }
        catch (Exception e)
        {
            log($"enemy tables: can't read the archive: {e.GetType().Name}");
        }
    }
}
