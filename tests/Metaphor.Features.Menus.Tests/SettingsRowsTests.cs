using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A settings page's rows by name (#560): a row whose label is known for the page and its row count is said by
/// its message text, as "name, n of total" (#96's order, the value left out until it is read); any other row by its
/// position alone. Hook events in, announcements out; made-up labels and text only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsRowsTests
{
    private const string SystemPcFile = "EN/message/system/System_Message_PC.msg";
    private const string SystemPage = "?$ConfigMenuChoicePcT@$0HG@@ope@ui@app";
    private const string SoundPage = "?$ConfigMenuChoicePcT@$0HH@@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public SettingsRowsTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(SettingsFocus.Screens);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        // Page 0 (System) with 3 rows: the first two named, the third unknown; page 1 (Sound) with 2 rows.
        SettingsPageRows[] rows =
        [
            new(Page: 0, Total: 3, NameLabels: ["Fake_row_A", "Fake_row_B", null]),
            new(Page: 1, Total: 2, NameLabels: ["Fake_row_C", "Fake_row_missing"]),
        ];
        screens.Observe(new SettingsFocus(library, announcer, new TextCleaner(TextCleanerRules.None), rows));
        messages.Compiled(new MessageFileCompiled(SystemPcFile, 48, string.Join("\n\n",
            Message("System_category_name_PC_01", "Alpha Pane"), Message("System_category_name_PC_02", "Beta Pane"),
            Message("Fake_row_A", "Fake <COLOR 3>speed<COLOR -1>"), Message("Fake_row_B", "Fake volume"),
            Message("Fake_row_C", "Fake output"))));
    }

    private void Cursor(int index, string screenClass, int? count, int flags = 0x46) =>
        cursors.Handled(new CursorInput(0xb0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: flags,
            screenClass, count));

    private static string Message(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void OpeningAPageSaysItsNameThenTheFocusedRowsName()
    {
        Cursor(0, SystemPage, count: 3);

        Assert.Equal([new Announcement("Alpha Pane, Fake speed, 1 of 3", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void EachMoveSaysTheRowsNameAndPosition()
    {
        Cursor(0, SystemPage, count: 3);

        Cursor(1, SystemPage, count: 3);

        Assert.Equal("Fake volume, 2 of 3", Said().Last());
    }

    [Fact]
    public void ARowWithNoKnownLabelIsSaidByItsPositionAlone()
    {
        Cursor(0, SystemPage, count: 3);

        Cursor(2, SystemPage, count: 3);

        Assert.Equal("3 of 3", Said().Last());
    }

    [Fact]
    public void ARowWhoseMessageIsMissingIsSaidByItsPositionAlone()
    {
        Cursor(0, SoundPage, count: 2);

        Cursor(1, SoundPage, count: 2);

        Assert.Equal(["Beta Pane, Fake output, 1 of 2", "2 of 2"], Said());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(null)]
    public void APageOfAnotherRowCountIsNotTheKnownOrder(int? count)
    {
        // System has 20 rows from the title and 24 in play: rows known for one count say nothing of the other.
        Cursor(0, SystemPage, count);

        Cursor(1, SystemPage, count);

        Assert.Equal(count is null ? "2" : "2 of 4", Said().Last());
    }

    // The OK button after a page's last row settles at index 0 with flags 0x43, its rows 0x47 (#560).
    [Fact]
    public void DownFromTheLastRowSaysTheOkButton()
    {
        Cursor(2, SystemPage, count: 3, flags: 0x47);

        Cursor(0, SystemPage, count: 3, flags: 0x43);

        Assert.Equal("OK, button", Said().Last());
    }

    [Fact]
    public void DownFromTheOkButtonSaysTheFirstRow()
    {
        Cursor(2, SystemPage, count: 3, flags: 0x47);
        Cursor(0, SystemPage, count: 3, flags: 0x43);

        Cursor(0, SystemPage, count: 3, flags: 0x47);

        Assert.Equal("Fake speed, 1 of 3", Said().Last());
    }

    [Fact]
    public void EachPinnedPageHasALabelSlotPerRow()
    {
        Assert.All(SettingsPageRows.Steam18330018, page => Assert.Equal(page.Total, page.NameLabels.Count));
    }

    // The pinned rows on build 548ddc95 (SettingsPageRows.Steam18330018).
    [Theory]
    [InlineData(20, 0, "Fake System_Option_14, 1 of 20")]
    [InlineData(20, 4, "Fake System_Option_89, 5 of 20")]
    [InlineData(20, 15, "Fake System_Option_91, 16 of 20")]
    [InlineData(20, 19, "Fake System_subject_name_PC_64, 20 of 20")]
    [InlineData(24, 7, "Fake System_Option_89, 8 of 24")]
    [InlineData(24, 18, "Fake System_Option_28, 19 of 24")]
    [InlineData(24, 19, "Fake System_Option_91, 20 of 24")]
    [InlineData(24, 23, "Fake System_subject_name_PC_64, 24 of 24")]
    [InlineData(24, 3, "4 of 24")]
    [InlineData(24, 0, "1 of 24")]
    public void TheSystemPageSaysItsPinnedRowsOnThisBuild(int count, int index, string said)
    {
        var pinned = new RecordingAnnouncer();
        var library = new MessageLibrary();
        var screens = new MenuScreens(SettingsFocus.Screens);
        screens.MenusRead(open: true);
        screens.Observe(new SettingsFocus(library, pinned, new TextCleaner(TextCleanerRules.None)));
        var fileMessages = new MessageEvents();
        fileMessages.ObserveFiles(library);
        string[] labels =
        [
            "System_Option_14", "System_Option_89", "System_Option_91", "System_Option_28",
            "System_subject_name_PC_64",
        ];
        fileMessages.Compiled(new MessageFileCompiled(
            SystemPcFile, 48, string.Join("\n\n", labels.Select(label => Message(label, $"Fake {label}")))));

        screens.Observe(new CursorInput(0xb0, 0x100, Settle, 1, Top: index, Row: 0, Flags: 0x47, SystemPage, count));

        Assert.Equal(said, pinned.Said[^1].Text);
    }
}
