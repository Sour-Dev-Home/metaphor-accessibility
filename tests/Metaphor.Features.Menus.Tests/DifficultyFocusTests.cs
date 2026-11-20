using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>New Game's difficulty list (#540) is recognised by its owner's class, outside the menus, and each item the
/// cursor settles on spoken as "name, n of 5", top first, from the system file's difficulty names
/// (docs/findings/difficulty-select-shared-cursor.md). Hook events in, announcements out; made-up names only.</summary>
[Trait("Category", "Unit")]
public sealed class DifficultyFocusTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const string SystemSource =
        "@System_General_16\n{\nPlus\n<WAIT>\n}\n\n@System_General_17\n{\nGentle\n<WAIT>\n}\n\n" +
        "@System_General_18\n{\nMild\n<WAIT>\n}\n\n@System_General_19\n{\nMiddling\n<WAIT>\n}\n\n" +
        "@System_General_20\n{\nStern\n<WAIT>\n}\n\n@System_General_20_1\n{\nCruel\n<WAIT>\n}";
    private const string DifficultyClass = "UUIC_Common_DifficultySelect@ui@app";
    private const long List = 0xa0;
    private const int Settle = 0x1;
    private const int Down = 0x14;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public DifficultyFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(KnownScreens.All);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new DifficultyFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int command, int top, string? screenClass = DifficultyClass) =>
        cursors.Handled(new CursorInput(List, ClassRva: 0x100, command, Result: 1, top, Row: 0, Flags: 0x46, screenClass, Count: 5));

    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemFile, 6, SystemSource));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // #540's run: the list opened on index 2 with the menus reading closed, then down twice and up once.
    [Fact]
    public void EachDifficultyIsSpokenByNameAndPositionWhileTheMenusAreClosed()
    {
        SystemFileCompiled();

        Cursor(Settle, top: 2);
        Cursor(Down, top: 3);
        Cursor(Settle, top: 3);
        Cursor(Settle, top: 4);
        Cursor(Settle, top: 3);

        Assert.Equal(
            ["Difficulty, Middling, 3 of 5", "Stern, 4 of 5", "Cruel, 5 of 5", "Stern, 4 of 5"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheTopIsTheFirstName()
    {
        SystemFileCompiled();

        Cursor(Settle, top: 0);

        Assert.Equal(["Difficulty, Gentle, 1 of 5"], Said());
    }

    [Fact]
    public void ADifficultyWhoseNameIsMissingIsSpokenByItsPosition()
    {
        Cursor(Settle, top: 1);

        Assert.Equal(["Difficulty, 2 of 5"], Said());
    }

    [Fact]
    public void AListWithoutAnOwnerStaysSilentOutsideTheMenus()
    {
        SystemFileCompiled();

        Cursor(Settle, top: 2, screenClass: null);

        Assert.Empty(announcer.Said);
    }
}
