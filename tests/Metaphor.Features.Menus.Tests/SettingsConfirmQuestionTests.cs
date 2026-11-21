using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A settings page's yes/no dialog says its question before the focused choice (#562): the save-changes one
/// after B on a page or A on its OK button, the Restore Defaults one after Start, as the by-ear runs of 2026-11-21 opened
/// them (docs/raw-sources/settings-confirm-dialogs-session-2026-11-21.md). Pad reads and cursor inputs in,
/// announcements out; made-up message text only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsConfirmQuestionTests
{
    private const string SystemSource =
        "@System_Choice_YESNO SEL 0\n{\nAye\nNay\n<WAIT>\n}\n\n" +
        "@System_Option_63\n{\nKeep these?\n<WAIT>\n}\n\n" +
        "@System_Option_64\n{\nBack to the start?\n<WAIT>\n}";
    private const string YesNoClass = "YesNoChoiceSample@ope@ui@app";
    private const string ListClass = "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app";
    private const string SubScreenClass = "ConfigBrightnessChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int OnRow = 0x47;
    private const int OnOkButton = 0x43;
    private const int A = 0x4000;
    private const int B = 0x2000;
    private const int Start = 0x0008;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly MenuScreens screens = new([YesNoDialogFocus.Screen, .. SettingsFocus.Screens]);
    private readonly SettingsConfirmQuestion question = new();
    private long manager = 0x100;

    public SettingsConfirmQuestionTests()
    {
        var library = new MessageLibrary();
        var messages = new MessageEvents();
        messages.ObserveFiles(library);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 6, SystemSource));
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new YesNoDialogFocus(library, announcer, new TextCleaner(TextCleanerRules.None), question));
        screens.Observe(question);
    }

    private void Cursor(string screenClass, int row = 0, int flags = OnRow, int count = 20) =>
        cursors.Handled(new CursorInput(manager, 0x100, Settle, Result: 1, Top: row, Row: 0, flags, screenClass, count));

    private void OnPage(int row = 0, int flags = OnRow) => Cursor(SettingsPages.Classes[0], row, flags);

    private void Press(int buttons)
    {
        question.Pad(0, buttons, made: true);
        question.Pad(0, 0, made: true);
    }

    private void DialogOpens()
    {
        manager++;
        Cursor(YesNoClass, count: 2);
    }

    private string LastSaid() => announcer.Said[^1].Text;

    [Fact]
    public void BOnAPageOpensTheSaveQuestion()
    {
        OnPage(row: 16);

        Press(B);
        DialogOpens();

        Assert.Equal("Keep these? Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void StartOnAPageOpensTheRestoreDefaultsQuestion()
    {
        OnPage(row: 3);

        Press(Start);
        DialogOpens();

        Assert.Equal("Back to the start? Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void AOnThePagesOkButtonOpensTheSaveQuestion()
    {
        OnPage(row: 0, flags: OnOkButton);

        Press(A);
        DialogOpens();

        Assert.Equal("Keep these? Aye, 1 of 2", LastSaid());
    }

    // A on a row opens that row's own screen, not one of these two dialogs.
    [Fact]
    public void AOnARowGivesNoQuestion()
    {
        OnPage(row: 2);

        Press(A);
        DialogOpens();

        Assert.Equal("Aye, 1 of 2", LastSaid());
    }

    // B on the brightness screen opened a dialog whose question is unknown (run A, line 354918).
    [Fact]
    public void BOnAScreenOpenedFromAPageGivesNoQuestion()
    {
        OnPage(row: 2);
        manager++;
        Cursor(SubScreenClass, count: 1);

        Press(B);
        DialogOpens();

        Assert.Equal("Aye, 1 of 2", LastSaid());
    }

    // B with nothing changed goes straight to the category list; a dialog opened later isn't the save one.
    [Fact]
    public void AQuestionIsForgottenWhenAnotherScreenOpensFirst()
    {
        OnPage();
        Press(B);
        manager++;
        Cursor(ListClass, count: 5);

        DialogOpens();

        Assert.Equal("Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void AQuestionIsForgottenWhenTheFocusMovesFirst()
    {
        OnPage();
        Press(Start);
        OnPage(row: 1);

        DialogOpens();

        Assert.Equal("Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void AQuestionIsSaidOnceAndMovesSayTheChoiceAlone()
    {
        OnPage();
        Press(B);
        DialogOpens();

        cursors.Handled(new CursorInput(manager, 0x100, Settle, Result: 1, Top: 1, Row: 0, 0x46, YesNoClass, 2));

        Assert.Equal(["Keep these? Aye, 1 of 2", "Nay, 2 of 2"], announcer.Said.Select(said => said.Text));
    }

    // A read the game reports as not made (#225), or a button still held from before, is no press.
    [Fact]
    public void OnlyAMadeReadsNewPressCounts()
    {
        question.Pad(0, B, made: true);
        OnPage();

        question.Pad(0, B, made: true);
        question.Pad(1, Start, made: false);
        DialogOpens();

        Assert.Equal("Aye, 1 of 2", LastSaid());
    }

    [Fact]
    public void ThePressOnAnyPadCounts()
    {
        OnPage();

        question.Pad(1, B, made: true);
        DialogOpens();

        Assert.Equal("Keep these? Aye, 1 of 2", LastSaid());
    }
}
