using Metaphor.Core.Battle;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>A battle list row's cost (#692 (b), #260): the row's name serial's label, hashed, finds the skill's cost in
/// the tables. Made-up labels and costs only.</summary>
[Trait("Category", "Unit")]
public sealed class SkillCostLinesTests
{
    private readonly Dictionary<int, string> labels = new()
    {
        [10] = "SkillName_Spark_01",
        [11] = "SkillName_Cleave_01",
        [12] = "SkillName_Pierce_01",
        [13] = "SkillName_Free_01",
    };

    private readonly Dictionary<string, SkillCost> costs = new()
    {
        [MessageLibrary.LabelHash("SkillName_Spark_01")] = new SkillCost(1, SkillCost.Mp, 0, 4, 1),
        [MessageLibrary.LabelHash("SkillName_Cleave_01")] = new SkillCost(2, SkillCost.Hp, 15, 0, 2),
        [MessageLibrary.LabelHash("SkillName_Pierce_01")] = new SkillCost(3, SkillCost.Hp, 0, 30, 0),
        [MessageLibrary.LabelHash("SkillName_Free_01")] = new SkillCost(4, 0, 0, 0, 0),
    };

    private bool on = true;

    private SkillCostLines Lines(Action<string>? log = null) => new(
        serial => labels.GetValueOrDefault(serial), hash => costs.GetValueOrDefault(hash), () => on)
    {
        Log = log,
    };

    [Fact]
    public void AnMpSkillSaysItsMpAndAnHpSkillItsPercentOrAmount()
    {
        var lines = Lines();

        Assert.Equal("4 MP", lines.CostOf(10, turnIcons: false));
        Assert.Equal("15 percent HP", lines.CostOf(11, turnIcons: false));
        Assert.Equal("30 HP", lines.CostOf(12, turnIcons: false));
    }

    [Fact]
    public void TheSynthesisListSaysTheTurnIconsFirst()
    {
        var lines = Lines();

        Assert.Equal("1 turn icon, 4 MP", lines.CostOf(10, turnIcons: true));
        Assert.Equal("2 turn icons, 15 percent HP", lines.CostOf(11, turnIcons: true));
    }

    [Fact]
    public void AFreeSkillAnUnknownRowOrTheSettingOffSaysNothing()
    {
        var lines = Lines();

        Assert.Null(lines.CostOf(13, turnIcons: false));
        Assert.Null(lines.CostOf(99, turnIcons: false));
        on = false;
        Assert.Null(lines.CostOf(10, turnIcons: false));
    }

    [Fact]
    public void ADebugLogGetsEachRowsLookupOnce()
    {
        List<string> logged = [];
        var lines = Lines(logged.Add);
        labels[14] = "SkillName_Missing_01";

        lines.CostOf(10, turnIcons: false);
        lines.CostOf(10, turnIcons: true);
        lines.CostOf(14, turnIcons: false);

        Assert.Equal(
            ["serial=10 label=SkillName_Spark_01 id=1 type=2 percent=0 value=4 icons=1",
             "serial=14 label=SkillName_Missing_01 cost=none"],
            logged);
    }
}
