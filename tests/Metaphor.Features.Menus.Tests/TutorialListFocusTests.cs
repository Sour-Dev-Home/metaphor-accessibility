using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The System &gt; Tutorials list (#806): each row is said by the title of the tutorial number its list keeps
/// for it, as its pop-up is, then its position; the screen is known, so the unknown-screen fallback stays quiet. Hook
/// events in, announcements out; made-up labels and text only.</summary>
[Trait("Category", "Unit")]
public sealed class TutorialListFocusTests
{
    private const int Settle = 0x1;
    private const string Titles =
        "@Made_Title_01\n{\nWalking About\n<WAIT>\n}\n\n@Made_Title_02\n{\nOne Thing\n<WAIT>\n}\n\n" +
        "@Made_Title_03\n{\nThird Thing\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly TutorialTable? table = TutorialTable.Parse(TutorialPopupsTests.TableBytes(
        ("Made_Title_01", ["Page"]), ("Made_Title_02", ["Page"]), ("Made_Title_03", ["Page"]),
        ("Missing_Title", ["Page"])));
    private bool on = true;

    public TutorialListFocusTests()
    {
        var library = new MessageLibrary();
        library.Observe(new MessageFileCompiled("EN/message/system/System_Tutorial_1.msg", 59, Titles));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new TutorialListFocus(
            library, announcer, new TextCleaner(TextCleanerRules.None), () => table, () => on));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int index, int? number, int count = 3) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            TutorialList.ScreenClass, Count: count, RowId: number));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachRowIsSaidByItsTutorialsTitle()
    {
        Cursor(0, number: 2);
        Cursor(1, number: 0);
        Cursor(2, number: 1);

        Assert.Equal(["Tutorials, Third Thing, 1 of 3", "Walking About, 2 of 3", "One Thing, 3 of 3"], Said());
    }

    // A tab change rewrites the list: the same row with another number is a new focus.
    [Fact]
    public void ATabChangeOnTheSameRowSaysTheNewTitle()
    {
        Cursor(0, number: 2);
        Cursor(0, number: 1, count: 5);

        Assert.Equal(["Tutorials, Third Thing, 1 of 3", "One Thing, 1 of 5"], Said());
    }

    [Fact]
    public void ARowWithNoKnownTitleIsSaidByItsPosition()
    {
        Cursor(0, number: null);
        Cursor(1, number: 3);
        Cursor(2, number: 99);

        Assert.Equal(["Tutorials, 1 of 3", "2 of 3", "3 of 3"], Said());
    }

    [Fact]
    public void OffTheListSaysPositionsOnly()
    {
        on = false;

        Cursor(0, number: 2);
        Cursor(1, number: 0);

        Assert.Equal(["Tutorials, 1 of 3", "2 of 3"], Said());
    }
}
