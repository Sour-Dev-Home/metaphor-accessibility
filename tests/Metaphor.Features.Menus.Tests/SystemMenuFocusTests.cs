using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The in-game System submenu (#733): its six rows are named in cursor order from the System_Option labels
/// (serials 1676, 1677, 1674, 1673, 1675, 1678), and the screen is known, so the unknown-screen fallback stays quiet.
/// Hook events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class SystemMenuFocusTests
{
    private const string SystemClass = "SystemMenuChoice@ope@ui@app";
    private const int Settle = 0x1;
    private const int First = 1670;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public SystemMenuFocusTests()
    {
        var serials = new MessageSerials([new SerialRange("message/system/System_Message.msg", First, 12)]);
        var messages = new MessageEvents();
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 20,
            string.Join("\n\n", Enumerable.Range(0, 12).Select(at =>
                $"//--------\n@System_Option_{at:00}\n{{\nText {First + at}\n<WAIT>\n}}"))));
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new SystemMenuFocus(serials, announcer, new TextCleaner(TextCleanerRules.None)));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int index) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            SystemClass, Count: 6));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringSaysSystemAndTheFirstRowsName()
    {
        Cursor(0);

        Assert.Equal(["System, Text 1676, 1 of 6"], Said());
    }

    [Fact]
    public void EachRowIsNamedFromItsOwnLabel()
    {
        Cursor(0);
        Cursor(1);
        Cursor(2);
        Cursor(3);
        Cursor(4);
        Cursor(5);

        Assert.Equal(
            ["System, Text 1676, 1 of 6", "Text 1677, 2 of 6", "Text 1674, 3 of 6", "Text 1673, 4 of 6",
             "Text 1675, 5 of 6", "Text 1678, 6 of 6"],
            Said());
    }
}
