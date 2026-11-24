using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Battle's skill and item lists (#247): each row the cursor settles on is said by the name the list fetched
/// for it by serial, in the order it fetched them, and "n of total", while the menus read closed. Hook events in,
/// announcements out; made-up file text and serials only.</summary>
[Trait("Category", "Unit")]
public sealed class BattleListFocusTests
{
    private const string SkillClass = "BTL_SkillSelect@ope@ui@app";
    private const string ItemClass = "BTL_ItemSelect@ope@ui@app";
    private const string DifficultyClass = "UUIC_Common_DifficultySelect@ui@app";
    private const int Settle = 0x1;
    private const int SkillFirst = 700;
    private const int ItemFirst = 300;
    // The item names file: six named rows, then "Herb 7" to "Herb 40", for a list as long as the game's 40-item tab.
    private const int ItemCount = 40;
    private const int DescriptionFlags = BattleListFocus.DescriptionFlags;
    private const int SkillDescriptionFirst = 900;
    private const int ItemDescriptionFirst = 400;
    private const long Later = BattleListFocus.RunGapMilliseconds + 1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors;
    private readonly MenuScreens screens = new(KnownScreens.All);
    private readonly BattleListFocus skills;
    private readonly BattleListFocus items;
    private readonly BattleListFocus synthesis;
    // Each skill row's cost by its name serial (#692 (b)): none unless a test adds one.
    private readonly Dictionary<int, string> costs = [];
    private long now = 50_000;
    private bool viewsOn = true;
    private bool synthesisOn = true;

