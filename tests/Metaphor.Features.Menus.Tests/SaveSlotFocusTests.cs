using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The save and load screens' slots (#622): each slot the cursor settles on is said as "Slot n", the load
/// list's first row as the system file's autosave word, with the list's 17 and 16 items as seen on game build 548ddc95
/// (docs/findings/save-load-slot-screens.md). Prompt serials and cursor inputs in, announcements and Debug lines out;
/// made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class SaveSlotFocusTests
{
    private const string SystemFile = "EN/message/system/System_Message.msg";
    private const int LoadPrompt = 1634;
    private const int SavePrompt = 1299;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();
    private readonly List<string> debug = [];
    private readonly SlotScreens slots;

    public SaveSlotFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        slots = new SlotScreens(screens);
        cursors.Observe(slots);
        screens.Observe(new SaveSlotFocus(library, announcer, new TextCleaner(TextCleanerRules.None), debug.Add));
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.Compiled(new MessageFileCompiled(SystemFile, 6,
            "//--------\n@System_Save_11\n{\nAuto <COLOR 3>record\n<WAIT>\n}"));
    }

    private void Cursor(int index, int count, long manager = 0xa0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x22045e8, Command: 0x1, Result: 1, Top: index, Row: 0,
            Flags: 0x46, ScreenClass: null, Count: count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheLoadListSaysItsAutosaveRowThenEachSlotByNumber()
    {
        slots.Observe(new SerialResolved(LoadPrompt, 0));
        Cursor(0, 17);
        Cursor(1, 17);
        Cursor(3, 17);
        Cursor(16, 17);

        Assert.Equal(["Auto record", "Slot 1", "Slot 3", "Slot 16"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheSaveListHasNoAutosaveRowSoItsFirstRowIsSlotOne()
    {
        slots.Observe(new SerialResolved(SavePrompt, 0));
        Cursor(3, 16);
        Cursor(0, 16);
        Cursor(15, 16);

        Assert.Equal(["Slot 4", "Slot 1", "Slot 16"], Said());
    }

    [Fact]
    public void EachFocusLogsWhatTheSlotDoesNotSayYet()
    {
        slots.Observe(new SerialResolved(SavePrompt, 0));
        Cursor(2, 16);

        Assert.Equal("slot screen=save index=2 said=\"Slot 3\" unread=place,playtime,empty (#622)", Assert.Single(debug));
    }

    [Fact]
    public void WithoutThePromptTheListIsAnUnknownScreenAgain()
    {
        Cursor(1, 17);

        Assert.Empty(Said());
    }
}
