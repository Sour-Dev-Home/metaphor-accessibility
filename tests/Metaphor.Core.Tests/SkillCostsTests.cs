using System.Buffers.Binary;
using Metaphor.Core.Battle;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests;

/// <summary>The skill tables (#692 (b)) give a skill's cost by its name label's hash, in the layout of
/// docs/findings/skill-cost-from-skill-tables.md. Made-up labels and costs only.</summary>
[Trait("Category", "Unit")]
public sealed class SkillCostsTests
{
    /// <summary>A skill table: each id's name label, null for a record with no name (zeros).</summary>
    public static byte[] Skills(params string?[] labels)
    {
        var bytes = new byte[labels.Length * SkillCosts.SkillRecordSize];
        for (var id = 0; id < labels.Length; id++)
        {
            if (labels[id] is { } label)
            {
                Convert.FromHexString(MessageLibrary.LabelHash(label)).CopyTo(bytes, id * SkillCosts.SkillRecordSize + 2);
            }
        }
        return bytes;
    }

    /// <summary>A cost table: each id's cost type, percent, flat amount and turn icons.</summary>
    public static byte[] Costs(params (int Type, int Percent, int Value, int Icons)[] records)
    {
        var bytes = new byte[records.Length * SkillCosts.CostRecordSize];
        for (var id = 0; id < records.Length; id++)
        {
            var record = bytes.AsSpan(id * SkillCosts.CostRecordSize, SkillCosts.CostRecordSize);
            record[5] = (byte)records[id].Type;
            BinaryPrimitives.WriteUInt16LittleEndian(record[6..], (ushort)records[id].Percent);
            BinaryPrimitives.WriteUInt16LittleEndian(record[8..], (ushort)records[id].Value);
            BinaryPrimitives.WriteUInt16LittleEndian(record[0xA..], (ushort)records[id].Icons);
        }
        return bytes;
    }

    private static string Hash(string label) => MessageLibrary.LabelHash(label);

    [Fact]
    public void AnMpSkillAndAnHpSkillHaveTheirTablesCosts()
    {
        var costs = SkillCosts.Parse(
            Skills(null, "SkillName_Spark_01", "SkillName_Cleave_01"),
            Costs((0, 0, 0, 0), (SkillCost.Mp, 0, 4, 1), (SkillCost.Hp, 15, 0, 2)));

        Assert.NotNull(costs);
        Assert.Equal(new SkillCost(1, SkillCost.Mp, 0, 4, 1), costs.CostOf(Hash("SkillName_Spark_01")));
        Assert.Equal(new SkillCost(2, SkillCost.Hp, 15, 0, 2), costs.CostOf(Hash("SkillName_Cleave_01").ToLowerInvariant()));
        Assert.Null(costs.CostOf(Hash("SkillName_Unknown")));
    }

    [Fact]
    public void ASharedNameTakesTheRecordWithACost()
    {
        // An enemy's copy of the skill, free, before and after the player's.
        var costs = SkillCosts.Parse(
            Skills("SkillName_Gust_01", "SkillName_Gust_01", "SkillName_Gust_01"),
            Costs((0, 0, 0, 0), (SkillCost.Mp, 0, 6, 0), (SkillCost.Mp, 0, 0, 0)));

        Assert.Equal(6, costs!.CostOf(Hash("SkillName_Gust_01"))!.Value);
    }

    [Fact]
    public void ASharedNameWithTwoDifferentCostsHasNone()
    {
        var costs = SkillCosts.Parse(
            Skills("SkillName_Gust_01", "SkillName_Gust_01"),
            Costs((SkillCost.Mp, 0, 6, 0), (SkillCost.Mp, 0, 8, 0)));

        Assert.Null(costs!.CostOf(Hash("SkillName_Gust_01")));
    }

    [Fact]
    public void ASkillPastTheCostTableHasNone()
    {
        var costs = SkillCosts.Parse(Skills("SkillName_Spark_01", "SkillName_Passive_01"), Costs((SkillCost.Mp, 0, 4, 0)));

        Assert.Null(costs!.CostOf(Hash("SkillName_Passive_01")));
        Assert.Equal(2, costs.Skills);
        Assert.Equal(1, costs.CostRecords);
    }

    // #954: a move banner names its skill by id, and the record's name hash finds the name.
    [Fact]
    public void EachSkillIdGivesItsNameLabelsHash()
    {
        var costs = SkillCosts.Parse(Skills("SkillName_Spark_01", "SkillName_Passive_01"), Costs((SkillCost.Mp, 0, 4, 0)));

        Assert.Equal(Hash("SkillName_Passive_01"), costs!.LabelHashOf(1), ignoreCase: true);
        Assert.Null(costs.LabelHashOf(2));
        Assert.Null(costs.LabelHashOf(-1));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void ADamagedTableGivesNoCosts(int skillExtra, int costExtra)
    {
        var skills = Skills("SkillName_Spark_01");
        var table = Costs((SkillCost.Mp, 0, 4, 0));

        Assert.Null(SkillCosts.Parse(
            skills.AsSpan(0, skills.Length).ToArray().Concat(new byte[skillExtra]).ToArray(),
            costExtra < 0 ? [] : table.Concat(new byte[costExtra]).ToArray()));
    }

    [Fact]
    public async Task TheSourceReadsBothTablesFromTheArchiveWhenStarted()
    {
        List<string> lines = [];
        var skills = Skills(null, "SkillName_Spark_01");
        var table = Costs((0, 0, 0, 0), (SkillCost.Mp, 0, 4, 1));
        var source = new SkillCostSource(
            () => CpkArchiveTests.Archive(
                ("COMMON/battle/table", "Skill.TBL", skills, skills.Length),
                ("COMMON/battle/table", "SkillNormal.TBL", table, table.Length)),
            lines.Add);

        Assert.Null(source.CostOf(Hash("SkillName_Spark_01")));
        await source.Start();

        Assert.True(source.Loaded);
        Assert.Equal(4, source.CostOf(Hash("SkillName_Spark_01"))!.Value);
        Assert.StartsWith("skill costs: 2 skills, 2 cost records, 1 names with a cost", Assert.Single(lines),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSourceSaysWhenATableIsMissing()
    {
        List<string> lines = [];
        var skills = Skills("SkillName_Spark_01");
        var source = new SkillCostSource(
            () => CpkArchiveTests.Archive(("COMMON/battle/table", "Skill.TBL", skills, skills.Length)), lines.Add);

        await source.Start();

        Assert.False(source.Loaded);
        Assert.Equal("skill costs: Skill.TBL Read, SkillNormal.TBL NotFound", Assert.Single(lines));
        Assert.True(source.Start().IsCompleted);
    }
}
