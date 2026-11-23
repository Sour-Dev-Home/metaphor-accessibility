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
        "@System_Choice_OK\n{\nFine\n<WAIT>\n}\n\n@System_Choice_YESNO SEL 0\n{\nAye<COLOR -1>\nNay\n<WAIT>\n}\n\n" +
        "@System_NameEntry_08\n{\nIs the name <V 0>?\n<COLOR 0> *Settled *later.\n<WAIT>\n}\n\n" +
        "@System_mode_13\n{\nBegin on this one?\n<WAIT>\n}";
    private const string YesNo = "System_Choice_YESNO";
    private const string YesNoClass = "YesNoChoiceSample@ope@ui@app";
    private const long Dialog = 0xa0;
    private const int Settle = 0x1;
    private const int Up = 0x15;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();
    private readonly MenuScreens screens = new([YesNoDialogFocus.Screen]);

    public YesNoDialogFocusTests()
    {
        var library = new MessageLibrary();
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        var focus = new YesNoDialogFocus(library, announcer, new TextCleaner(TextCleanerRules.None));
        screens.Observe(focus);
        messages.ObserveChoices(focus);
        messages.ObserveSelects(focus);
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

    // #540's run: once the title's handle cleared, the menus read closed, and the yes/no after the difficulty list was
    // silent; a second dialog opening on the same item is a new manager, so it is heard too.
    [Fact]
    public void ADialogIsSpokenWhileTheMenusAreClosedEachTimeItOpens()
    {
        SystemFileCompiled();
        screens.MenusRead(open: false);

        Cursor(Settle, row: 0, top: 1);
        Cursor(Settle, row: 0, top: 1, manager: 0xb0);

        Assert.Equal(["Nay, 2 of 2", "Nay, 2 of 2"], Said());
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

    private void SelectOpened(string prompt, string choice = YesNo) =>
        messages.SelectOpened(new SelectRequested(1, prompt, choice));

    // #569: the name entry opens its confirmation itself, outside the scripts' choices, so its text was never heard.
    // It is said once, with the first focus in one announcement so neither cuts the other off; its asterisk line is a
    // note (the maintainer's wording, 2026-11-20). The typed name's tag is dropped until where it is held is known.
    [Fact]
    public void ADialogOpenedOutsideTheScriptsSpeaksItsTextWithTheFirstFocusOnly()
    {
        SystemFileCompiled();

        SelectOpened("System_NameEntry_08");
        Cursor(Settle, row: 0, top: 1);
        Cursor(Up, row: 0);
        Cursor(Settle, row: 0);

        Assert.Equal(
            [
                new Announcement("Is the name? Note: Settled *later. Nay, 2 of 2", Interrupt: true),
                new Announcement("Aye, 1 of 2", Interrupt: true),
            ],
            announcer.Said);
    }

    // #569's second case: the difficulty list's confirmation.
    [Fact]
    public void AQuestionIsJoinedToTheFocusWithoutAComma()
    {
        SystemFileCompiled();

        SelectOpened("System_mode_13");
        Cursor(Settle, row: 0, top: 1);

        Assert.Equal(["Begin on this one? Nay, 2 of 2"], Said());
    }

    [Fact]
    public void TheTextIsSpokenOncePerOpening()
    {
        SystemFileCompiled();

        SelectOpened("System_mode_13");
        Cursor(Settle, row: 0, top: 1);
        Cursor(Settle, row: 0, top: 1, manager: 0xb0);

        Assert.Equal(["Begin on this one? Nay, 2 of 2", "Nay, 2 of 2"], Said());
    }

    // The scripts' choices already speak their prompt (DialogueChoices, #82); the window they open is the same call.
    [Fact]
    public void AScriptsChoiceIsNotGivenItsPromptAgain()
    {
        SystemFileCompiled();

        messages.ChoiceOpened(new ChoiceRequested(1, "System_mode_13", YesNo));
        SelectOpened("System_mode_13");
        Cursor(Settle, row: 0, top: 1);

        Assert.Equal(["Nay, 2 of 2"], Said());
    }

    // The save prompt by ear (maintainer, 2026-11-21): the scripts' choice spoke "<prompt>" and the choice count, then
    // the dialog's first focus cut both off. After a scripts' choice the first focus waits its turn; moves still cut off.
    [Fact]
    public void AScriptsChoiceDialogsFirstFocusWaitsForThePrompt()
    {
        SystemFileCompiled();

        messages.ChoiceOpened(new ChoiceRequested(1, "System_mode_13", YesNo));
        SelectOpened("System_mode_13");
        Cursor(Settle, row: 0, top: 1);
        Cursor(Settle, row: 0, top: 0);

        Assert.Equal(
            [new Announcement("Nay, 2 of 2", Interrupt: false), new Announcement("Aye, 1 of 2", Interrupt: true)],
            announcer.Said);
    }

    [Fact]
    public void ASelectWithOtherChoicesGivesTheDialogNoText()
    {
        SystemFileCompiled();

        SelectOpened("System_mode_13", choice: "System_Choice_OK");
        Cursor(Settle, row: 0, top: 1);

        Assert.Equal(["Nay, 2 of 2"], Said());
    }

    [Fact]
    public void TheTextIsDroppedWhenAnotherScreenOpensFirst()
    {
        SystemFileCompiled();

        SelectOpened("System_mode_13");
        Cursor(Settle, row: 1, screenClass: "SingleChoiceBase@ope@ui@app", manager: 0xb0);
        Cursor(Settle, row: 0, top: 1);

        Assert.Equal(["2 of 2", "Nay, 2 of 2"], Said());
    }

    [Fact]
    public void APromptWithNoMessageGivesTheDialogNoText()
    {
        SystemFileCompiled();

        SelectOpened("System_missing");
        Cursor(Settle, row: 0, top: 1);

        Assert.Equal(["Nay, 2 of 2"], Said());
    }
}
