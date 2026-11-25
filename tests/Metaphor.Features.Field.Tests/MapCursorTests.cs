using Metaphor.Core.Announcements;
using Metaphor.Core.Input;

namespace Metaphor.Features.Field.Tests;

/// <summary>The map cursor with bookmarks (#877, the maintainer's decision of 2026-11-24): hold V (or LB with the right
/// stick) and move a cursor over the map in 5-step moves, hearing what's there; let go and it is back at the player. B
/// bookmarks its spot, K and J jump to the map's bookmarks, G guides to it. Made-up maps and positions; the camera looks
/// along +Z, so +X is the screen's left.</summary>
[Trait("Category", "Unit")]
public sealed class MapCursorTests
{
    private const int KeyV = 0x56;
    private const int KeyB = 0x42;
    private const int KeyG = 0x47;
    private const int KeyK = 0x4B;
    private const int KeyJ = 0x4A;
    private const int Up = 0x26;
    private const int Left = 0x25;
    private const int Right = 0x27;
    private const int Down = 0x28;
    private const int LB = 0x0400;
    private const int R3 = 0x0004;
    private const int DpadRight = 0x0020;

    private readonly CursorField field = new();
    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> saved = [];
    private readonly List<BeaconTarget> guided = [];
    private readonly MapBookmarks bookmarks;
    private bool on = true;
    private bool guidance = true;
    private IReadOnlyList<(float X, float Z, bool Objective)> marks = [];
    private readonly MapCursor cursor;

    public MapCursorTests()
    {
        field.View = new FieldView(0, 0, 0, 0, [new(FieldKind.Door, 0, 0, 500)], MapId: 7, CameraHeading: 0);
        bookmarks = new MapBookmarks(null, saved.Add);
        // Open floor within 1000 units of the player across X, wall beyond.
        var edges = new NavEdges((_, _, _) => null, _ => null, point => Math.Abs(point.X) < 1000);
        cursor = new MapCursor(
            field, announcer, () => on, () => CursorBindings.Default, () => FieldBindings.Default, bookmarks,
            map => map == 7 ? edges : null, (_, _) => marks,
            target =>
            {
                guided.Add(target);
                return guidance;
            });
    }

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    // Holds V and presses each key in turn, letting each go before the next.
    private IReadOnlyList<int> Hold(params int[] keys)
    {
        var handed = cursor.Keys([KeyV]);
        foreach (var key in keys)
        {
            handed = cursor.Keys([KeyV, key]);
            cursor.Keys([KeyV]);
        }
        return handed;
    }

    private static PadSticks RightStick(double x, double y) =>
        PadSticks.Rest with { RightX = PadSticks.Centre + (int)(x * 127), RightY = PadSticks.Centre - (int)(y * 127) };

    [Fact]
    public void TheDefaultKeysAreVAndBClearOfTheModsFieldKeys()
    {
        // The field's N, L, G, K, J, I, O, U, H and brackets, and the dialogue's R, P, O, U and T.
        int[] taken = [0x4E, 0x4C, 0x47, 0x4B, 0x4A, 0x49, 0x4F, 0x55, 0x48, 0xDB, 0xDD, 0x52, 0x50, 0x54];
        var keys = CursorBindings.Default;

        Assert.Equal((KeyV, KeyB, R3), (keys.CursorKey, keys.BookmarkKey, keys.BookmarkButton));
        Assert.DoesNotContain(keys.CursorKey, taken);
        Assert.DoesNotContain(keys.BookmarkKey, taken);
    }

    [Fact]
    public void HoldingVStartsAtThePlayerAndEachArrowMovesFiveStepsSayingWhatIsThere()
    {
        var handed = Hold(Up, Left);

        Assert.Empty(handed);
        Assert.Equal(
            ["Map cursor", "Door, open, 5 steps ahead", "Open, 7 steps ahead-left"],
            Said());
    }

    [Fact]
    public void TheWalkingMapSaysWall()
    {
        Hold(Left, Left);

        Assert.Equal("Wall, 10 steps left", Said()[^1]);
    }

    [Fact]
    public void AnIconPointAtTheCursorIsSaid()
    {
        marks = [(0, -500, true), (-500, 0, false)];

        Hold(Down, Up, Right);

        Assert.Equal(["Map cursor", "Objective marker, open, 5 steps behind", "Open, at you", "Map icon, open, 5 steps right"], Said());
    }

    [Fact]
    public void LettingGoReturnsItToThePlayerAndHandsTheKeysBack()
    {
        Hold(Up);
        Assert.Equal([KeyB], cursor.Keys([KeyB]));

        Hold(Right);

        Assert.Equal("Open, 5 steps right", Said()[^1]);
    }

