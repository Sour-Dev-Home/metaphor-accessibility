using Metaphor.Core.Menus;

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

    public SlotScreensTests() => slots = new SlotScreens(new Recording(passed));

    private static CursorInput Unnamed(long manager, int index, int? count) =>
        new(manager, ClassRva: 0x22045e8, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x46, ScreenClass: null,
            Count: count);

    [Fact]
    public void TheLoadPromptNamesTheNextUnnamedListOfSeventeenAndItsLaterInputs()
    {
        slots.Resolved(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        slots.Observe(Unnamed(0xa0, 1, 17));

        Assert.Equal([SlotScreens.LoadClass, SlotScreens.LoadClass], passed.Select(input => input.ScreenClass));
        Assert.Equal([0, 1], passed.Select(input => input.Index));
    }

    [Fact]
    public void TheSavePromptNamesTheNextUnnamedListOfSixteen()
    {
        slots.Resolved(SavePrompt);
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
        slots.Resolved(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 29));

        Assert.Null(Assert.Single(passed).ScreenClass);
    }

    [Fact]
    public void ANamedInputPassesUnchanged()
    {
        slots.Resolved(LoadPrompt);
        var titleMenu = Unnamed(0xc0, 1, 17) with { ScreenClass = "TitleChoice@ope@ui@app" };
        slots.Observe(titleMenu);

        Assert.Same(titleMenu, Assert.Single(passed));
    }

    [Fact]
    public void APromptNamesOneOpeningOnlySoTheNextManagerNeedsItsOwn()
    {
        slots.Resolved(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        slots.Observe(Unnamed(0xd0, 0, 17));

        Assert.Equal([SlotScreens.LoadClass, null], passed.Select(input => input.ScreenClass));
    }

    [Fact]
    public void ANewOpeningReplacesTheOldSlotList()
    {
        slots.Resolved(LoadPrompt);
        slots.Observe(Unnamed(0xa0, 0, 17));
        slots.Resolved(SavePrompt);
        slots.Observe(Unnamed(0xb0, 0, 16));
        slots.Observe(Unnamed(0xa0, 2, 17));

        Assert.Equal([SlotScreens.LoadClass, SlotScreens.SaveClass, null], passed.Select(input => input.ScreenClass));
    }

    [Fact]
    public void OtherSerialsChangeNothing()
    {
        slots.Resolved(1671);
        slots.Observe(Unnamed(0xa0, 0, 17));

        Assert.Null(Assert.Single(passed).ScreenClass);
    }

    private sealed class Recording(List<CursorInput> passed) : ICursorObserver
    {
        public void Observe(CursorInput input) => passed.Add(input);
    }
}
