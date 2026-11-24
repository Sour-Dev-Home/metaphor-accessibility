using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The quick menu rows' help lines (#907, ADR 0023): the menu fetches each row's help line by serial, in cursor
/// order, in one frame before its first cursor record (docs/findings/message-serials-from-file-positions.md,
/// docs/raw-sources/quick-menu-nine-items-run-2026-11-21.md), flags 8. The details key says the focused row's line; with
/// the on-focus setting each focus says it after the row. Hook events in, announcements and details out; made-up file
/// text only.</summary>
[Trait("Category", "Unit")]
public sealed class QuickMenuHelpTests
{
    private const string SystemPath = "EN/message/system/System_Message.msg";
    private const string MenuClass = "InstantMenuChoice@ope@ui@app";
    private const int First = 1500;
    private const int Settle = 0x1;

    // The help lines' numbers in the order the 8-row menu fetched them, then the 9-row menu's first.
    private static readonly int[] EightRows = [8, 12, 13, 10, 9, 16, 11, 14];

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();
    private readonly QuickMenuHelp help;
    private bool onFocus;
    private bool on = true;

    public QuickMenuHelpTests()
    {
        var serials = new MessageSerials([new SerialRange("message/system/System_Message.msg", First, 20)]);
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled(SystemPath, 6, string.Join("\n\n",
            Enumerable.Range(0, 20).Select(at => at == 4
                ? Entry("System_Dummy", "Not a help line")
                : Entry($"System_EasyMenu_{at:00}", $"Help {at:00}")))));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        help = new QuickMenuHelp(serials, announcer, new TextCleaner(TextCleanerRules.None))
        {
            On = () => on,
            SayOnFocus = () => onFocus,
        };
        // As MenusModule wires it: the help hears each cursor record before the menu screens do.
        cursors.Observe(help);
        cursors.Observe(screens);
        screens.Observe(new QuickMenuFocus(new MessageLibrary(), announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(help);
        messages.ObserveSerials(help);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Fetch(params int[] numbers)
    {
        foreach (var number in numbers)
        {
            messages.Resolved(new SerialResolved(First + number, QuickMenuHelp.HelpFlags));
        }
    }

    private void Cursor(int index, int? count = 8, long manager = 0xa0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            MenuClass, count));

    private string? Details(int index, int count = 8) =>
        help.Details(new ScreenEvent(ScreenChange.FocusMoved, QuickMenuFocus.Screen, MenuClass, index, count));

    [Fact]
    public void TheDetailsAreTheFocusedRowsHelpLineInTheOrderTheMenuFetchedThem()
    {
        Fetch(EightRows);
        Cursor(0);

        Assert.Equal("Help 08", Details(0));
        Assert.Equal("Help 12", Details(1));
        Assert.Equal("Help 10", Details(3));
        Assert.Equal("Help 14", Details(7));
    }

    [Fact]
    public void ANineRowMenuStartsWithItsExtraRowsLine()
    {
        Fetch([18, .. EightRows]);
        Cursor(0, count: 9);

        Assert.Equal("Help 18", Details(0, 9));
        Assert.Equal("Help 08", Details(1, 9));
        Assert.Equal("Help 14", Details(8, 9));
    }

    [Fact]
    public void ARowPastTheFetchedLinesSaysNone()
    {
        Fetch(8, 12);
        Cursor(0);

        Assert.Null(Details(5));
    }

    [Fact]
    public void ALabelThatIsNotAHelpLineIsNotAKeptLine()
    {
        Fetch(4, 8);
        Cursor(0);

        Assert.Equal("Help 08", Details(0));
        Assert.Null(Details(1));
    }

    [Fact]
    public void AnotherOpeningTakesTheNewBatch()
    {
        Fetch(EightRows);
        Cursor(0);
        Fetch(18, 8);
        Cursor(0, count: 2, manager: 0xb0);

        Assert.Equal("Help 18", Details(0, 2));
        Assert.Equal("Help 08", Details(1, 2));
    }

    [Fact]
    public void AnotherScreensFocusHasNoQuickMenuHelp()
    {
        Fetch(EightRows);
        Cursor(0);

        Assert.Null(help.Details(new ScreenEvent(ScreenChange.FocusMoved, ItemFocus.List, ItemFocus.List.ScreenClass, 0, 8)));
    }

    [Fact]
    public void TheDetailsAreSilentWhileTheSettingIsOff()
    {
        Fetch(EightRows);
        Cursor(0);
        on = false;

        Assert.Null(Details(0));
    }

    [Fact]
    public void FocusSaysNoHelpUnlessTheOnFocusSettingIsOn()
    {
        Fetch(EightRows);
        Cursor(0);
        Cursor(1);

        Assert.DoesNotContain(Said(), said => said.StartsWith("Help", StringComparison.Ordinal));
    }

    [Fact]
    public void WithTheOnFocusSettingEachFocusSaysItsHelpLineAfterTheRow()
    {
        onFocus = true;
        Fetch(EightRows);
        Cursor(0);
        Cursor(1);

        var said = announcer.Said.Where(line => line.Text.StartsWith("Help", StringComparison.Ordinal)).ToList();
        Assert.Equal(["Help 08", "Help 12"], said.Select(line => line.Text));
        Assert.All(said, line => Assert.False(line.Interrupt));
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);
}
