using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Logging;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Battle;

/// <summary>What a skill costs to use, as its <c>SkillNormal.TBL</c> record holds it (#692): the cost type
/// (<see cref="Hp"/>, <see cref="Mp"/> or <see cref="Money"/>), a percent and a flat amount, and the turn icons.</summary>
/// <param name="Id">The skill id: the record's index in both tables.</param>
/// <param name="Type">The cost type, <c>+0x5</c>.</param>
/// <param name="Percent">The percent cost, <c>+0x6</c>.</param>
/// <param name="Value">The flat cost, <c>+0x8</c>.</param>
/// <param name="TurnIcons">The turn icon cost, <c>+0xA</c>.</param>
public sealed record SkillCost(int Id, int Type, int Percent, int Value, int TurnIcons)
{
    /// <summary>The cost type of an HP cost.</summary>
    public const int Hp = 1;

    /// <summary>The cost type of an MP cost.</summary>
    public const int Mp = 2;

    /// <summary>The cost type of a money cost.</summary>
    public const int Money = 3;

    /// <summary>Whether the record asks for anything: an HP, MP or money cost of more than 0.</summary>
    public bool Costs => Type is Hp or Mp or Money && (Percent > 0 || Value > 0);
}

/// <summary>Each skill's cost by its name label's hash (#692 (b)), from the battle tables in the game's archive, read
/// offline on game build 548ddc95… (Steam build 18330018) (docs/findings/skill-cost-from-skill-tables.md).
/// <c>Skill.TBL</c> holds <c>0x22</c>-byte records, one per skill id, whose bytes at <c>+0x2</c> are the MD5 of the
/// skill's name label; <c>SkillNormal.TBL</c> holds <c>0x30</c>-byte records at the same ids, with the cost type at
/// <c>+0x5</c>, the percent (<c>u16</c>) at <c>+0x6</c>, the flat amount at <c>+0x8</c> and the turn icons at
/// <c>+0xA</c> (docs/raw-sources/metaphor-010-templates-tables-2026-11-17.md). Neither has a header. Where several
/// records share a name, the one with a cost wins, and when more than one has a cost and they differ the name has none,
/// rather than a guess. The table's cost is the base cost: what a passive or a halving effect makes of it is
/// <c>[NEEDS VERIFICATION]</c>.</summary>
public sealed class SkillCosts
{
    /// <summary>Where the game keeps the skill table in its archive.</summary>
    public const string SkillTablePath = "COMMON/battle/table/Skill.TBL";

    /// <summary>Where the game keeps the skill cost table in its archive.</summary>
    public const string CostTablePath = "COMMON/battle/table/SkillNormal.TBL";

    /// <summary>The size of one <c>Skill.TBL</c> record.</summary>
    public const int SkillRecordSize = 0x22;

    /// <summary>The size of one <c>SkillNormal.TBL</c> record.</summary>
    public const int CostRecordSize = 0x30;

    /// <summary>The largest table read: the game's are 51,000 and 48,000 bytes.</summary>
    public const int MaxSize = 256 * 1024;

    private const int NameHashAt = 0x2;
    private const int HashSize = 16;
    private const int TypeAt = 0x5;
    private const int PercentAt = 0x6;
    private const int ValueAt = 0x8;
    private const int TurnIconsAt = 0xA;

    private static readonly string EmptyLabel = MessageLibrary.LabelHash("");
    private static readonly string NoHash = new('0', HashSize * 2);

    private readonly Dictionary<string, SkillCost?> byName;
    private readonly string?[] nameById;

    private SkillCosts(Dictionary<string, SkillCost?> byName, string?[] nameById, int costs)
    {
        this.byName = byName;
        this.nameById = nameById;
        Skills = nameById.Length;
        CostRecords = costs;
    }

    /// <summary>The name label hash of skill <paramref name="id"/> (32 hex digits), the one a move banner shows
    /// (#954, docs/findings/battle-affinity-discovery-from-move-banner.md), or null for an id with no name.</summary>
    public string? LabelHashOf(int id) => id >= 0 && id < nameById.Length ? nameById[id] : null;

    /// <summary>How many records the skill table held.</summary>
    public int Skills { get; }

    /// <summary>How many records the cost table held.</summary>
    public int CostRecords { get; }

    /// <summary>The cost of the skill whose name label hashes to <paramref name="labelHash"/> (32 hex digits, as
    /// <see cref="MessageLibrary.LabelHash"/> gives), or null when it has none or the name is ambiguous.</summary>
    public SkillCost? CostOf(string labelHash)
    {
        ArgumentNullException.ThrowIfNull(labelHash);
        return byName.TryGetValue(labelHash.ToUpperInvariant(), out var cost) ? cost : null;
    }

    /// <summary>What was read, for the log.</summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"skill costs: {Skills} skills, {CostRecords} cost records, {byName.Values.Count(cost => cost is not null)} names with a cost");