    public BattleListFocusTests()
    {
        cursors = new CursorEvents(() => now);
        var serials = new MessageSerials([
            new SerialRange(MessageSerials.SkillNames, SkillFirst, 3),
            new SerialRange(MessageSerials.ItemNames, ItemFirst, ItemCount),
            new SerialRange(MessageSerials.SkillDescriptions, SkillDescriptionFirst, 2),
            new SerialRange(MessageSerials.ItemDescriptions, ItemDescriptionFirst, 2),
        ]);
        var cleaner = new TextCleaner(TextCleanerRules.None);
        messages.ObserveFiles(serials);
        // A battle: the menus read closed.
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        skills = BattleListFocus.Skills(serials, announcer, cleaner, () => now, rowCost: costs.GetValueOrDefault);
        items = BattleListFocus.Items(serials, announcer, cleaner, () => now, () => viewsOn);
        synthesis = BattleListFocus.Synthesis(
            serials, announcer, cleaner, () => now, viewsOn: null, name => costs.GetValueOrDefault(name) is { } cost
                ? $"1 turn icon, {cost}"
                : null, () => synthesisOn);
        screens.Observe(skills);
        screens.Observe(items);
        screens.Observe(synthesis);
        screens.Observe(new UnknownScreenFocus(announcer));
        messages.ObserveSerials(skills);
        messages.ObserveSerials(items);
        messages.ObserveSerials(synthesis);
        messages.Compiled(new MessageFileCompiled("EN/message/system/Skill_Message_1.msg", 20,
            Source("Skill", "Spark", "Gust", "Frost <COLOR 3>")));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_1.msg", 21,
            Source("Item", [
                "Tonic", "Salve", "Ether", "Elixir", "Remedy", "Antidote",
                .. Enumerable.Range(7, ItemCount - 6).Select(n => $"Herb {n}"),
            ])));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Skill_Message_2.msg", 22,
            Source("SkillHelp", "Burns one foe", "Cuts all foes")));
        messages.Compiled(new MessageFileCompiled("EN/message/system/Item_Message_2.msg", 23,
            Source("ItemText", "Heals a little", "Heals more")));
    }

    private void Described(params int[] serials)
    {
        foreach (var serial in serials)
        {
            messages.Resolved(new SerialResolved(serial, DescriptionFlags));
        }
    }

    private static string Source(string prefix, params string[] texts) => string.Join("\n\n",
        texts.Select((text, at) => $"//--------\n@{prefix}_{at:000}\n{{\n{text}\n<WAIT>\n}}"));

    private void Cursor(string screenClass, int index, int? count, long manager = 0xa0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, Settle, Result: 1, Top: index, Row: 0, Flags: 0x46,
            screenClass, count));

    private void Fetched(params int[] serials)
    {
        foreach (var serial in serials)
        {
            messages.Resolved(new SerialResolved(serial, BattleListFocus.NameFlags));
        }
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheSkillListIsHeadedThenEachRowIsSaidByTheNameFetchedForIt()
    {
        // The list fetches its rows' names as it opens, here not in file order.
        Fetched(SkillFirst + 2, SkillFirst, SkillFirst + 1);

        Cursor(SkillClass, 0, count: 3);
        Cursor(SkillClass, 1, count: 3);
        Cursor(SkillClass, 2, count: 3);

        Assert.Equal(["Skills, Frost, 1 of 3", "Spark, 2 of 3", "Gust, 3 of 3"], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheItemListIsHeadedItsOwnWay()
    {
        Fetched(ItemFirst + 1);

        Cursor(ItemClass, 0, count: 1);

        Assert.Equal(["Items, Salve, 1 of 1"], Said());
    }

    [Fact]
    public void AnUnreadableCountLeavesThePositionAlone()
    {
        Fetched(SkillFirst);

        Cursor(SkillClass, 0, count: null);

        Assert.Equal(["Skills, Spark, 1"], Said());
    }

    // The cursor can settle before the row's name is fetched: the row is said once it is.
    [Fact]
    public void ARowWhoseNameIsNotFetchedYetIsSaidWhenItIs()
    {
        Cursor(SkillClass, 0, count: 2);
        Assert.Empty(Said());

        Fetched(SkillFirst + 1);

        Assert.Equal(["Skills, Gust, 1 of 2"], Said());
    }

    [Fact]
    public void ARowIsSaidOnceEvenWhenItsNameIsFetchedAgain()
    {
        Cursor(SkillClass, 0, count: 2);
        Fetched(SkillFirst, SkillFirst, SkillFirst + 1);

        Assert.Equal(["Skills, Spark, 1 of 2"], Said());
    }

    // Past the first page a row whose name wasn't fetched before its cursor settled is said by its position, never
    // left silent: a name fetched after it can't be told from the next move's.
    [Fact]
    public void ARowPastTheFirstPageWithNoNameFetchedIsSaidByItsPosition()
    {
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 2);
        now += Later;
        Cursor(SkillClass, 1, count: 2);
        Fetched(SkillFirst + 2);

        Assert.Equal(["Skills, Spark, 1 of 2", "2 of 2"], Said());
    }

    // A move, then the names it fetched, then where the cursor settled, as the game hands them: top is the row within
    // the view and row the rows scrolled (docs/raw-sources/battle-item-list-scroll-by-ear-2026-11-22.md).
    private void Walked(int command, int inView, int scrolled, int count, params int[] fetched) =>
        WalkedAfter(Later, command, inView, scrolled, count, fetched);

    private void WalkedAfter(long milliseconds, int command, int inView, int scrolled, int count, params int[] fetched)
    {
        now += milliseconds;
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, command, Result: command, Top: inView, Row: scrolled,
            Flags: 0x46, ItemClass, count));
        Fetched(fetched);
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Settle, Result: 1, Top: inView, Row: scrolled,
            Flags: 0x46, ItemClass, count));
    }

    // The by-ear run of 2026-11-22 (PR #646): past the first page some rows were silent, moving back up brought speech
    // back, then a row was said by another's name. Each scroll fetches the row it brings in, last going down, with
    // some of the rows it redraws, each move well apart; a three-row view stands in for the game's eight.
    [Fact]
    public void RowsKeepTheirOwnNamesAsTheListScrolls()
    {
        const int down = 0x14;
        const int up = 0x15;
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(down, 1, 0, 6);
        Walked(down, 2, 0, 6, ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Walked(down, 2, 1, 6, ItemFirst + 3);
        Walked(down, 2, 2, 6, ItemFirst + 2, ItemFirst + 4);
        Walked(down, 2, 3, 6, ItemFirst + 5);
        Walked(up, 1, 3, 6, ItemFirst + 3);
        Walked(up, 0, 3, 6, ItemFirst + 4, ItemFirst + 5);
        Walked(up, 0, 2, 6, ItemFirst + 2, ItemFirst + 3, ItemFirst + 4);

        Assert.Equal([
            "Items, Tonic, 1 of 6", "Salve, 2 of 6", "Ether, 3 of 6", "Elixir, 4 of 6", "Remedy, 5 of 6",
            "Antidote, 6 of 6", "Remedy, 5 of 6", "Elixir, 4 of 6", "Ether, 3 of 6",
        ], Said());
    }

    // Wrapped to the bottom, the rows there aren't known: each is said by its position until one scrolls in, the first
    // name fetched going up, as on the settings pages (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md).
    [Fact]
    public void ARowScrolledInGoingUpIsTheOnlyNameFetched()
    {
        const int up = 0x15;
        Fetched(ItemFirst, ItemFirst + 1);
        Cursor(ItemClass, 0, count: 6);
        Walked(up, 1, 4, 6);
        Walked(up, 0, 4, 6);
        Walked(up, 0, 3, 6, ItemFirst + 3);
        Walked(up, 0, 2, 6, ItemFirst + 2);

        Assert.Equal(["Items, Tonic, 1 of 6", "6 of 6", "5 of 6", "Elixir, 4 of 6", "Ether, 3 of 6"], Said());
    }

    // #737, by ear 2026-11-22 (build 548ddc95, Steam 18330018; [NEEDS VERIFICATION] beyond those runs): a wrap up to the
    // last row fetches the whole view again, as many names as the first page, in list order and ending at the last row,
    // and each move up after it fetches the same view. Those are the bottom rows, so each is named.
    [Fact]
    public void AfterAWrapUpTheBottomRowsAreNamedFromTheViewFetched()
    {
        const int up = 0x15;
        // A three-row view of a six-row list.
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(up, 2, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);
        Walked(up, 1, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);
        Walked(up, 0, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);

        Assert.Equal(["Items, Tonic, 1 of 6", "Antidote, 6 of 6", "Remedy, 5 of 6", "Elixir, 4 of 6"], Said());
    }

    // The 14-item tab of the by-ear session of 2026-11-22 (F8 marks 18 to 20), its eight-row view as the game fetched it:
    // after the wrap the game fetched rows 7 to 14, two of them already known, and the same eight on the next move up.
    [Fact]
    public void TheFourteenItemTabNamesItsRowsAfterAWrap()
    {
        const int up = 0x15;
        const int down = 0x14;
        int[] page = [.. Enumerable.Range(ItemFirst, 8)];
        int[] bottom = [.. Enumerable.Range(ItemFirst + 6, 8)];
        Fetched(page);
        Cursor(ItemClass, 0, count: 14);
        Walked(down, 1, 0, 14);
        Walked(down, 2, 0, 14, page);
        Walked(up, 1, 0, 14);
        Walked(up, 0, 0, 14);
        Walked(up, 7, 6, 14, bottom);
        Walked(up, 6, 6, 14, bottom);

        Assert.Equal([
            "Items, Tonic, 1 of 14", "Salve, 2 of 14", "Ether, 3 of 14", "Salve, 2 of 14", "Tonic, 1 of 14",
            "Herb 14, 14 of 14", "Herb 13, 13 of 14",
        ], Said());
    }

    // The 40-item tab of #737's run: the wrap fetched rows 33 to 40, none known, then the same eight going up.
    [Fact]
    public void TheFortyItemTabNamesItsRowsAfterAWrap()
    {
        const int up = 0x15;
        int[] bottom = [.. Enumerable.Range(ItemFirst + 32, 8)];
        Fetched([.. Enumerable.Range(ItemFirst, 8)]);
        Cursor(ItemClass, 0, count: 40);
        Walked(up, 7, 32, 40, bottom);
        Walked(up, 6, 32, 40, bottom);
        Walked(up, 5, 32, 40, bottom);

        Assert.Equal(["Items, Tonic, 1 of 40", "Herb 40, 40 of 40", "Herb 39, 39 of 40", "Herb 38, 38 of 40"], Said());
    }

    // #737, the Magic items tab of 2026-11-23 (build 0c122d3, docs/raw-sources/battle-item-tabs-wrap-log-2026-11-23.md):
    // switched to, then wrapped up within the run gap of its first page. The wrap's view is a fetch of its own, not
    // more of the first page.
    [Fact]
    public void AWrapRightAfterATabSwitchNamesTheBottomRows()
    {
        const int up = 0x15;
        int[] bottom = [.. Enumerable.Range(ItemFirst + 32, 8)];
        Fetched(ItemFirst + 20, ItemFirst + 21, ItemFirst + 22);
        Cursor(ItemClass, 0, count: 3);
        Walked(0x14, 1, 0, 3);
        Walked(0x16, 0, 0, 40, [.. Enumerable.Range(ItemFirst, 8)]);
        WalkedAfter(BattleListFocus.RunGapMilliseconds, up, 7, 32, 40, bottom);
        Walked(up, 6, 32, 40, bottom);

        Assert.Equal(
            ["Items, Herb 21, 1 of 3", "Herb 22, 2 of 3", "Tonic, 1 of 40", "Herb 40, 40 of 40", "Herb 39, 39 of 40"],
            Said());
    }

    // #737, the HP recovery tab of 2026-11-23: switched to from the top row of another tab, so only the count changed
    // and no focus was reported; its first move, a wrap up, fetched only the three rows it brought into view.
    [Fact]
    public void AWrapOnATabSwitchedToWithNoFocusNamesItsRows()
    {
        const int up = 0x15;
        const int down = 0x14;
        int[] bottom = [ItemFirst + 28, ItemFirst + 29, ItemFirst + 30];
        Fetched([.. Enumerable.Range(ItemFirst + 10, 8)]);
        Cursor(ItemClass, 0, count: 9);
        Walked(0x16, 0, 0, 11, [.. Enumerable.Range(ItemFirst + 20, 8)]);
        Walked(up, 7, 3, 11, bottom);
        Walked(up, 6, 3, 11);
        Walked(down, 7, 3, 11, [.. Enumerable.Range(ItemFirst + 23, 5)]);
        Walked(up, 6, 3, 11, bottom);
        Walked(up, 5, 3, 11);
        Walked(up, 4, 3, 11);

        Assert.Equal([
            "Items, Herb 11, 1 of 9", "Herb 31, 11 of 11", "Herb 30, 10 of 11", "Herb 31, 11 of 11",
            "Herb 30, 10 of 11", "Herb 29, 9 of 11", "Herb 28, 8 of 11",
        ], Said());
    }

    // A view whose names don't line up with the rows known (one known row out of place) names nothing.
    [Fact]
    public void AViewFetchedOutOfLineWithTheKnownRowsNamesNoRow()
    {
        const int up = 0x15;
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(up, 2, 3, 6, ItemFirst + 1, ItemFirst + 4, ItemFirst + 5);

        Assert.Equal(["Items, Tonic, 1 of 6", "6 of 6"], Said());
    }

    // With the views setting off (#737's fix while it is tested) a wrap says the position alone, as before.
    [Fact]
    public void WithViewsOffAWrapSaysThePositionAlone()
    {
        viewsOn = false;
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(0x15, 2, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);

        Assert.Equal(["Items, Tonic, 1 of 6", "6 of 6"], Said());
    }

    [Fact]
    public void AfterAWrapUpMovingDownAndBackNeverSaysAWrongName()
    {
        const int up = 0x15;
        const int down = 0x14;
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(up, 2, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);
        Walked(up, 1, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);
        Walked(down, 2, 3, 6, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);
        // Wrapping down to the top fetches the first page afresh.
        Walked(down, 0, 0, 6, ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Walked(down, 1, 0, 6);

        Assert.Equal(
            ["Items, Tonic, 1 of 6", "Antidote, 6 of 6", "Remedy, 5 of 6", "Antidote, 6 of 6", "Tonic, 1 of 6",
                "Salve, 2 of 6"], Said());
    }

    // Another tab of the item list: after a gap it fetches its first page, then its cursor settles on the top row.
    [Fact]
    public void AnotherTabStartsTheRowsAfresh()
    {
        Fetched(ItemFirst, ItemFirst + 1, ItemFirst + 2);
        Cursor(ItemClass, 0, count: 6);
        Walked(0x14, 1, 0, 6);
        Walked(0x16, 0, 0, 3, ItemFirst + 3, ItemFirst + 4, ItemFirst + 5);

        Assert.Equal(["Items, Tonic, 1 of 6", "Salve, 2 of 6", "Elixir, 1 of 3"], Said());
    }

    // A description, another file's name, or a name fetched with other flags is no row.
    [Fact]
    public void OnlyTheListsOwnNamesFetchedWithTheNameFlagsAreRows()
    {
        messages.Resolved(new SerialResolved(SkillFirst, DescriptionFlags));
        Fetched(ItemFirst, SkillFirst + 2);

        Cursor(SkillClass, 0, count: 1);

        Assert.Equal(["Skills, Frost, 1 of 1"], Said());
    }

    // The next member's list fetches its names before its cursor settles, and its cursor is a new manager.
    [Fact]
    public void TheListOpenedAgainSaysTheNamesItFetchedNotTheLastVisits()
    {
        Fetched(SkillFirst, SkillFirst + 1);
        Cursor(SkillClass, 0, count: 2);
        now += 5_000;
        Fetched(SkillFirst + 2);
        Cursor(SkillClass, 0, count: 1, manager: 0xc0);

        Assert.Equal(["Skills, Spark, 1 of 2", "Skills, Frost, 1 of 1"], Said());
    }

    [Fact]
    public void TheListOpenedAgainWaitsForItsOwnNamesWhenItsCursorComesFirst()
    {
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 1);
        now += 5_000;
        Cursor(SkillClass, 0, count: 1, manager: 0xc0);
        Fetched(SkillFirst + 1);

        Assert.Equal(["Skills, Spark, 1 of 1", "Skills, Gust, 1 of 1"], Said());
    }

    [Fact]
    public void EnteringAnotherScreenForgetsTheRows()
    {
        Fetched(SkillFirst);
        Cursor(DifficultyClass, 0, count: 5, manager: 0xb0);
        Cursor(SkillClass, 0, count: 1, manager: 0xc0);

        Assert.DoesNotContain(Said(), said => said.Contains("Spark", StringComparison.Ordinal));
    }

    // A field menu fetches item names while it is current; they are no battle list's rows, and the list stops
    // listening then, so the hook can skip its reads.
    [Fact]
    public void NamesFetchedWhileAnotherScreenIsCurrentAreNoRows()
    {
        Cursor(DifficultyClass, 0, count: 5, manager: 0xb0);
        Assert.False(messages.SerialsWanted);
        Fetched(ItemFirst, ItemFirst + 1);
        screens.MenusRead(open: true);
        screens.MenusRead(open: false);
        Assert.True(messages.SerialsWanted);

        Cursor(ItemClass, 0, count: 2, manager: 0xc0);

        Assert.DoesNotContain(Said(), said => said.Contains("Tonic", StringComparison.Ordinal));
    }

    // Names fetched long before the list opened came from something else: the list waits for its own.
    [Fact]
    public void NamesFetchedLongBeforeTheListOpensAreNoRows()
    {
        Fetched(ItemFirst);
        now += 5_000;
        Cursor(ItemClass, 0, count: 1);
        Assert.Empty(Said());

        Fetched(ItemFirst + 1);

        Assert.Equal(["Items, Salve, 1 of 1"], Said());
    }

    // The reader turned back on enters the current screen again; its rows stay.
    [Fact]
    public void TheListEnteredAgainWhileOnItKeepsItsRows()
    {
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 1);
        now += 5_000;

        screens.RepeatCurrent();

        Assert.Equal(["Skills, Spark, 1 of 1", "Skills, Spark, 1 of 1"], Said());
    }

    // A row fetched from a file the game didn't compile, or compiled with another count, has no known text.
    [Fact]
    public void ARowWithNoKnownTextIsSaidByItsPosition()
    {
        var serials = new MessageSerials([new SerialRange(MessageSerials.SkillNames, SkillFirst, 3)]);
        var alone = new MenuScreens(KnownScreens.All);
        var spoken = new RecordingAnnouncer();
        var skills = BattleListFocus.Skills(serials, spoken, new TextCleaner(TextCleanerRules.None), () => now);
        alone.Observe(skills);
        skills.Observe(new SerialResolved(SkillFirst, BattleListFocus.NameFlags));

        alone.Observe(new CursorInput(0xa0, 0x100, Settle, 1, 0, 0, 0x46, SkillClass, Count: 3));

        Assert.Equal(["Skills, 1 of 3"], spoken.Said.Select(said => said.Text));
    }

    // The by-ear run of 2026-11-21: backing out of the skill list to battle's commands tells nothing (they are no
    // screen), so the skill list stays current until the item list's cursor settles, after the item list fetched its
    // names. Those names are the item list's rows all the same.
    [Fact]
    public void TheItemListOpenedAfterTheSkillListSaysItsFirstRow()
    {
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 1);
        now += 80_000;
        Fetched(ItemFirst);
        Described(ItemDescriptionFirst);
        Fetched(ItemFirst + 1);
        Described(ItemDescriptionFirst + 1);

        Cursor(ItemClass, 0, count: 2, manager: 0xc0);

        Assert.Equal(["Skills, Spark, 1 of 1", "Items, Tonic, 1 of 2"], Said());
        Assert.Equal("Heals a little", items.Details(Focus(ItemClass, 0)));
    }

    // The same run's second opening: the item list closed and opened again on the same manager, its cursor settling on
    // the same row with no other cursor between, after it fetched its names afresh.
    [Fact]
    public void TheListOpenedAgainOnTheSameManagerAndRowIsHeadedAgain()
    {
        Fetched(ItemFirst, ItemFirst + 1);
        Cursor(ItemClass, 0, count: 2);
        now += 16;
        Cursor(ItemClass, 0, count: 2);
        now += 30_000;
        Fetched(ItemFirst, ItemFirst + 1);

        Cursor(ItemClass, 0, count: 2);

        Assert.Equal(["Items, Tonic, 1 of 2", "Items, Tonic, 1 of 2"], Said());
    }

    [Fact]
    public void TheSkillListOpenedAfterTheItemListSaysItsFirstRow()
    {
        Fetched(ItemFirst);
        Cursor(ItemClass, 0, count: 1);
        now += 80_000;
        Fetched(SkillFirst + 1);

        Cursor(SkillClass, 0, count: 1, manager: 0xc0);

        Assert.Equal(["Items, Tonic, 1 of 1", "Skills, Gust, 1 of 1"], Said());
    }

    // The details key (#247): the list fetches each row's description right after its name as it opens (the item
    // list, by-ear run of 2026-11-21), so each row's details are the description fetched after its name.
    [Fact]
    public void TheDetailsAreTheFocusedRowsDescription()
    {
        Fetched(ItemFirst);
        Described(ItemDescriptionFirst);
        Fetched(ItemFirst + 1);
        Described(ItemDescriptionFirst + 1);
        Cursor(ItemClass, 0, count: 2);
        Assert.Equal("Heals a little", items.Details(Focus(ItemClass, 0)));

        Cursor(ItemClass, 1, count: 2);
        Assert.Equal("Heals more", items.Details(Focus(ItemClass, 1)));

        // The help line fetched again on a focus move, with no name before it, changes no row's.
        Cursor(ItemClass, 0, count: 2);
        Described(ItemDescriptionFirst + 1);
        Assert.Equal("Heals a little", items.Details(Focus(ItemClass, 0)));
    }

    [Fact]
    public void ARowWhoseDescriptionWasNotFetchedHasNoDetails()
    {
        Fetched(SkillFirst, SkillFirst + 1);
        Described(SkillDescriptionFirst);
        Cursor(SkillClass, 0, count: 2);

        Assert.Null(skills.Details(Focus(SkillClass, 0)));
        Assert.Equal("Burns one foe", skills.Details(Focus(SkillClass, 1)));
    }

    [Fact]
    public void TheListOpenedAgainForgetsTheLastVisitsDescriptions()
    {
        Fetched(SkillFirst);
        Described(SkillDescriptionFirst);
        Cursor(SkillClass, 0, count: 1);
        now += 5_000;
        Fetched(SkillFirst + 1);
        Cursor(SkillClass, 0, count: 1, manager: 0xc0);

        Assert.Null(skills.Details(Focus(SkillClass, 0)));
    }

    [Fact]
    public void ThereAreNoDetailsOffTheListOrBeforeADescription()
    {
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 1);
        Assert.Null(skills.Details(Focus(SkillClass, 0)));

        Fetched(SkillFirst);
        Described(SkillDescriptionFirst);
        Assert.Equal("Burns one foe", skills.Details(Focus(SkillClass, 0)));
        Cursor(DifficultyClass, 0, count: 5, manager: 0xb0);

        Assert.Null(skills.Details(Focus(SkillClass, 0)));
    }

    private static ScreenEvent Focus(string screenClass, int index) =>
        new(ScreenChange.FocusMoved, KnownScreens.All.First(screen => screen.ScreenClass == screenClass), screenClass, index);

    [Fact]
    public void BothListsAreKnownScreensShownOutsideTheMenus()
    {
        Assert.Contains(BattleListFocus.SkillList, KnownScreens.All);
        Assert.Contains(BattleListFocus.ItemList, KnownScreens.All);
        Assert.True(BattleListFocus.SkillList.OutsideMenus);
        Assert.True(BattleListFocus.ItemList.OutsideMenus);
    }

    private const string SynthesisClass = "BTL_SyentesisSkillList@ope@ui@app";

    [Fact]
    public void ASkillRowSaysItsCostAfterItsName()
    {
        costs[SkillFirst] = "4 MP";
        Fetched(SkillFirst, SkillFirst + 1);

        Cursor(SkillClass, 0, count: 2);
        Cursor(SkillClass, 1, count: 2);

        Assert.Equal(["Skills, Spark, 4 MP, 1 of 2", "Gust, 2 of 2"], Said());
    }

    [Fact]
    public void ARowSaidByItsPositionHasNoCost()
    {
        costs[SkillFirst] = "4 MP";
        Fetched(SkillFirst);
        Cursor(SkillClass, 0, count: 3);
        // A jump with no name fetched: the row can't be told.
        Cursor(SkillClass, 2, count: 3);

        Assert.Equal(["Skills, Spark, 4 MP, 1 of 3", "3 of 3"], Said());
    }

    [Fact]
    public void TheSynthesisListIsHeadedAndNamesItsRowsWithTheirCostsAndDescriptions()
    {
        // #260: four names (flags 9) and descriptions (flags 7), one pair per row, before its cursor.
        costs[SkillFirst + 1] = "12 MP";
        Fetched(SkillFirst + 1);
        Described(SkillDescriptionFirst);
        Fetched(SkillFirst + 2);
        Described(SkillDescriptionFirst + 1);

        Cursor(SynthesisClass, 0, count: 2, manager: 0xd0);
        Cursor(SynthesisClass, 1, count: 2, manager: 0xd0);

        Assert.Equal(["Synthesis, Gust, 1 turn icon, 12 MP, 1 of 2", "Frost, 2 of 2"], Said());
        Assert.Equal("Burns one foe", synthesis.Details(Focus(SynthesisClass, 0)));
        Assert.Null(skills.Details(Focus(SynthesisClass, 0)));
    }

    [Fact]
    public void TheSynthesisListIsSilentWithItsSettingOff()
    {
        synthesisOn = false;
        Fetched(SkillFirst);
        Described(SkillDescriptionFirst);

        Cursor(SynthesisClass, 0, count: 1, manager: 0xd0);

        Assert.Empty(Said());
        Assert.Null(synthesis.Details(Focus(SynthesisClass, 0)));
    }

    [Fact]
    public void TheSkillListOpenedAfterTheSynthesisListNamesItsOwnRows()
    {
        // Backing out of Synthesis to the commands tells nothing; the skill list's own fetches are still heard.
        Fetched(SkillFirst + 2);
        Cursor(SynthesisClass, 0, count: 1, manager: 0xd0);
        now += 80_000;
        Fetched(SkillFirst, SkillFirst + 1);

        Cursor(SkillClass, 0, count: 2);

        Assert.Equal(["Synthesis, Frost, 1 of 1", "Skills, Spark, 1 of 2"], Said());
    }

    [Fact]
    public void TheSynthesisListIsAKnownScreenShownOutsideTheMenus()
    {
        Assert.Contains(BattleListFocus.SynthesisList, KnownScreens.All);
        Assert.True(BattleListFocus.SynthesisList.OutsideMenus);
    }
}
