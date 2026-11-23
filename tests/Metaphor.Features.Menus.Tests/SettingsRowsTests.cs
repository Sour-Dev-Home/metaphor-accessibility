using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A settings page's rows by name (#560): a row whose label is known for the page and its row count is said by
/// its message text, as "name, n of total", with its value between them where its setting is known (#96), and a change
/// to it by the new value alone; any other row by its position alone. Hook events in, announcements out; made-up labels,
/// text and values only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsRowsTests
{
    private const string SystemPcFile = "EN/message/system/System_Message_PC.msg";
    private const string SystemPage = "?$ConfigMenuChoicePcT@$0HG@@ope@ui@app";
    private const string SoundPage = "?$ConfigMenuChoicePcT@$0HH@@ope@ui@app";
    private const int Settle = 0x1;

    private static readonly TimeSpan Settled = TimeSpan.FromMilliseconds(200);

    private readonly FakeClock clock = new();
    private bool learnRowIds = true;
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
            new(Page: 0, Total: 3, NameLabels: ["Fake_row_A", "Fake_row_B", null]) {
                ValueIds = [27, null, 5],
                ValueChoices = [[new("Fake_choice_0"), new("Fake_choice_1"), new(null, "Literal"), new("Fake_choice_gone")], null, null],
            },
            new(Page: 1, Total: 2, NameLabels: ["Fake_row_C", "Fake_row_missing"]),
        ];
        screens.Observe(new SettingsFocus(
            library, announcer, new TextCleaner(TextCleanerRules.None), rows, KeyConfigTab.Steam18330018, clock)
        {
            LearnRowIds = () => learnRowIds,
        });
        messages.Compiled(new MessageFileCompiled(SystemPcFile, 48, string.Join("\n\n",
            Message("System_category_name_PC_01", "Alpha Pane"), Message("System_category_name_PC_02", "Beta Pane"),
            Message("Fake_row_A", "Fake <COLOR 3>speed<COLOR -1>"), Message("Fake_row_B", "Fake volume"),
            Message("Fake_row_C", "Fake output"), Message("Fake_choice_0", "Choice <COLOR 3>zero<COLOR -1>"),
            Message("Fake_choice_1", "Choice one"))));
    }

    // #734: a row with named choices says the choice's name, from its message label or its fixed text; a value outside
    // the choices, or a name that can't be found, is said as the number.
    [Theory]
    [InlineData(0, "Alpha Pane, Fake speed, Choice zero, 1 of 3")]
    [InlineData(1, "Alpha Pane, Fake speed, Choice one, 1 of 3")]
    [InlineData(2, "Alpha Pane, Fake speed, Literal, 1 of 3")]
    [InlineData(3, "Alpha Pane, Fake speed, 3, 1 of 3")]
    [InlineData(4, "Alpha Pane, Fake speed, 4, 1 of 3")]
    [InlineData(-1, "Alpha Pane, Fake speed, -1, 1 of 3")]
    public void AChoiceRowSaysTheChoicesNameOnFocus(int value, string said)
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, value)));

        Assert.Equal([said], Said());
    }

    [Theory]
    [InlineData(1, "Choice one")]
    [InlineData(2, "Literal")]
    [InlineData(7, "7")]
    public void AChoiceRowSaysTheChoicesNameOnChange(int value, string said)
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 0)));

        Cursor(0, SystemPage, count: 3, values: Values((27, value)));
        clock.Advance(Settled);

        Assert.Equal(said, Said().Last());
    }

    private void Cursor(int index, string screenClass, int? count, int flags = 0x46, SettingValues? values = null) =>
        cursors.Handled(new CursorInput(0xb0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: flags,
            screenClass, count, Values: values));

    // A page's values with the given settings set and every other one 0 (SettingValues' layout).
    private static SettingValues Values(params (int Id, int Value)[] values)
    {
        var bytes = new byte[SettingsPages.WorkingCopyLength];
        BitConverter.GetBytes(SettingValues.Count).CopyTo(bytes, 0x8);
        foreach (var (id, value) in values)
        {
            BitConverter.GetBytes(value).CopyTo(bytes, 0x10 + (4 * id));
        }
        return SettingValues.Of(bytes)!;
    }

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

    // #96: "<setting>, <value>, <n> of <total>" on focus, and only the new value on a change.
    [Fact]
    public void ARowWithAKnownValueSaysItBetweenItsNameAndPosition()
    {
        Cursor(1, SystemPage, count: 3, values: Values((27, 77)));

        Cursor(0, SystemPage, count: 3, values: Values((27, 77)));

        Assert.Equal("Fake speed, 77, 1 of 3", Said().Last());
    }

    [Fact]
    public void OpeningAPageOnARowWithAKnownValueSaysIt()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        Assert.Equal(["Alpha Pane, Fake speed, 50, 1 of 3"], Said());
    }

    [Fact]
    public void ChangingTheFocusedRowsValueSaysOnlyTheNewValue()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        Cursor(0, SystemPage, count: 3, values: Values((27, 51)));
        clock.Advance(Settled);
        Cursor(0, SystemPage, count: 3, values: Values((27, 52)));
        clock.Advance(Settled);

        Assert.Equal(
            [new Announcement("51", Interrupt: true), new Announcement("52", Interrupt: true)], announcer.Said.Skip(1));
    }

    // #735: a held key repeats about 100 ms apart; only the value that has been still for 200 ms is said.
    [Fact]
    public void AHeldRunOfChangesSaysOnlyTheLastValueOnceItIsStill()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        for (var value = 51; value <= 60; value++)
        {
            Cursor(0, SystemPage, count: 3, values: Values((27, value)));
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }
        Assert.Single(Said());
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(["Alpha Pane, Fake speed, 50, 1 of 3", "60"], Said());
    }

    [Fact]
    public void ASingleChangeIsSaidOnlyAfterTheWait()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        Cursor(0, SystemPage, count: 3, values: Values((27, 51)));
        clock.Advance(TimeSpan.FromMilliseconds(199));
        Assert.Single(Said());
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal("51", Said().Last());
        Assert.Equal(2, Said().Count());
    }

    [Fact]
    public void MovingToAnotherRowCancelsAPendingValue()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));
        Cursor(0, SystemPage, count: 3, values: Values((27, 51)));

        Cursor(1, SystemPage, count: 3, values: Values((27, 51)));
        clock.Advance(Settled);

        Assert.Equal(["Alpha Pane, Fake speed, 50, 1 of 3", "Fake volume, 2 of 3"], Said());
    }

    [Fact]
    public void AChangeToAnotherSettingsValueSaysNothing()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        Cursor(0, SystemPage, count: 3, values: Values((27, 50), (36, -1)));

        Assert.Single(Said());
    }

    [Fact]
    public void ARowWithoutAKnownValueSaysNoValue()
    {
        Cursor(0, SystemPage, count: 3, values: Values((27, 50)));

        Cursor(1, SystemPage, count: 3, values: Values((27, 50)));
        Cursor(1, SystemPage, count: 3, values: Values((27, 51)));

        Assert.Equal(["Alpha Pane, Fake speed, 50, 1 of 3", "Fake volume, 2 of 3"], Said());
    }

    // #770: a row with no pinned ID says the number of the one unpinned setting that moved as it changed, and then says
    // that number on focus too.
    [Fact]
    public void AnUnpinnedRowSaysTheOneSettingThatMovedOnChange()
    {
        Cursor(1, SystemPage, count: 3, values: Values((27, 50), (9, 3)));

        Cursor(1, SystemPage, count: 3, values: Values((27, 50), (9, 4)));
        clock.Advance(Settled);

        Assert.Equal(["Alpha Pane, Fake volume, 2 of 3", "4"], Said());
    }

    [Fact]
    public void WithLearnedValuesOffAnUnpinnedRowStaysSilent()
    {
        learnRowIds = false;
        Cursor(1, SystemPage, count: 3, values: Values((27, 50), (9, 3)));

        Cursor(1, SystemPage, count: 3, values: Values((27, 50), (9, 4)));
        clock.Advance(Settled);

        Assert.Equal(["Alpha Pane, Fake volume, 2 of 3"], Said());
    }

    [Fact]
    public void AnUnpinnedRowSaysItsLearnedValueOnFocus()
    {
        Cursor(1, SystemPage, count: 3, values: Values((9, 3)));
        Cursor(1, SystemPage, count: 3, values: Values((9, 4)));
        clock.Advance(Settled);

        Cursor(0, SystemPage, count: 3, values: Values((9, 4)));
        Cursor(1, SystemPage, count: 3, values: Values((9, 4)));

        Assert.Equal("Fake volume, 4, 2 of 3", Said().Last());
    }

    [Fact]
    public void TwoUnpinnedSettingsMovingTogetherSayNothing()
    {
        Cursor(1, SystemPage, count: 3, values: Values((9, 3), (10, 3)));

        Cursor(1, SystemPage, count: 3, values: Values((9, 4), (10, 4)));
        clock.Advance(Settled);

        Assert.Single(Said());
    }

    [Fact]
    public void ARowsValueIsSaidWithoutItsName()
    {
        Cursor(0, SystemPage, count: 3, values: Values());

        Cursor(2, SystemPage, count: 3, values: Values((5, 1)));

        Assert.Equal("1, 3 of 3", Said().Last());
    }

    [Fact]
    public void AValueChangeOnTheOkButtonSaysNothing()
    {
        Cursor(0, SystemPage, count: 3, flags: 0x47, values: Values((27, 50)));
        Cursor(0, SystemPage, count: 3, flags: 0x43, values: Values((27, 50)));

        Cursor(0, SystemPage, count: 3, flags: 0x43, values: Values((27, 51)));

        Assert.Equal("OK, button", Said().Last());
        Assert.Equal(2, Said().Count());
    }

    // #770, by ear on 2026-11-22: the System page's on/off rows say Off and On, not 0 and 1, and Button Display says
    // Auto and Type 1 to 4, not 0 to 4, once their setting is learned; a slider such as Camera Speed keeps its number.
    [Theory]
    [InlineData(20, 13, 1, "On")]
    [InlineData(20, 0, 0, "Off")]
    [InlineData(20, 9, 1, "On")]
    [InlineData(20, 18, 0, "Auto")]
    [InlineData(20, 18, 3, "Type 3")]
    [InlineData(24, 22, 2, "Type 2")]
    [InlineData(24, 16, 0, "Off")]
    [InlineData(20, 8, 64, "64")]
    public void TheSystemPagesLearnedRowsSayTheirChoicesOnThisBuild(int count, int index, int value, string said)
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(SettingsFocus.Screens);
        screens.MenusRead(open: true);
        screens.Observe(new SettingsFocus(
            library, announcer, new TextCleaner(TextCleanerRules.None), SettingsPageRows.Steam18330018,
            KeyConfigTab.Steam18330018, clock));
        var start = value == 0 ? 1 : 0;

        screens.Observe(new CursorInput(0xb0, 0x100, Settle, 1, Top: index, Row: 0, Flags: 0x47, SystemPage, count,
            Values: Values((9, start))));
        screens.Observe(new CursorInput(0xb0, 0x100, Settle, 1, Top: index, Row: 0, Flags: 0x47, SystemPage, count,
            Values: Values((9, value))));
        clock.Advance(Settled);

        Assert.Equal(said, Said().Last());
    }

    [Fact]
    public void EachPinnedPageHasAValueSlotPerRow()
    {
        Assert.All(SettingsPageRows.Steam18330018, page => Assert.Equal(page.Total, page.ValueIds.Count));
    }

    // The pinned values on build 548ddc95 (docs/raw-sources/settings-values-sessions-2026-11-21.md): Message Speed 17,
    // Skip Selections 18, Mouse Sensitivity 27, Controller Sensitivity 28, on both System pages.
    [Theory]
    [InlineData(20, 11, "Fake System_Option_72, Fake System_Option_83, 12 of 20")]
    [InlineData(20, 12, "Fake System_Option_24, On, 13 of 20")]
    [InlineData(20, 16, "Fake System_subject_name_PC_04, 77, 17 of 20")]
    [InlineData(20, 17, "Fake System_subject_name_PC_05, 52, 18 of 20")]
    [InlineData(20, 9, "Fake System_Option_23, 10 of 20")]
    [InlineData(24, 14, "Fake System_Option_72, Fake System_Option_83, 15 of 24")]
    [InlineData(24, 15, "Fake System_Option_24, On, 16 of 24")]
    [InlineData(24, 20, "Fake System_subject_name_PC_04, 77, 21 of 24")]
    [InlineData(24, 21, "Fake System_subject_name_PC_05, 52, 22 of 24")]
    [InlineData(24, 1, "2 of 24")]
    public void TheSystemPageSaysItsPinnedValuesOnThisBuild(int count, int index, string said)
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
            "System_Option_72", "System_Option_83", "System_Option_24", "System_Option_23", "System_subject_name_PC_04",
            "System_subject_name_PC_05",
        ];
        fileMessages.Compiled(new MessageFileCompiled(
            SystemPcFile, 48, string.Join("\n\n", labels.Select(label => Message(label, $"Fake {label}")))));

        screens.Observe(new CursorInput(0xb0, 0x100, Settle, 1, Top: index, Row: 0, Flags: 0x47, SystemPage, count,
            Values: Values((5, 9), (17, 1), (18, 1), (22, 9), (27, 77), (28, 52))));

        Assert.Equal(said, pinned.Said[^1].Text);
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
