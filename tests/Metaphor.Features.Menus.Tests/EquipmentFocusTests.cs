using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The camp Equipment screen (#217): entering the character list says "Equipment" and the position, and each
/// move says its position, on the characters, the five slots, the slot's items and the swap's characters. Names, the
/// "equipped" mark and the sort key are not spoken yet: no finding gives their source ([NEEDS VERIFICATION], reported
/// on the issue). Hook events in, announcements out; made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class EquipmentFocusTests
{
    private const string CharactersClass = "EquipCharaSelChoice@ope@ui@app";
    private const string SlotsClass = "EquipPartsChoice@ope@ui@app";
    private const string ItemsClass = "EquipMenuItemList@ope@ui@app";
    private const string SwapClass = "EquipSwapCharaSelChoice@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public EquipmentFocusTests()
    {
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new EquipmentFocus(announcer));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheCharactersSaysEquipmentAndThePosition()
    {
        Cursor(CharactersClass, 0, count: 4);
        Cursor(CharactersClass, 1, count: 4);

        Assert.Equal(["Equipment, 1 of 4", "2 of 4"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheSlotsAreSaidByPositionOfFive()
    {
        Cursor(SlotsClass, 0, count: 5);
        Cursor(SlotsClass, 4, count: 5);

        Assert.Equal(["1 of 5", "5 of 5"], Said());
    }

    [Fact]
    public void ASlotsItemsAreSaidByPositionAndAnUnreadableCountByPositionAlone()
    {
        Cursor(ItemsClass, 2, count: 9);
        Cursor(ItemsClass, 3, count: null);

        Assert.Equal(["3 of 9", "4"], Said());
    }

    [Fact]
    public void TheSwapCharactersAreSaidByPosition()
    {
        Cursor(SwapClass, 1, count: 3);

        Assert.Equal(["2 of 3"], Said());
    }

    [Fact]
    public void ReturningToTheCharactersSaysEquipmentAgain()
    {
        Cursor(CharactersClass, 0, count: 4);
        Cursor(SlotsClass, 0, count: 5);
        Cursor(CharactersClass, 0, count: 4);

        Assert.Equal(["Equipment, 1 of 4", "1 of 5", "Equipment, 1 of 4"], Said());
    }

    [Fact]
    public void TheEquipmentScreensAreKnownScreens() =>
        Assert.Subset(KnownScreens.All.ToHashSet(), EquipmentFocus.Screens.ToHashSet());
}
