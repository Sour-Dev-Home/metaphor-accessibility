using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's Retry and confirm prompts (#262, #257): the Retry question from CommandBattle_HelpText_05, then
/// Yes or No from the yes/no choice message, each cutting off the last (ADR 0023). Hook events in, announcements out;
/// made-up message text only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleConfirmFocusTests
{
    private const string RetryClass = "BTL_RewindComfirm@ope@ui@app";
    private const string ConfirmClass = "BTL_Comfirm@ope@ui@app";

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public BattleConfirmFocusTests(bool withMessages = true)
    {
        var library = new MessageLibrary();
        var messages = new MessageEvents();
        messages.ObserveFiles(library);
        if (withMessages)
        {
            messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 6,
                "@System_Choice_YESNO SEL 0\n{\nAye\nNay\n<WAIT>\n}"));
            messages.Compiled(new MessageFileCompiled("EN/message/system/CommandBattle_Message.msg", 7,
                "@CommandBattle_HelpText_05\n{\nTurn back time?\n<WAIT>\n}"));
        }
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        screens.Observe(new BattleConfirmFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(string screenClass, int index) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, 0x1, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, Count: 2));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheRetryPromptSaysItsQuestionThenYesAndNo()
    {
        Cursor(RetryClass, 0);
        Cursor(RetryClass, 1);

        Assert.Equal(["Turn back time? Aye, 1 of 2", "Nay, 2 of 2"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void AnotherConfirmPromptSaysConfirmThenYesAndNo()
    {
        Cursor(ConfirmClass, 0);

        Assert.Equal(["Confirm, Aye, 1 of 2"], Said());
    }

    [Fact]
    public void ARetryPromptWhoseQuestionIsMissingSaysConfirm()
    {
        var bare = new BattleConfirmFocusTests(withMessages: false);
        bare.Cursor(RetryClass, 0);

        Assert.Equal(["Confirm, 1 of 2"], bare.Said());
    }

    [Fact]
    public void BothPromptsAreKnownAndShownOutsideTheMenus()
    {
        Assert.Contains(BattleConfirmFocus.Retry, KnownScreens.All);
        Assert.Contains(BattleConfirmFocus.Confirm, KnownScreens.All);
        Assert.True(BattleConfirmFocus.Retry.OutsideMenus);
        Assert.True(BattleConfirmFocus.Confirm.OutsideMenus);
    }
}
