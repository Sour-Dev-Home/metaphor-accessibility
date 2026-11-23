using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The camp Item screen (#216): entering the item list says "Item" and the focused row's position, and each
/// move says the position, on the list and on the party-member target list. The rows' names, the tab names and the
/// target's names are not spoken yet: no finding gives their source ([NEEDS VERIFICATION], reported on the issue).
/// Hook events in, announcements out; made-up numbers only.</summary>
[Trait("Category", "Unit")]
public sealed class ItemFocusTests
{
    private const string ListClass = "ItemMenuChoice@ope@ui@app";
    private const string TargetClass = "ItemCharaSelChoice@ope@ui@app";
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public ItemFocusTests()
    {
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new ItemFocus(announcer));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(string screenClass, int index, int? count) =>
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x66,
            screenClass, count));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheListSaysItemAndTheFocusedRowsPosition()
    {
        Cursor(ListClass, 0, count: 12);

        Assert.Equal(["Item, 1 of 12"], Said());
    }

    [Fact]
    public void EachMoveSaysThePositionAndCutsOffTheLast()
    {
        Cursor(ListClass, 0, count: 12);
        Cursor(ListClass, 1, count: 12);
        Cursor(ListClass, 2, count: 12);

        Assert.Equal(["Item, 1 of 12", "2 of 12", "3 of 12"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void AListWhoseCountCannotBeReadIsSaidByPositionAlone()
    {
        Cursor(ListClass, 0, count: null);
        Cursor(ListClass, 1, count: null);

        Assert.Equal(["Item, 1", "2"], Said());
    }

    [Fact]
    public void TheTargetListSaysEachPartyMembersPositionWithoutTheScreenName()
    {
        Cursor(TargetClass, 0, count: 4);
        Cursor(TargetClass, 3, count: 4);

        Assert.Equal(["1 of 4", "4 of 4"], Said());
    }

    [Fact]
    public void ReturningToTheListFromTheTargetsSaysItemAgain()
    {
        Cursor(ListClass, 0, count: 5);
        Cursor(TargetClass, 0, count: 4);
        Cursor(ListClass, 0, count: 5);

        Assert.Equal(["Item, 1 of 5", "1 of 4", "Item, 1 of 5"], Said());
    }

    [Fact]
    public void BothItemScreensAreKnownScreens() =>
        Assert.Subset(KnownScreens.All.ToHashSet(), ItemFocus.Screens.ToHashSet());
}
