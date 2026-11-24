using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Battle.Tests;

/// <summary>The move banner's skill said by name (#954): a kind 0 banner's id is a <c>Skill.TBL</c> row, whose name
/// label's hash finds the name in the skill names file. Made-up labels, names and ids only.</summary>
[Trait("Category", "Unit")]
public sealed class MoveNamesTests
{
    private const int First = 700;
    private static readonly string[] Labels = ["SkillName_Spark", "SkillName_Frost", "SkillName_Gust"];
    private static readonly string[] Names = ["Spark", "Frost", "Gust"];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageSerials serials =
        new([new SerialRange(MessageSerials.SkillNames, First, Labels.Length)]);
    private readonly MoveNames moves;

    public MoveNamesTests()
    {
        serials.Observe(new MessageFileCompiled("EN/message/system/Skill_Message_1.msg", 1, string.Join("\n\n",
            Labels.Select((label, at) => $"//--------\n@{label}\n{{\n{Names[at]}\n<WAIT>\n}}"))));
        // Skill 12 is Frost, 30 is Gust; 40 has a label the file doesn't hold.
        var hashes = new Dictionary<int, string>
        {
            [12] = MessageLibrary.LabelHash("SkillName_Frost"),
            [30] = MessageLibrary.LabelHash("SkillName_Gust").ToLowerInvariant(),
            [40] = MessageLibrary.LabelHash("SkillName_Unknown"),
        };
        moves = new MoveNames(id => hashes.TryGetValue(id, out var hash) ? hash : null, serials,
            new TextCleaner(TextCleanerRules.None), announcer);
    }

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void AMoveBannerSaysItsSkillsNameQueued()
    {
        moves.Moved(12);
        moves.Moved(30);

        Assert.Equal(["Frost", "Gust"], Said());
        Assert.All(announcer.Said, said => Assert.False(said.Interrupt));
    }

    [Fact]
    public void ABasicAttackOrASkillWithNoKnownNameSaysNothing()
    {
        moves.Moved(0);
        moves.Moved(40);
        moves.Moved(99);

        Assert.Empty(Said());
    }
}
