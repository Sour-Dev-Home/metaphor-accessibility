using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The field's quick menu (#584): each item the cursor settles on is said by its name from the system file and
/// "n of 8", in the cursor order the game fetched its help lines in (docs/findings/message-serials-from-file-positions.md).
/// Hook events in, announcements out; made-up names only.</summary>
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

    // The names, numbered as the file numbers their labels: 01 to 07, then 15, each with a help line after it.
    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemFile, 6, string.Join("\n\n",
        Entry(1, "Bag"), Entry(2, "Tale"), Entry(3, "Lessons"), Entry(4, "Options"), Entry(5, "Open"),
        Entry(6, "Keep"), Entry(7, "Leave"), Entry(8, "Help for the bag"), Entry(15, "Notes <COLOR 3>"))));

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
            ["Bag, 1 of 8", "Open, 2 of 8", "Keep, 3 of 8", "Lessons, 4 of 8", "Tale, 5 of 8", "Notes, 6 of 8",
                "Options, 7 of 8", "Leave, 8 of 8"],
            Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void AnItemWhoseNameIsMissingIsSaidByPositionAlone()
    {
        Cursor(0);

        Assert.Equal(["1 of 8"], Said());
    }

    // Later in the game the menu has a ninth item, whose place in the order isn't known: positions only.
    [Fact]
    public void AMenuOfAnotherLengthIsSaidByPositionAlone()
    {
        SystemFileCompiled();

        Cursor(0, count: 9);
        Cursor(8, count: 9);

        Assert.Equal(["1 of 9", "9 of 9"], Said());
    }
}