    [Fact]
    public void BBookmarksTheCursorsSpotOnThisMap()
    {
        Hold(Up, KeyB, Down, KeyB);

        Assert.Equal(["Bookmark 1 saved", "Bookmark 2 saved"], Said().Where(line => line.EndsWith("saved", StringComparison.Ordinal)));
        Assert.Equal([new MapBookmark(1, 0, 0, 500), new MapBookmark(2, 0, 0, 0)], bookmarks.On(7));
        Assert.Equal(2, saved.Count);
        Assert.Contains("\"7\"", saved[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void KAndJJumpToTheMapsBookmarksAndTheCursorSaysThem()
    {
        bookmarks.Add(7, 0, 0, 500);
        bookmarks.Add(7, -500, 0, 0);
        bookmarks.Add(8, 0, 0, 0);

        Hold(KeyK, KeyK, KeyK, KeyJ);

        Assert.Equal(
            [
                "Map cursor", "Bookmark 1 of 2, Bookmark 1, Door, open, 5 steps ahead",
                "Bookmark 2 of 2, Bookmark 2, open, 5 steps right", "Bookmark 1 of 2, Bookmark 1, Door, open, 5 steps ahead",
                "Bookmark 2 of 2, Bookmark 2, open, 5 steps right",
            ],
            Said());
    }

    [Fact]
    public void WithNoBookmarksKSaysSo()
    {
        Hold(KeyK);

        Assert.Equal("No bookmarks on this map", Said()[^1]);
    }

    [Fact]
    public void GGuidesToTheCursorOrToTheBookmarkItIsOn()
    {
        bookmarks.Add(7, -500, 0, 0);

        Hold(Up, KeyG, KeyK, KeyG);

        Assert.Equal(2, guided.Count);
        Assert.Equal(("the map cursor", 0f, 500f), (guided[0].Unit.Name, guided[0].Unit.X, guided[0].Unit.Z));
        Assert.Equal(("bookmark 1", -500f, 0f), (guided[1].Unit.Name, guided[1].Unit.X, guided[1].Unit.Z));
    }

    [Fact]
    public void GAtThePlayerOrWithGuidanceOffSaysWhy()
    {
        Hold(KeyG);
        guidance = false;
        Hold(Up, KeyG);

        Assert.Contains("The cursor is at you", Said());
        Assert.Equal("Guidance is off", Said()[^1]);
    }

    [Fact]
    public void OffOrOutsideFreeRoamVReachesTheGame()
    {
        on = false;
        Assert.Equal([KeyV, Up], cursor.Keys([KeyV, Up]));
        cursor.Keys([]);
        on = true;
        field.Roaming = false;
        Assert.Equal([KeyV, Up], cursor.Keys([KeyV, Up]));

        Assert.Empty(Said());
    }

    [Fact]
    public void LbWithTheRightStickMovesTheCursorOncePerPushAndKeepsTheCameraStill()
    {
        var first = cursor.Sticks(0, LB, RightStick(0, 1));
        cursor.Sticks(0, LB, RightStick(0, 1));
        cursor.Sticks(0, LB, RightStick(0, 0));
        cursor.Sticks(0, LB, RightStick(-1, 0));

        Assert.Equal(PadSticks.Rest, first);
        Assert.Equal(["Map cursor", "Door, open, 5 steps ahead", "Open, 7 steps ahead-left"], Said());
    }

    [Fact]
    public void WithTheCursorOutLbWithR3BookmarksAndWithDpadRightGuidesHidingThem()
    {
        cursor.Sticks(0, LB, RightStick(0, 1));

        Assert.Equal(LB, cursor.Pad(0, LB | R3, made: true));
        Assert.Equal(LB, cursor.Pad(0, LB, made: true));
        Assert.Equal(LB, cursor.Pad(0, LB | DpadRight, made: true));

        Assert.Single(bookmarks.On(7));
        Assert.Equal("bookmark 1", guided.Single().Unit.Name);
    }

    [Fact]
    public void LettingGoOfLbEndsThePadCursorAndTheDpadReachesTheListAgain()
    {
        cursor.Sticks(0, LB, RightStick(0, 1));
        cursor.Pad(0, 0, made: true);
        cursor.Sticks(0, 0, RightStick(0, 0));

        Assert.Equal(LB | DpadRight, cursor.Pad(0, LB | DpadRight, made: true));
        Assert.Equal(RightStick(0, 1), cursor.Sticks(0, 0, RightStick(0, 1)));
        Assert.Empty(guided);
    }

    private sealed class CursorField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldView? View { get; set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => View;

        public int? MapId() => View?.MapId;

        public FieldPose? ReadPose() => View?.Pose;
    }
}