    /// <summary>The costs from the two tables, or null when either isn't a whole number of records or is larger than
    /// <see cref="MaxSize"/>.</summary>
    public static SkillCosts? Parse(ReadOnlySpan<byte> skillTable, ReadOnlySpan<byte> costTable)
    {
        if (!Whole(skillTable, SkillRecordSize) || !Whole(costTable, CostRecordSize))
        {
            return null;
        }
        var skills = skillTable.Length / SkillRecordSize;
        var costs = costTable.Length / CostRecordSize;
        Dictionary<string, SkillCost?> byName = [];
        var nameById = new string?[skills];
        // Names whose records with a cost disagree: no cost is said for them.
        HashSet<string> ambiguous = [];
        for (var id = 0; id < skills; id++)
        {
            var hash = Convert.ToHexString(skillTable.Slice(id * SkillRecordSize + NameHashAt, HashSize));
            if (hash == NoHash || hash == EmptyLabel)
            {
                continue;
            }
            nameById[id] = hash;
            var cost = id < costs ? Read(id, costTable.Slice(id * CostRecordSize, CostRecordSize)) : null;
            if (cost is not { Costs: true })
            {
                byName.TryAdd(hash, null);
                continue;
            }
            if (byName.TryGetValue(hash, out var held) && held is not null && !SameCost(held, cost))
            {
                ambiguous.Add(hash);
            }
            else if (held is null)
            {
                byName[hash] = cost;
            }
        }
        foreach (var hash in ambiguous)
        {
            byName[hash] = null;
        }
        return new SkillCosts(byName, nameById, costs);
    }

    private static bool Whole(ReadOnlySpan<byte> table, int recordSize) =>
        table.Length > 0 && table.Length <= MaxSize && table.Length % recordSize == 0;

    private static SkillCost Read(int id, ReadOnlySpan<byte> record) => new(
        id, record[TypeAt], BinaryPrimitives.ReadUInt16LittleEndian(record[PercentAt..]),
        BinaryPrimitives.ReadUInt16LittleEndian(record[ValueAt..]),
        BinaryPrimitives.ReadUInt16LittleEndian(record[TurnIconsAt..]));

    private static bool SameCost(SkillCost one, SkillCost other) =>
        one.Type == other.Type && one.Percent == other.Percent && one.Value == other.Value &&
        one.TurnIcons == other.TurnIcons;
}

/// <summary>The skill costs once they have loaded (#692 (b)), read from the player's own archive off the game's thread,
/// so a skill list row never waits on it. Empty until then, and when they couldn't be read.</summary>
public sealed class SkillCostSource
{
    private readonly Func<Stream> open;
    private readonly Action<string> log;
    private volatile SkillCosts? costs;
    private int started;

    /// <summary>Reads from the archive <paramref name="open"/> opens, read-only, once <see cref="Start"/> is called,
    /// telling <paramref name="log"/> what came of it: a line naming no folder.</summary>
    public SkillCostSource(Func<Stream> open, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(log);
        this.open = open;
        this.log = log;
    }

    /// <summary>The cost of the skill whose name label hashes to <paramref name="labelHash"/>, or null while the
    /// tables load, when they couldn't be read, or when it has none.</summary>
    public SkillCost? CostOf(string labelHash) => costs?.CostOf(labelHash);

    /// <summary>Skill <paramref name="id"/>'s name label hash, or null while not loaded
    /// (<see cref="SkillCosts.LabelHashOf"/>).</summary>
    public string? LabelHashOf(int id) => costs?.LabelHashOf(id);

    /// <summary>Whether the tables have loaded.</summary>
    public bool Loaded => costs is not null;

    /// <summary>Starts the read on the thread pool, the first time only; returns it, or a finished task after.</summary>
    public Task Start() => Interlocked.Exchange(ref started, 1) == 0 ? Task.Run(Load) : Task.CompletedTask;

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves the costs empty; it must not reach the game.")]
    private void Load()
    {
        var timer = StepTimer.Start();
        try
        {
            using var archive = open();
            var skills = CpkArchive.Lookup(archive, SkillCosts.SkillTablePath, SkillCosts.MaxSize);
            var table = CpkArchive.Lookup(archive, SkillCosts.CostTablePath, SkillCosts.MaxSize);
            if (skills.Bytes is null || table.Bytes is null)
            {
                log($"skill costs: Skill.TBL {skills.Status}, SkillNormal.TBL {table.Status}");
                return;
            }
            if (SkillCosts.Parse(skills.Bytes, table.Bytes) is not { } read)
            {
                log(string.Create(CultureInfo.InvariantCulture,
                    $"skill costs: tables of {skills.Bytes.Length} and {table.Bytes.Length} bytes aren't whole records"));
                return;
            }
            costs = read;
            log($"{read.Describe()} {StepTimer.Took(timer)}");
        }
        catch (Exception e)
        {
            log($"skill costs: can't read the archive: {e.GetType().Name}");
        }
    }
}
