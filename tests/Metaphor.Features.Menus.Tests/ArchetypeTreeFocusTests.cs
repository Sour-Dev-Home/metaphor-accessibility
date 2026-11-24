using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Archetype Tree (#220): the character pick says "Archetype Tree" and each row's character, and the tree,
/// which has no cursor, says each archetype it fetches with flags 10 as "&lt;archetype&gt;, &lt;line&gt;, rank n"
/// (docs/findings/class-name-and-battle-item-tab-serials.md). Hook events in, announcements out; made-up file text
/// only.</summary>
[Trait("Category", "Unit")]
public sealed class ArchetypeTreeFocusTests
{
    private const string PickClass = "AcademiaClassGetCharaSel@ope@ui@app";
    private const string DetailsClass = "AcademiaClassDetailChoice@ope@ui@app";
    private const string RootClass = "RootMenuChoice@ope@ui@app";
    private const int FirstLine = 9441;
    private const int FirstClass = 1082;
    private const int Strohl = 1;
    private const int Hulkenberg = 2;
    private const int Hacker = FirstClass + 1;
    private const int Unused = FirstClass + 2;
    private const int Brute = FirstClass + 3;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly MessageEvents messages = new();
    private bool on = true;

    public ArchetypeTreeFocusTests()
    {
        var serials = new MessageSerials([
            new SerialRange(MessageSerials.CharacterNames, 0, 3),
            new SerialRange(MessageSerials.ArchetypeNames, FirstLine, 3),
            new SerialRange(MessageSerials.ClassNames, FirstClass, 4),
        ]);
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/CharaName.msg", 1, string.Join("\n\n",
            Entry("CharaName_PC01_Hero", "Hero"), Entry("CharaName_PC02_Stroll", "Strohl"),
            Entry("CharaName_PC03_Hulkenberg", "Hulkenberg"))));
        messages.Compiled(new MessageFileCompiled("EN/message/ATCategoryName.msg", 30, string.Join("\n\n",
            Entry("ATCategoryName_01", "Seeker"), Entry("ATCategoryName_02", "Mage"), Entry("ATCategoryName_03", "Warrior"))));
        messages.Compiled(new MessageFileCompiled("EN/message/ClassName.msg", 5, string.Join("\n\n",
            Entry("ClassName_01_01", "Seeker"), Entry("ClassName_01_03", "Hacker"),
            Entry("ClassName_01_04", "@ClassName_01_04"), Entry("ClassName_03_02", "Brute"))));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new ArchetypeTreeFocus(serials, announcer, new TextCleaner(TextCleanerRules.None), () => on)
        {
            ProtagonistName = () => "Will",
        };
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        messages.ObserveSerials(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Cursor(string screenClass, int index, int count = 7) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    // The pick's opening, as on 2026-11-23: the protagonist's archetype, then the members' names in row order.
    private void OpenPick()
    {
        messages.Resolved(new SerialResolved(Hacker, 9));
        messages.Resolved(new SerialResolved(Strohl, 9));
        messages.Resolved(new SerialResolved(Hulkenberg, 9));
        Cursor(PickClass, 0);
    }

    private void Node(int serial) => messages.Resolved(new SerialResolved(serial, ArchetypeTreeFocus.NodeFlags));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // #220: the pick said positions only, as an unknown screen.
    [Fact]
    public void ThePickSaysItsNameThenEachCharacter()
    {
        OpenPick();
        Cursor(PickClass, 2);

        Assert.Equal(["Archetype Tree, Will, 1 of 7", "Hulkenberg, 3 of 7"], Said());
    }

    // #220: the tree said nothing.
    [Fact]
    public void EachArchetypeTheTreeFocusesIsSaidWithItsLineAndRank()
    {
        OpenPick();
        Node(Hacker);
        Node(Brute);

        Assert.Equal(["Hacker, Seeker, rank 3", "Brute, Warrior, rank 2"], Said().Skip(1));
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheTreeSpeaksAgainAfterDetailsCloses()
    {
        OpenPick();
        Cursor(DetailsClass, 0, count: 6);
        Node(Brute);

        Assert.Equal("Brute, Warrior, rank 2", Said().Last());
    }

    [Fact]
    public void ANodeFetchOutsideTheTreesVisitIsNotSaid()
    {
        OpenPick();
        Cursor(RootClass, 0, count: 9);
        var before = announcer.Said.Count;
        Node(Brute);

        Assert.Equal(before, announcer.Said.Count);
    }

    [Fact]
    public void AnUnusedSlotIsNotSaid()
    {
        OpenPick();
        Node(Unused);

        Assert.Single(announcer.Said);
    }

    [Fact]
    public void OffThePickSaysPositionsAndTheTreeNothing()
    {
        on = false;
        OpenPick();
        Cursor(PickClass, 1);
        Node(Brute);

        Assert.Equal(["Archetype Tree, 1 of 7", "2 of 7"], Said());
    }

    [Fact]
    public void ThePickIsAKnownScreen() => Assert.Contains(ArchetypeTreeFocus.CharacterPick, KnownScreens.All);
}
