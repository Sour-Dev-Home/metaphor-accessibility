using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The quick menu's Return to title dialog says its question before the focused choice (#810): heard on
/// 2026-11-23 as only "No, 2 of 2", opened right after the focus on the menu's last row. Cursor inputs in,
/// announcements out; made-up message text only.</summary>
[Trait("Category", "Unit")]
public sealed class ReturnToTitleQuestionTests
{
    private const string SystemSource =
        "@System_Choice_YESNO SEL 0\n{\nAye\nNay\n<WAIT>\n}\n\n" +
        "@System_Return_01\n{\nGo back?\nAll is lost.\n<WAIT>\n}";
    private const string YesNoClass = "YesNoChoiceSample@ope@ui@app";
    private const string QuickMenuClass = "InstantMenuChoice@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly MenuScreens screens = new([YesNoDialogFocus.Screen, QuickMenuFocus.Screen]);
    private long manager = 0x100;
    private bool on = true;

    public ReturnToTitleQuestionTests()
    {
        var library = new MessageLibrary();
        var messages = new MessageEvents();
        messages.ObserveFiles(library);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 6, SystemSource));
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        var question = new ReturnToTitleQuestion(() => on);
        screens.Observe(new YesNoDialogFocus(
            library, announcer, new TextCleaner(TextCleanerRules.None), new YesNoQuestions([question])));
        screens.Observe(question);
    }

    private void QuickMenu(int row, int count = 8) =>
        cursors.Handled(new CursorInput(0x90, 0x100, Settle, Result: 1, Top: row, Row: 0, 0x46, QuickMenuClass, count));

    private void DialogOpens(int row = 1)
    {
        manager++;
        cursors.Handled(new CursorInput(manager, 0x100, Settle, Result: 1, Top: row, Row: 0, 0x46, YesNoClass, 2));
    }

    private string LastSaid() => announcer.Said[^1].Text;

    [Fact]
    public void TheLastRowsDialogSaysItsQuestionFirst()
    {
        QuickMenu(6);
        QuickMenu(7);
        DialogOpens();

        Assert.Equal("Go back? All is lost. Nay, 2 of 2", LastSaid());
    }

    [Fact]
    public void TheLastRowOfTheNineRowMenuToo()
    {
        QuickMenu(8, count: 9);
        DialogOpens(row: 0);

        Assert.Equal("Go back? All is lost. Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void ADialogFromAnotherRowSaysNoQuestion()
    {
        QuickMenu(7);
        QuickMenu(2);
        DialogOpens();

        Assert.Equal("Nay, 2 of 2", LastSaid());
    }

    [Fact]
    public void TheQuestionIsSaidOncePerOpening()
    {
        QuickMenu(7);
        DialogOpens();
        DialogOpens();

        Assert.Equal("Nay, 2 of 2", LastSaid());
    }

    [Fact]
    public void OffItSaysNoQuestion()
    {
        on = false;
        QuickMenu(7);
        DialogOpens();

        Assert.Equal("Nay, 2 of 2", LastSaid());
    }
}
