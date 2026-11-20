using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A yes/no dialog, such as the name-entry confirmation (#549), is recognised by its screen class and each
/// choice the cursor settles on spoken as "text, n of 2", each cutting off the last. The text is the line of the yes/no
/// choice message at the cursor's index (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md). Hook events in (cursor
/// inputs carrying a screen class, message files compiled), announcements out; made-up choice text only.</summary>
[Trait("Category", "Unit")]
public sealed class YesNoDialogFocusTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const string SystemSource =
        "@System_Choice_OK\n{\nFine\n<WAIT>\n}\n\n@System_Choice_YESNO SEL 0\n{\nAye<COLOR -1>\nNay\n<WAIT>\n}";
    private const string YesNoClass = "YesNoChoiceSample@ope@ui@app";
    private const long Dialog = 0xa0;
    private const int Settle = 0x1;
    private const int Up = 0x15;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public YesNoDialogFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens([YesNoDialogFocus.Screen]);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new YesNoDialogFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int command, int row, int top = 0, string? screenClass = YesNoClass, long manager = Dialog) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, command, Result: 1, top, row, Flags: 0x46, screenClass, Count: 2));

    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemFile, 6, SystemSource));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // The name-entry confirmation opened on index 1, then the player moved up to index 0 (log of 2026-11-20).
    [Fact]
    public void EachChoiceIsSpokenAsItsTextThenItsPosition()
    {
        SystemFileCompiled();

        Cursor(Settle, row: 0, top: 1);
        Cursor(Up, row: 0);
        Cursor(Settle, row: 0);

        Assert.Equal(
            [new Announcement("Nay, 2 of 2", Interrupt: true), new Announcement("Aye, 1 of 2", Interrupt: true)],
            announcer.Said);
    }

    [Fact]
    public void AChoiceWhoseTextIsMissingIsSpokenByItsPosition()
    {
        Cursor(Settle, row: 1);

        Assert.Equal(["2 of 2"], Said());
    }

    [Fact]
    public void APositionPastTheTwoChoicesIsNotAnnounced()
    {
        SystemFileCompiled();

        Cursor(Settle, row: 2);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnotherScreenIsNotSpokenAsAYesNoDialog()
    {
        SystemFileCompiled();

        Cursor(Settle, row: 1, screenClass: "SingleChoiceBase@ope@ui@app", manager: 0xb0);

        Assert.Equal(["2 of 2"], Said());
    }
}
