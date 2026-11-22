using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The Settings category list and its pages (#559): opening the list says "Settings" (the maintainer's wording,
/// approved 2026-11-20) and the focused category, each category is said by its message text with "n of 5",
/// and opening a page says the page's name. Hook events in (cursor inputs carrying a screen class, the system message
/// file compiled), announcements out through the recording announcer; made-up category text only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsFocusTests
{
    private const string SystemPcFile = "EN/message/system/System_Message_PC.msg";
    private const string ListClass = "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app";
    private const string ListInPlayClass = "?$ConfigTopChoicePc@VConfigTopChoicePcLayout@ui@app@@@ope@ui@app";
    private const string SystemPage = "?$ConfigMenuChoicePcT@$0HG@@ope@ui@app";
    private const string SoundPage = "?$ConfigMenuChoicePcT@$0HH@@ope@ui@app";
    private const string GraphicsPage = "?$ConfigMenuChoicePcT@$0HI@@ope@ui@app";
    private const string DisplayPage = "?$ConfigMenuChoicePcT@$0HJ@@ope@ui@app";
    private const string KeyConfigPage = "ConfigMenuChoiceKeyConfig@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public SettingsFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(SettingsFocus.Screens);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new SettingsFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
    }

    private void Cursor(int index, string screenClass, int? count, long manager = 0xa0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, count));

    private void SystemFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemPcFile, 48, string.Join("\n\n",
        Category(1, "Alpha <COLOR 3>Pane<COLOR -1>"), Category(2, "Beta Pane"), Category(3, "Gamma Pane"),
        Category(4, "Delta Pane"), Category(5, "Epsilon Pane"), Category(6, "Zeta Pane"))));

    private static string Category(int number, string text) =>
        $"//--------\n@System_category_name_PC_0{number}\n{{\n{text}\n<WAIT>\n}}";

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachCategoryIsSaidByItsMessageTextWithItsPositionOfFive()
    {
        SystemFileCompiled();

        for (var index = 0; index < 5; index++)
        {
            Cursor(index, ListClass, count: 5);
        }

        Assert.Equal(
            [
                new Announcement("Settings, Alpha Pane, 1 of 5", Interrupt: true),
                new Announcement("Beta Pane, 2 of 5", Interrupt: true),
                new Announcement("Gamma Pane, 3 of 5", Interrupt: true),
                new Announcement("Delta Pane, 4 of 5", Interrupt: true),
                new Announcement("Epsilon Pane, 5 of 5", Interrupt: true),
            ],
            announcer.Said);
    }

    [Fact]
    public void TheInPlayListIsTheSameList()
    {
        SystemFileCompiled();

        Cursor(0, ListInPlayClass, count: 5);
        Cursor(3, ListInPlayClass, count: 5);

        Assert.Equal(["Settings, Alpha Pane, 1 of 5", "Delta Pane, 4 of 5"], Said());
    }

    [Fact]
    public void ACategoryWhoseMessageIsMissingIsSaidByItsPositionAlone()
    {
        Cursor(1, ListClass, count: 5);

        Assert.Equal(["Settings, 2 of 5"], Said());
    }

    [Theory]
    [InlineData(0, SystemPage, 20, "Alpha Pane, 1 of 20")]
    [InlineData(1, SoundPage, 6, "Beta Pane, 1 of 6")]
    [InlineData(2, GraphicsPage, 7, "Gamma Pane, 1 of 7")]
    [InlineData(3, DisplayPage, 4, "Delta Pane, 1 of 4")]
    public void OpeningAPageSaysItsNameAndThePosition(int category, string page, int rows, string expected)
    {
        SystemFileCompiled();
        Cursor(category, ListClass, count: 5);

        Cursor(0, page, rows, manager: 0xb0);

        Assert.Equal(expected, Said().Last());
    }

    [Fact]
    public void MovingOnAPageSaysThePositionAloneUntilItsRowsAreRead()
    {
        SystemFileCompiled();
        Cursor(0, SystemPage, count: 24);

        Cursor(1, SystemPage, count: 24);

        Assert.Equal(["Alpha Pane, 1 of 24", "2 of 24"], Said());
    }

    [Fact]
    public void APageWhoseCountIsUnreadableSaysThePositionWithoutATotal()
    {
        SystemFileCompiled();

        Cursor(2, SoundPage, count: null);

        Assert.Equal(["Beta Pane, 3"], Said());
    }

    [Fact]
    public void BackOnTheListFromAPageEachCategoryIsSaidAgain()
    {
        SystemFileCompiled();
        Cursor(1, ListClass, count: 5);
        Cursor(0, SoundPage, count: 6, manager: 0xb0);

        // Back on the list, its manager logs nothing new until the cursor moves
        // (docs/raw-sources/settings-category-labels-session1-2026-11-20.md).
        Cursor(2, ListClass, count: 5);

        Assert.Equal(["Settings, Beta Pane, 2 of 5", "Beta Pane, 1 of 6", "Settings, Gamma Pane, 3 of 5"], Said());
    }

    // #563: Key Configuration's grid cursor, two cells to a row (docs/raw-sources/grid-cursor-fields-static-2026-11-21.md).
    private void GridCursor(
        int line, int column, int? count = 23, int scroll = 0, int page = 0, bool? onButton = null, int flags = 0x46) =>
        cursors.Handled(new CursorInput(0xc0, Core.Menus.GridCursor.ClassRva, Settle, Result: 1, Top: (line * 2) + column,
            Row: scroll, flags, KeyConfigPage, count,
            Grid: Core.Menus.GridCursor.Of(columns: 2, column, line, scroll, page, pageStride: 0), OnButton: onButton));

    // #589: made-up texts under the sub-tab and action labels Key Configuration resolves
    // (docs/raw-sources/key-config-actions-session-2026-11-21.md).
    private void KeyConfigFileCompiled() => messages.Compiled(new MessageFileCompiled(SystemPcFile, 49, string.Join("\n\n",
        Label("System_mid_subject_name_key_PC_01", "Tab Alpha"), Label("System_mid_subject_name_key_PC_02", "Tab Beta"),
        Label("System_mid_subject_name_key_PC_21", "Tab Epsilon"), Label("System_Keybind_name_PC_32", "Act One"),
        Label("System_Keybind_name_PC_33", "Act Two"), Label("System_Keybind_name_PC_28", "Act Last"),
        Label("System_Keybind_name_PC_36", "Act Memo"), Label("System_Keybind_name_PC_06", "Act Look"))));

    private static string Label(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    [Fact]
    public void OpeningKeyConfigurationSaysItsSubTabAndTheCellsAction()
    {
        SystemFileCompiled();
        KeyConfigFileCompiled();

        GridCursor(line: 0, column: 0);

        Assert.Equal(["Epsilon Pane, Tab Alpha, Act One, column 1, row 1 of 12"], Said());
    }

    [Fact]
    public void MovingOnASubTabSaysEachCellsActionWithoutTheSubTab()
    {
        KeyConfigFileCompiled();
        GridCursor(line: 0, column: 0);

        GridCursor(line: 0, column: 1);
        GridCursor(line: 5, column: 0, scroll: 6);

        Assert.Equal(["Act Two, column 2, row 1 of 12", "Act Last, column 1, row 12 of 12"], Said().Skip(1));
    }

    [Fact]
    public void SwitchingSubTabSaysTheNewSubTab()
    {
        KeyConfigFileCompiled();
        GridCursor(line: 0, column: 0);

        GridCursor(line: 1, column: 0, count: 3);
        GridCursor(line: 3, column: 0, count: 12);

        Assert.Equal(["Tab Beta, Act Memo, column 1, row 2 of 2", "Tab Epsilon, Act Look, column 1, row 4 of 6"],
            Said().Skip(1));
    }

    [Fact]
    public void AFourteenCellSubTabIsFieldOrDungeonSoItsCellsAreSaidByPosition()
    {
        // Field and Dungeon both have 14 cells, and nothing read yet tells them apart.
        KeyConfigFileCompiled();
        GridCursor(line: 0, column: 0);

        GridCursor(line: 0, column: 1, count: 14);

        Assert.Equal(["column 2, row 1 of 7"], Said().Skip(1));
    }

    [Fact]
    public void BackOnAKnownSubTabFromAFourteenCellOneSaysTheSubTab()
    {
        KeyConfigFileCompiled();
        GridCursor(line: 0, column: 0, count: 3);
        GridCursor(line: 0, column: 0, count: 14);

        GridCursor(line: 1, column: 0, count: 3);

        Assert.Equal("Tab Beta, Act Memo, column 1, row 2 of 2", Said().Last());
    }

    [Fact]
    public void NoTwoKnownSubTabsShareACount()
    {
        // The count is all that tells a sub-tab, so a shared one would name the wrong actions.
        var counts = KeyConfigTab.Steam18330018.Select(tab => tab.ActionLabels.Count).ToList();

        Assert.Equal(counts.Count, counts.Distinct().Count());
    }

    [Fact]
    public void KeyConfigurationsOkButtonIsSaidAsTheButton()
    {
        // The flags' item bit stays clear on cells there (0x42), so only the screen's own field says the button.
        KeyConfigFileCompiled();
        GridCursor(line: 0, column: 0, onButton: false, flags: 0x42);

        GridCursor(line: 5, column: 0, scroll: 6, onButton: true, flags: 0x42);
        GridCursor(line: 5, column: 0, scroll: 6, onButton: false, flags: 0x46);

        Assert.Equal(["Tab Alpha, Act One, column 1, row 1 of 12", "OK, button", "Act Last, column 1, row 12 of 12"],
            Said());
    }

    [Fact]
    public void OpeningKeyConfigurationSaysItsNameTheColumnAndTheRow()
    {
        SystemFileCompiled();
        Cursor(4, ListClass, count: 5);

        GridCursor(line: 0, column: 0);

        Assert.Equal("Epsilon Pane, column 1, row 1 of 12", Said().Last());
    }

    [Fact]
    public void MovingOnTheGridSaysTheColumnAndTheRowFromTheTopOfTheList()
    {
        SystemFileCompiled();
        GridCursor(line: 0, column: 0);

        GridCursor(line: 0, column: 1);
        GridCursor(line: 3, column: 1, scroll: 2);

        Assert.Equal(["Epsilon Pane, column 1, row 1 of 12", "column 2, row 1 of 12", "column 2, row 6 of 12"], Said());
    }

    [Fact]
    public void AGridWhoseCountIsUnreadableSaysTheRowWithoutATotal()
    {
        GridCursor(line: 1, column: 1, count: null);

        Assert.Equal(["column 2, row 2"], Said());
    }

    [Fact]
    public void AGridOnALaterPageSaysTheRowWithoutATotal()
    {
        // How a page's cells number against the count isn't known, so its rows have no total.
        GridCursor(line: 1, column: 0, page: 1);

        Assert.Equal(["column 1, row 2"], Said());
    }

    [Fact]
    public void KeyConfigurationWithoutItsGridSaysThePosition()
    {
        cursors.Handled(new CursorInput(0xc0, Core.Menus.GridCursor.ClassRva, Settle, Result: 1, Top: 2, Row: 0,
            Flags: 0x46, KeyConfigPage, Count: 23));

        Assert.Equal(["3 of 23"], Said());
    }

    [Fact]
    public void AnotherScreenIsLeftToItsOwnFeature()
    {
        SystemFileCompiled();

        Cursor(0, "TitleChoice@ope@ui@app", count: 3);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ThePagesAndListsAreKnownScreens()
    {
        Assert.Subset(KnownScreens.All.ToHashSet(), SettingsFocus.Screens.ToHashSet());
    }
}
