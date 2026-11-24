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
    private readonly SaveSlotFocus focus;
    private readonly Dictionary<int, SaveSlotHeader> headers = [];
    private readonly List<ScreenEvent> focused = [];
    private bool detailsOn;

    public SaveSlotFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        slots = new SlotScreens(screens);
        cursors.Observe(slots);
        focus = new SaveSlotFocus(
            library, announcer, new TextCleaner(TextCleanerRules.None), new Headers(headers), () => detailsOn, debug.Add);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
        screens.Observe(new Recorder(focused));
        messages.Compiled(new MessageFileCompiled(SystemFile, 6,
            "//--------\n@System_Save_11\n{\nAuto <COLOR 3>record\n<WAIT>\n}"));
        messages.Compiled(new MessageFileCompiled(GeneralFile, 3,
            "//--------\n@System_General_Week_02\n{\nWet Day\n}\n//--------\n@System_General_Time_07\n{\nVery Late\n}"));
        // Made-up headers in the pinned format: file 4 on day 1, weekday 1, time 6, 643 seconds.
        headers[4] = new SaveSlotHeader(DayCount: 1, Weekday: 1, TimeOfDay: 6, PlaytimeSeconds: 643);
        headers[0] = new SaveSlotHeader(DayCount: 124, Weekday: 4, TimeOfDay: 3, PlaytimeSeconds: 170330);
    }

    private const string GeneralFile = "EN/message/system/System_General.msg";

    private sealed class Headers(Dictionary<int, SaveSlotHeader> files) : ISaveSlotHeaders
    {
        public SaveSlotHeader? Read(int slot) => files.GetValueOrDefault(slot);
    }

    private sealed class Recorder(List<ScreenEvent> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen.Add(change);
    }

    [Fact]
    public void WithTheDetailsOnASlotAlsoSaysItsDateAndPlaytime()
    {
        detailsOn = true;
        slots.Observe(new SerialResolved(LoadPrompt, 0));
        Cursor(4, 17);
        Cursor(0, 17);
        Cursor(5, 17);

        Assert.Equal(
            ["Slot 4, month 6, day 2, 10 minutes", "Auto record, month 10, day 5, 47 hours 18 minutes", "Slot 5"],
            Said());
        Assert.Equal("slot screen=load index=5 said=\"Slot 5\" unread=place,level,header (#622)", debug[^1]);
    }

    [Fact]
    public void WithTheDetailsOffNoSaveFileIsRead()
    {
        var reads = new CountingHeaders();
        var off = new SaveSlotFocus(
            new MessageLibrary(), announcer, new TextCleaner(TextCleanerRules.None), reads, () => false);
        var slotFour = new ScreenEvent(ScreenChange.FocusMoved, SaveSlotFocus.Load, SlotScreens.LoadClass, 4, Count: 17);
        off.Observe(slotFour);

        Assert.Equal(["Slot 4"], Said());
        Assert.Null(off.Details(slotFour));
        Assert.Equal(0, reads.Count);
    }

    private sealed class CountingHeaders : ISaveSlotHeaders
    {
        public int Count { get; private set; }

        public SaveSlotHeader? Read(int slot)
        {
            Count++;
            return null;
        }
    }

    [Fact]
    public void TheSaveListsRowsAreFilesOneOnSoItsFourthRowIsFileFour()
    {
        detailsOn = true;
        slots.Observe(new SerialResolved(SavePrompt, 0));
        Cursor(3, 16);

        Assert.Equal(["Slot 4, month 6, day 2, 10 minutes"], Said());
    }

    [Fact]
    public void TheDetailsKeyReadsTheWeekdayDateTimeOfDayAndPlaytime()
    {
        detailsOn = true;
        slots.Observe(new SerialResolved(LoadPrompt, 0));
        Cursor(4, 17);

        Assert.Equal("Wet Day, month 6, day 2, Very Late, played 10 minutes", focus.Details(focused[^1]));
    }

    [Fact]
    public void TheDetailsLeaveOutANameTheMessagesDoNotHold()
    {
        detailsOn = true;
        slots.Observe(new SerialResolved(LoadPrompt, 0));
        Cursor(0, 17);

        Assert.Equal("month 10, day 5, played 47 hours 18 minutes", focus.Details(focused[^1]));
    }

    [Fact]
    public void TheDetailsKeySaysNothingForASlotWithNoHeaderOrWithTheDetailsOff()
    {
        slots.Observe(new SerialResolved(LoadPrompt, 0));
        Cursor(4, 17);
        Assert.Null(focus.Details(focused[^1]));

        detailsOn = true;
        Cursor(5, 17);
        Assert.Null(focus.Details(focused[^1]));
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

        Assert.Equal("slot screen=save index=2 said=\"Slot 3\" unread=place,level,header (#622)", Assert.Single(debug));
    }

    [Fact]
    public void WithoutThePromptTheListIsAnUnknownScreenAgain()
    {
        Cursor(1, 17);

        Assert.Empty(Said());
    }
}
