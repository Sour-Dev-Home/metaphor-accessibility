using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The title menu's focus is spoken (#94): "Main menu" with the focused item when its cursor first becomes
/// active, then each item the cursor settles on as "item, n of total", each cutting off the last. Checked through the
/// recording announcer; made-up item text only.</summary>
[Trait("Category", "Unit")]
public sealed class TitleMenuFocusTests
{
    private const string TitleFile = "EN/message/system/Title_System.msg";
    private const string TitleSource = "@Title_System_07\n{\nYes\nNo\n<WAIT>\n}\n\n@Title_System_08\n{\nBegin\nResume\n<COLOR 3>Options<COLOR -1>\n<WAIT>\n}";
    private const long Title = 0xa0;
    private const int Settle = 0x1;
    private const int Down = 0x16;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();
    private readonly TitleMenuFocus focus;

    public TitleMenuFocusTests() => focus = new(messages, announcer, new TextCleaner(TextCleanerRules.None));

    private static CursorInput Input(long manager, int command, int row) =>
        new(manager, ClassRva: 0x100, command, Result: 1, Top: 0, Row: row, Flags: 0x46);

    private void TitleFileCompiled(string path = TitleFile)
    {
        var file = new MessageFileCompiled(path, 56, TitleSource);
        messages.Observe(file);
        focus.Observe(file);
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheFirstCursorAfterTheTitleFileCompilesIsAnnouncedAsTheMainMenu()
    {
        TitleFileCompiled();

        focus.Observe(Input(Title, Settle, row: 0));

        Assert.Equal([new Announcement("Main menu, Begin, 1 of 3", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void EachItemTheCursorSettlesOnIsAnnouncedWithItsPositionCuttingOffTheLast()
    {
        TitleFileCompiled();
        focus.Observe(Input(Title, Settle, row: 0));

        focus.Observe(Input(Title, Settle, row: 1));
        focus.Observe(Input(Title, Settle, row: 2));
        focus.Observe(Input(Title, Settle, row: 0));

        Assert.Equal(
            [
                new Announcement("Resume, 2 of 3", Interrupt: true),
                new Announcement("Options, 3 of 3", Interrupt: true),
                new Announcement("Begin, 1 of 3", Interrupt: true),
            ],
            announcer.Said.Skip(1));
    }

    [Fact]
    public void AMoveIsAnnouncedOnlyWhereTheCursorSettles()
    {
        TitleFileCompiled();
        focus.Observe(Input(Title, Settle, row: 0));

        focus.Observe(Input(Title, Down, row: 1));
        focus.Observe(Input(Title, Settle, row: 0));
        focus.Observe(Input(Title, Down, row: 1));
        focus.Observe(Input(Title, Settle, row: 1));

        Assert.Equal(["Main menu, Begin, 1 of 3", "Resume, 2 of 3"], Said());
    }

    [Fact]
    public void ACursorThatStaysOnItsItemIsNotAnnouncedAgain()
    {
        TitleFileCompiled();
        focus.Observe(Input(Title, Settle, row: 1));

        focus.Observe(Input(Title, Settle, row: 1) with { Flags = 0x66 });

        Assert.Equal(["Main menu, Resume, 2 of 3"], Said());
    }

    [Fact]
    public void NothingIsSaidBeforeTheTitleFileCompiles()
    {
        focus.Observe(Input(Title, Settle, row: 0));
        focus.Observe(new MessageFileCompiled("EN/message/system/Other_System.msg", 3, TitleSource));
        focus.Observe(Input(Title, Settle, row: 1));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OtherMenusCursorsAreNotAnnounced()
    {
        TitleFileCompiled();
        focus.Observe(Input(Title, Settle, row: 0));

        focus.Observe(Input(0xb0, Settle, row: 1));

        Assert.Equal(["Main menu, Begin, 1 of 3"], Said());
    }

    [Fact]
    public void APositionPastTheMenusItemsIsNotAnnounced()
    {
        TitleFileCompiled();
        focus.Observe(Input(Title, Settle, row: 0));

        focus.Observe(Input(Title, Settle, row: 3));

        Assert.Equal(["Main menu, Begin, 1 of 3"], Said());
    }

    [Fact]
    public void AnItemWhoseTextIsMissingIsAnnouncedByItsPosition()
    {
        focus.Observe(new MessageFileCompiled(TitleFile, 56, TitleSource));

        focus.Observe(Input(Title, Settle, row: 1));

        Assert.Equal(["Main menu, 2 of 3"], Said());
    }

    [Fact]
    public void TheTitleFileIsKnownByItsPathInAnyLanguageFolder()
    {
        TitleFileCompiled(@"JP\message\system\title_system.msg");

        focus.Observe(Input(Title, Settle, row: 2));

        Assert.Equal(["Main menu, Options, 3 of 3"], Said());
    }
}
