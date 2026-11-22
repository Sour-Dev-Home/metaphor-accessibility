using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The field's quick menu (#584): each item the cursor settles on is said by its name, from the system file or
/// the mod's own word for the three rows the game draws itself, and "n of 8", in the cursor order the game fetched its
/// help lines in (docs/findings/message-serials-from-file-positions.md). Hook events in, announcements out; made-up file
/// text only.</summary>
[Trait("Category", "Unit")]
public sealed class QuickMenuFocusTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const string MenuClass = "InstantMenuChoice@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public QuickMenuFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new QuickMenuFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int index, int? count = 8) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            MenuClass, count));

    // The names, numbered as the file numbers their labels: 01 to 07, then 15 and 17, each with a help line after it.
    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemFile, 6, string.Join("\n\n",
        Entry(1, "Bag"), Entry(2, "Tale"), Entry(3, "Lessons"), Entry(4, "Options"), Entry(5, "Open"),
        Entry(6, "Keep"), Entry(7, "Leave"), Entry(8, "Help for the bag"), Entry(15, "Notes <COLOR 3>"), Entry(17, "Skill"))));

    private static string Entry(int number, string text) =>
        $"//--------\n@System_EasyMenu_{number:00}\n{{\n{text}\n<WAIT>\n}}";

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachItemIsSaidByNameAndPositionOfEightInCursorOrder()
    {
        SystemFileCompiled();

        for (var index = 0; index < 8; index++)
        {
            Cursor(index);
        }

        Assert.Equal(
            ["Bag, 1 of 8", "Open, 2 of 8", "Keep, 3 of 8", "Tutorials, 4 of 8", "Tale, 5 of 8", "Memorandum, 6 of 8",
                "Settings, 7 of 8", "Leave, 8 of 8"],
            Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    // Rows 4, 6 and 7 draw words no message holds (heard 2026-11-21, #591): the mod's own words, the maintainer's
    // choice, whether or not the file has compiled.
    [Fact]
    public void TheRowsTheGameDrawsItselfAreSaidInTheModsOwnWords()
    {
        Cursor(3);
        Cursor(5);
        Cursor(6);

        Assert.Equal(["Tutorials, 4 of 8", "Memorandum, 6 of 8", "Settings, 7 of 8"], Said());
    }

    [Fact]
    public void AnItemWhoseNameIsMissingIsSaidByPositionAlone()
    {
        Cursor(0);

        Assert.Equal(["1 of 8"], Said());
    }

    // From the 2026-11-21 session (#677) the menu has nine items: a new first row (help "Use a skill"; its name is
    // probably System_EasyMenu_17 [NEEDS VERIFICATION]), then the eight above moved down one.
    [Fact]
    public void ANineItemMenuNamesTheNewRowFirstAndTheOldRowsShiftedDownOne()
    {
        SystemFileCompiled();

        for (var index = 0; index < 9; index++)
        {
            Cursor(index, count: 9);
        }

        Assert.Equal(
            ["Skill, 1 of 9", "Bag, 2 of 9", "Open, 3 of 9", "Keep, 4 of 9", "Tutorials, 5 of 9", "Tale, 6 of 9",
                "Memorandum, 7 of 9", "Settings, 8 of 9", "Leave, 9 of 9"],
            Said());
    }

    [Fact]
    public void ANineItemMenuWithoutTheNewRowsNameSaysItsPositionAlone()
    {
        Cursor(0, count: 9);

        Assert.Equal(["1 of 9"], Said());
    }

    // Any other length is a layout nobody mapped: positions only.
    [Fact]
    public void AMenuOfAnotherLengthIsSaidByPositionAlone()
    {
        SystemFileCompiled();

        Cursor(0, count: 10);
        Cursor(9, count: 10);

        Assert.Equal(["1 of 10", "10 of 10"], Said());
    }
}
