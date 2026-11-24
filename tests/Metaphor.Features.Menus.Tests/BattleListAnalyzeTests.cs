using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's Analyze screen fetches enemies' skill names with the skill list's name flags while no list is open
/// (#920). Replays the by-ear session of 2026-11-24 (build 2fdf9f63, Steam build 18330018), its times in ms: those
/// names are none of the skill list's rows. Made-up file text and serials only.</summary>
/// <remarks>The session's message log lets a serial through once per 2 s (<c>GetMessageBySerialHook</c>'s repeat
/// gate), so it shows the first enemy's skills once; that they were fetched again when the third Analyze cursor
/// (3643321, back on a 4-skill enemy) settled is inferred from what the list said, the only order that gives its rows
/// (Charge first, Junah's first skill fifth) under this code.</remarks>
[Trait("Category", "Unit")]
public sealed class BattleListAnalyzeTests
{
    private const string SkillClass = "BTL_SkillSelect@ope@ui@app";
    private const string AnalyzeClass = "BTL_AnalyzeChoice@ope@ui@app";
    private const int SkillFirst = 700;
    private const int DescriptionFirst = 900;
    // One enemy's four skills, another's three, then the acting member's nine.
    private static readonly string[] Names =
    [
        "Charge", "Numbing Strike", "Hellbound", "Icy Impact", "Smashing Salvo", "Myriad Shots", "Kandeon",
        "Faker's Roguery", "Vidyartha", "Gift MP", "Pain-in-the-Box", "Debilitate", "Blizza", "Dekaja",
        "Infernal Vortex", "Masked Revelry",
    ];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private readonly MenuScreens screens = new(KnownScreens.All);
    private long now = 3_600_000;

    public BattleListAnalyzeTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange(MessageSerials.SkillNames, SkillFirst, Names.Length),
            new SerialRange(MessageSerials.SkillDescriptions, DescriptionFirst, Names.Length),
        ]);
        var cleaner = new TextCleaner(TextCleanerRules.None);
        messages.ObserveFiles(serials);
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        var skills = BattleListFocus.Skills(serials, announcer, cleaner, () => now);
        screens.Observe(skills);
        messages.ObserveSerials(skills);
        messages.Compiled(new MessageFileCompiled("EN/message/system/Skill_Message_1.msg", 20, Source("Skill", Names)));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Skill_Message_2.msg", 22,
            Source("SkillHelp", [.. Names.Select(name => $"{name} help")])));
    }

    private static string Source(string prefix, params string[] texts) => string.Join("\n\n",
        texts.Select((text, at) => $"//--------\n@{prefix}_{at:000}\n{{\n{text}\n<WAIT>\n}}"));

    private void Cursor(long at, string screenClass, int index, int count, long manager)
    {
        now = at;
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, Command: 0x1, Result: 1, Top: index, Row: 0,
            Flags: 0x66, screenClass, count));
    }

    // Each name with its description, as the game fetches them.
    private void Fetched(long at, int first, int count)
    {
        now = at;
        for (var row = first; row < first + count; row++)
        {
            messages.Resolved(new SerialResolved(SkillFirst + row, BattleListFocus.NameFlags));
            messages.Resolved(new SerialResolved(DescriptionFirst + row, BattleListFocus.DescriptionFlags));
        }
    }

    // The Analyze screen: the first enemy's four skills, its cursor, the second enemy's three, back to the first.
    private void Analyzed()
    {
        Fetched(3_642_072, 0, 4);
        Cursor(3_642_078, AnalyzeClass, 0, count: 4, manager: 0xab0);
        Cursor(3_642_242, AnalyzeClass, 0, count: 3, manager: 0xab0);
        Fetched(3_642_318, 4, 3);
        Fetched(3_643_316, 0, 4);
        Cursor(3_643_321, AnalyzeClass, 0, count: 4, manager: 0xab0);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheSkillListAfterTheAnalyzeScreenStartsAtTheActorsFirstSkill()
    {
        Analyzed();

        // Minutes later, the member's skill list: its nine names, then its cursor, then a move down.
        Fetched(3_858_032, 7, 9);
        Cursor(3_858_043, SkillClass, 0, count: 9, manager: 0xf60);
        Cursor(3_891_266, SkillClass, 1, count: 9, manager: 0xf60);

        Assert.Equal(["Skills, Faker's Roguery, 1 of 9", "Vidyartha, 2 of 9"], Said());
    }

    // The list visited earlier in the battle and backed out of tells nothing (battle's commands are no screen), so it is
    // still the current screen when the Analyze screen fetches.
    [Fact]
    public void TheSkillListOpenedAgainAfterTheAnalyzeScreenStartsAtItsOwnFirstSkill()
    {
        Fetched(3_500_000, 7, 9);
        Cursor(3_500_010, SkillClass, 0, count: 9, manager: 0xe00);
        Analyzed();

        Fetched(3_858_032, 7, 9);
        Cursor(3_858_043, SkillClass, 0, count: 9, manager: 0xf60);

        Assert.Equal(["Skills, Faker's Roguery, 1 of 9", "Skills, Faker's Roguery, 1 of 9"], Said());
    }
}
