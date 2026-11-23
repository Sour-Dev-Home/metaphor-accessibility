using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The save and load screens' slot lists (#622) have no choice owner, so their cursors come without a screen
/// class. Each opening fetches its prompt by serial just before its new manager's first cursor
/// (docs/findings/save-load-slot-screens.md), and from then on that manager's inputs carry the slot screen's name.
/// Serials and cursor inputs in, named inputs out.</summary>
[Trait("Category", "Unit")]
public sealed class SlotScreensTests
{
    private const int LoadPrompt = 1634;
    private const int SavePrompt = 1299;

    private readonly List<CursorInput> passed = [];
    private readonly SlotScreens slots;

    // Serials reach the slot screens through MessageEvents, as the serial hook hands them on (2026-11-22 review,
    // finding 5).
    private readonly MessageEvents events = new();

    public SlotScreensTests()
    {
        slots = new SlotScreens(new Recording(passed));
        events.ObserveSerials(slots);
    }

    private void Fetched(int serial) => events.Resolved(new SerialResolved(serial, Flags: 0));

    [Fact]
    public void TheSlotScreensKeepTheSerialHookReading()
    {
        Assert.True(events.SerialsWanted);
    }

    private static CursorInput Unnamed(long manager, int index, int? count) =>
        new(manager, ClassRva: 0x22045e8, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x46, ScreenClass: null,
            Count: count);

    [Fact]
    public void TheLoadPromptNamesTheNextUnnamedListOfSeventeenAndItsLaterInputs()
    {
        Fetched(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        slots.Observe(Unnamed(0xa0, 1, 17));

        Assert.Equal([SlotScreens.LoadClass, SlotScreens.LoadClass], passed.Select(input => input.ScreenClass));
        Assert.Equal([0, 1], passed.Select(input => input.Index));
    }

    [Fact]
    public void TheSavePromptNamesTheNextUnnamedListOfSixteen()
    {
        Fetched(SavePrompt);
        slots.Observe(Unnamed(0xb0, 3, 16));

        Assert.Equal(SlotScreens.SaveClass, Assert.Single(passed).ScreenClass);
    }

    [Fact]
    public void WithoutAPromptAnUnnamedListStaysUnnamed()
    {
        slots.Observe(Unnamed(0xa0, 0, 17));

        Assert.Null(Assert.Single(passed).ScreenClass);
    }

    [Fact]
    public void AListOfAnotherCountIsNotTakenForTheSlots()
    {
        Fetched(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 29));

        Assert.Null(Assert.Single(passed).ScreenClass);
    }

    [Fact]
    public void ANamedInputPassesUnchanged()
    {
        Fetched(LoadPrompt);
        var titleMenu = Unnamed(0xc0, 1, 5) with { ScreenClass = "TitleChoice@ope@ui@app" };
        slots.Observe(titleMenu);

        Assert.Same(titleMenu, Assert.Single(passed));
    }

    private const string Classed = "SingleChoiceBase@ope@ui@app";

    [Fact]
    public void AClassedListOfSeventeenAfterTheLoadPromptIsNamedAndItsManagerKeepsTheName()
    {
        Fetched(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 9, 17) with { ScreenClass = Classed });
        slots.Observe(Unnamed(0xa0, 8, 17) with { ScreenClass = Classed });

        Assert.Equal([SlotScreens.LoadClass, SlotScreens.LoadClass], passed.Select(input => input.ScreenClass));
    }

    [Fact]
    public void AClassedListOfSeventeenWithNoPromptStaysUnnamed()
    {
        slots.Observe(Unnamed(0xa0, 9, 17) with { ScreenClass = Classed });

        Assert.Equal(Classed, Assert.Single(passed).ScreenClass);
    }

    [Fact]
    public void APromptNamesOneOpeningOnlySoTheNextManagerNeedsItsOwn()
    {
        Fetched(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        slots.Observe(Unnamed(0xd0, 0, 17));

        Assert.Equal([SlotScreens.LoadClass, null], passed.Select(input => input.ScreenClass));
    }

    [Fact]
    public void ANewOpeningReplacesTheOldSlotList()
    {
        Fetched(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        Fetched(SavePrompt);
        slots.Observe(Unnamed(0xb0, 0, 16));
        slots.Observe(Unnamed(0xa0, 2, 17));

        Assert.Equal([SlotScreens.LoadClass, SlotScreens.SaveClass, null], passed.Select(input => input.ScreenClass));
    }

    [Fact]
    public void OtherSerialsChangeNothing()
    {
        Fetched(1671);
        slots.Observe(Unnamed(0xa0, 0, 17));

        Assert.Null(Assert.Single(passed).ScreenClass);
    }

    private sealed class Recording(List<CursorInput> passed) : ICursorObserver
    {
        public void Observe(CursorInput input) => passed.Add(input);
    }
}
