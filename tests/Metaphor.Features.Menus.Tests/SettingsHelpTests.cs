using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The game's own settings rows' help lines on the details key (#560, #907, ADR 0023): each move on a settings
/// page resolves the focused row's name and then its help line before the settled cursor record
/// (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md), so the line fetched since the last settled input is
/// the row's. A page's help lines are <c>System_Option_29</c> to <c>_44</c> and so on, and <c>System_tips_message_PC_*</c>
/// (docs/findings/settings-screens-inventory-static.md). Hook events in, details out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsHelpTests
{
    private const int Settle = 0x1;
    private const int Move = 0x14;
    private const int OptionFirst = 1700;
    private const int TipsFirst = 11800;
    private static readonly string PageClass = SettingsPages.Classes[0];

    private readonly MessageEvents messages = new();
    private readonly SettingsHelp help;
    private bool on = true;

    public SettingsHelpTests()
    {
        var serials = new MessageSerials([
            new SerialRange("message/system/System_Message.msg", OptionFirst, 120),
            new SerialRange("message/system/System_Message_PC.msg", TipsFirst, 8),
        ]);
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 21, string.Join("\n\n",
            Enumerable.Range(0, 120).Select(at => Entry($"System_Option_{at:000}", $"Option {at}")))));
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message_PC.msg", 22, string.Join("\n\n",
            Enumerable.Range(0, 8).Select(at => Entry($"System_tips_message_PC_{at:00}", $"Tip {at}")))));
        help = new SettingsHelp(serials, new TextCleaner(TextCleanerRules.None)) { On = () => on };
        messages.ObserveSerials(help);
    }

    private static string Entry(string label, string text) => $"//--------\n@{label}\n{{\n{text}\n<WAIT>\n}}";

    private void Fetch(int serial) => messages.Resolved(new SerialResolved(serial, 9));

    private void Cursor(int index, int command = Settle, int? count = 20, string? screenClass = null, int flags = 0x47) =>
        help.Observe(new CursorInput(0xa0, ClassRva: 0x100, command, Result: 1, Top: index, Row: 0, Flags: flags,
            screenClass ?? PageClass, count));

    private string? Details(int index, int? count = 20, string? screenClass = null, bool offList = false) =>
        help.Details(new ScreenEvent(ScreenChange.FocusMoved, null, screenClass ?? PageClass, index, count)
        {
            OffList = offList,
        });

    [Fact]
    public void ARowsDetailsAreTheHelpLineFetchedBeforeItsSettledCursor()
    {
        // A move resolves the row's name, a redrawn row's name, then the help line.
        Fetch(OptionFirst + 14);
        Fetch(OptionFirst + 16);
        Fetch(OptionFirst + 30);
        Cursor(2);

        Assert.Equal("Option 30", Details(2));
    }

    [Fact]
    public void ThePcTipsAreHelpLinesToo()
    {
        Fetch(OptionFirst + 14);
        Fetch(TipsFirst + 3);
        Cursor(4);

        Assert.Equal("Tip 3", Details(4));
    }

    [Fact]
    public void ARowNameOrValueWordIsNotAHelpLine()
    {
        Fetch(OptionFirst + 14);
        Fetch(OptionFirst + 45);
        Cursor(0);

        Assert.Null(Details(0));
    }

    [Fact]
    public void TheLastHelpLineBeforeTheSettleIsTheRowsEvenAcrossAMoveRecord()
    {
        Fetch(OptionFirst + 29);
        Cursor(1, Move);
        Fetch(OptionFirst + 31);
        Cursor(1);

        Assert.Equal("Option 31", Details(1));
    }

    [Fact]
    public void EachRowKeepsItsOwnLine()
    {
        Fetch(OptionFirst + 29);
        Cursor(0);
        Fetch(OptionFirst + 31);
        Cursor(1);

        Assert.Equal("Option 29", Details(0));
        Assert.Equal("Option 31", Details(1));
    }

    [Fact]
    public void ARowReachedWithNoFetchKeepsWhatItHadAndAnUnreachedRowSaysNone()
    {
        Fetch(OptionFirst + 29);
        Cursor(0);
        Cursor(0);
        Cursor(3);

        Assert.Equal("Option 29", Details(0));
        Assert.Null(Details(3));
    }

    [Fact]
    public void ALineFetchedForAnotherRowIsNotCarriedOverToTheNext()
    {
        Fetch(OptionFirst + 29);
        Cursor(0);
        Cursor(1);

        Assert.Null(Details(1));
    }

    [Fact]
    public void APageWithAnotherRowCountHasItsOwnLines()
    {
        Fetch(OptionFirst + 29);
        Cursor(0, count: 20);

        Assert.Null(Details(0, count: 24));
    }

    [Fact]
    public void TheOkButtonHasNoHelpLine()
    {
        Fetch(OptionFirst + 29);
        Cursor(0, flags: 0x43);

        Assert.Null(Details(0, offList: true));
        Assert.Null(Details(0));
    }

    [Fact]
    public void AnotherScreensLinesAreNotKept()
    {
        Fetch(OptionFirst + 29);
        Cursor(0, screenClass: "ItemMenuChoice@ope@ui@app");

        Assert.Null(Details(0, screenClass: "ItemMenuChoice@ope@ui@app"));
        Assert.Null(Details(0));
    }

    [Fact]
    public void TheDetailsAreSilentWhileTheSettingIsOff()
    {
        Fetch(OptionFirst + 29);
        Cursor(0);
        on = false;

        Assert.Null(Details(0));
    }
}
