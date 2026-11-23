using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The quest list and an opened quest (#194): a row is named by the n-th quest name its view fetched (flags 9),
/// and an opened quest says the name (flags 10) and text (flags 7) fetched just before its cursor
/// (docs/findings/quest-message-serials-static.md). Hook events in, announcements out; made-up file text and serials
/// only.</summary>
[Trait("Category", "Unit")]
public sealed class QuestFocusTests
{
    private const string ListClass = "CampQuestChoice@ope@ui@app";
    private const string DetailClass = "CampQuestDetailChoice@ope@ui@app";
    private const string MainMenuClass = "RootMenuChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int Move = 0x14;
    private const int SystemFirst = 1300;
    private const int NameFirst = 500;
    private const int TextFirst = 600;
    private const int KindSerial = SystemFirst + 5;

    private static readonly string[] Names = ["Lost Cat", "Old Ruins", "Wolf Hunt", "Sky Fort"];
    private static readonly string[] Texts = ["Find the cat.", "Search the ruins", "Hunt the wolf."];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private bool on = true;
    private long now = 50_000;

    public QuestFocusTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", SystemFirst, 10),
            new SerialRange(MessageSerials.QuestNames, NameFirst, Names.Length),
            new SerialRange(MessageSerials.QuestTexts, TextFirst, Texts.Length),
        ]);
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        var focus = new QuestFocus(serials, announcer, new TextCleaner(TextCleanerRules.None), () => on);
        // The focus hears each cursor input before the screens turn it into a screen event, as MenusModule wires it.
        cursors.Observe(focus);
        cursors.Observe(screens);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveFiles(serials);
        messages.ObserveSerials(focus);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 10).Select(at => Entry($"System_QUEST_{at:00}", $"Label {at}")))));
        messages.Compiled(new MessageFileCompiled("EN/message/quest/Quest_1.msg", 33,
            string.Join("\n\n", Names.Select((text, at) => Entry($"QuestName_{at:000}", text)))));
        messages.Compiled(new MessageFileCompiled("EN/message/quest/Quest_2.msg", 34,
            string.Join("\n\n", Texts.Select((text, at) => Entry($"QuestInfo_{at:000}_000", text)))));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Fetched(int flags, params int[] fetched)
    {
        foreach (var serial in fetched)
        {
            messages.Resolved(new SerialResolved(serial, flags));
        }
    }

    // The list's view as the 2026-11-22 log fetched it: the first row's name, its kind label (a system message, flags
    // 9 too), then the other rows' names.
    private void ListView() =>
        Fetched(QuestFocus.RowNameFlags, NameFirst + 2, KindSerial, NameFirst + 1, NameFirst + 3, NameFirst);

    private void Cursor(string screenClass, int index, int? count, int command = Settle, long manager = 0xa0)
    {
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, command, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, count));
        now += 10;
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void AListRowIsTheNthQuestNameTheViewFetched()
    {
        ListView();
        Cursor(ListClass, 0, count: 4);

        Assert.Equal(["Wolf Hunt, 1 of 4"], Said());
    }

    [Fact]
    public void AMoveThatFetchesTheViewAgainNamesTheNewRow()
    {
        ListView();
        Cursor(ListClass, 0, count: 4);
        Cursor(ListClass, 1, count: 4, Move);
        ListView();
        Cursor(ListClass, 1, count: 4);

        Assert.Equal(["Wolf Hunt, 1 of 4", "Old Ruins, 2 of 4"], Said());
    }

    [Fact]
    public void OffTheListSaysPositionsOnly()
    {
        on = false;
        ListView();
        Cursor(ListClass, 0, count: 4);

        Assert.Equal(["1 of 4"], Said());
    }

    [Fact]
    public void AnOpenedQuestSaysItsNameAndDescriptionThenStaysSilent()
    {
        ListView();
        Cursor(ListClass, 0, count: 4);
        Fetched(QuestFocus.TitleFlags, NameFirst + 2);
        Fetched(QuestFocus.DescriptionFlags, SystemFirst + 1, TextFirst + 1);
        Cursor(DetailClass, 0, count: 9, manager: 0xb0);
        Cursor(DetailClass, 1, count: 9, manager: 0xb0);

        Assert.Equal(["Wolf Hunt, 1 of 4", "Wolf Hunt. Search the ruins."], Said());
    }

    [Fact]
    public void OffAnOpenedQuestIsSilent()
    {
        on = false;
        Fetched(QuestFocus.TitleFlags, NameFirst);
        Fetched(QuestFocus.DescriptionFlags, TextFirst);
        Cursor(DetailClass, 0, count: 9, manager: 0xb0);

        Assert.Empty(Said());
    }

    [Fact]
    public void ANameFetchedBeforeAnotherScreensCursorIsNotAnOpenedQuests()
    {
        Fetched(QuestFocus.TitleFlags, NameFirst);
        Fetched(QuestFocus.DescriptionFlags, TextFirst);
        Cursor(MainMenuClass, 5, count: 9);
        var before = announcer.Said.Count;
        Cursor(DetailClass, 0, count: 9, manager: 0xb0);

        Assert.Equal(before, announcer.Said.Count);
    }
}
