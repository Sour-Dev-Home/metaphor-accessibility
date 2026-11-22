using System.Numerics;
using Metaphor.Core.Navigation;

namespace Metaphor.Core.Tests.Navigation;

/// <summary>Paths over made-up tile sets (#690): A* around a wall, through a doorway, under a climb limit, none, and the
/// corners a path is cut to. 8 × 8 tiles of 10-unit cells from the origin, one-unit height steps and a climb of 2; no
/// game data.</summary>
[Trait("Category", "Unit")]
public sealed class PathFinderTests
{
    private const int Size = 8;

    private static NavBytes.Layer Open(int tx = 0) => NavBytes.Layer.Open(tx, 0, Size);

    private static WalkGraph Graph(params NavBytes.Layer[] layers) =>
        new(NavMeshFile.Parse(NavBytes.File(layers.Select(l => l.Entry()), tileCells: Size, climb: 2))!);

    // A wall along column x, with gaps at the rows given.
    private static NavBytes.Layer Wall(NavBytes.Layer layer, int x, params int[] gaps)
    {
        for (var y = 0; y < Size; y++)
        {
            if (!gaps.Contains(y))
            {
                layer = layer.Blocked(x, y);
            }
        }
        return layer;
    }

    private static NavCell Cell(int x, int y, int tile = 0) => new(tile, x, y);

    private static void AssertSteps(WalkGraph graph, IReadOnlyList<NavCell> path)
    {
        Assert.All(path, cell => Assert.True(graph.IsWalkable(cell)));
        for (var i = 1; i < path.Count; i++)
        {
            var (c0, r0) = graph.GridCell(path[i - 1]);
            var (c1, r1) = graph.GridCell(path[i]);
            Assert.Equal(1, Math.Max(Math.Abs(c1 - c0), Math.Abs(r1 - r0)));
        }
    }

    [Fact]
    public void AnOpenFloorIsCrossedDiagonally()
    {
        var graph = Graph(Open());

        var path = PathFinder.Path(graph, Cell(0, 0), Cell(5, 5));

        Assert.NotNull(path);
        Assert.Equal(6, path.Count);
        AssertSteps(graph, path);
        Assert.Equal([Cell(5, 5)], PathFinder.Corners(graph, path));
    }

    [Fact]
    public void ADiagonalNeverCutsAWallsCorner()
    {
        var graph = Graph(Open().Blocked(1, 0));

        var path = PathFinder.Path(graph, Cell(0, 0), Cell(1, 1));

        Assert.Equal([Cell(0, 0), Cell(0, 1), Cell(1, 1)], path);
        Assert.False(PathFinder.Clear(graph, Cell(0, 0), Cell(1, 1)));
    }

    [Fact]
    public void APathGoesAroundAWall()
    {
        var graph = Graph(Wall(Open(), 4, gaps: 7));

        var path = PathFinder.Path(graph, Cell(1, 1), Cell(6, 1));

        Assert.NotNull(path);
        AssertSteps(graph, path);
        Assert.Contains(Cell(4, 7), path);
        Assert.False(PathFinder.Clear(graph, Cell(1, 1), Cell(6, 1)));
        var corners = PathFinder.Corners(graph, path);
        Assert.Equal(Cell(6, 1), corners[^1]);
        Assert.InRange(corners.Count, 2, 4);
        Assert.All(corners.Zip(new[] { Cell(1, 1) }.Concat(corners)),
            leg => Assert.True(PathFinder.Clear(graph, leg.Second, leg.First)));
    }

    [Fact]
    public void APathGoesThroughADoorway()
    {
        var graph = Graph(Wall(Open(), 4, gaps: 3));

        var path = PathFinder.Path(graph, Cell(1, 6), Cell(7, 6));

        Assert.NotNull(path);
        AssertSteps(graph, path);
        Assert.Contains(Cell(4, 3), path);
    }

    [Fact]
    public void APathCrossesTheTileEdge()
    {
        var graph = Graph(Open(), Open(tx: 1));

        var path = PathFinder.Path(graph, Cell(6, 2), Cell(1, 2, tile: 1));

        Assert.NotNull(path);
        Assert.Equal(4, path.Count);
        Assert.True(PathFinder.Clear(graph, Cell(6, 2), Cell(1, 2, tile: 1)));
    }

    [Fact]
    public void AStepHigherThanTheClimbIsNotTaken()
    {
        // Column 4 is a ledge 5 high, linked by the bits; only row 2's cell, 2 high, is within the climb.
        var heights = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            heights[4 + (y * Size)] = (byte)(y == 2 ? 2 : 5);
        }
        var graph = Graph(Open() with { Heights = heights });

        var path = PathFinder.Path(graph, Cell(1, 6), Cell(7, 6));

        Assert.NotNull(path);
        Assert.Contains(Cell(4, 2), path);
        Assert.Null(PathFinder.Path(graph, Cell(1, 6), Cell(7, 6), climb: 1));
        Assert.Contains(Cell(4, 6), PathFinder.Path(graph, Cell(1, 6), Cell(7, 6), climb: 5) ?? []);
    }

    [Fact]
    public void AClosedWallHasNoPath()
    {
        var graph = Graph(Wall(Open(), 4));

        Assert.Null(PathFinder.Path(graph, Cell(1, 1), Cell(6, 1)));
        Assert.Null(PathFinder.Path(graph, Cell(1, 1), Cell(4, 1)));
    }

    [Fact]
    public void TheSearchGivesUpAtItsBound()
    {
        var graph = Graph(Open());

        Assert.Null(PathFinder.Path(graph, Cell(0, 0), Cell(7, 7), maxVisited: 3));
    }

    [Fact]
    public void APathOfOneCellHasNoCorners()
    {
        var graph = Graph(Open());

        Assert.Equal([Cell(2, 2)], PathFinder.Path(graph, Cell(2, 2), Cell(2, 2)));
        Assert.Empty(PathFinder.Corners(graph, [Cell(2, 2)]));
    }

    [Fact]
    public void ThePlannerEndsTheRouteAtTheTarget()
    {
        var graph = Graph(Wall(Open(), 4, gaps: 7));
        var planner = new NavRoutePlanner(NavMapFiles.Guess, (_, _) => Task.FromResult<WalkGraph?>(graph));
        var target = new Vector3(66, 0, 13);

        var plan = planner.Plan(12, new(15, 0, 15), target);

        Assert.Equal(RouteOutcome.Found, plan.Outcome);
        Assert.Equal(target, plan.Corners[^1]);
        Assert.True(plan.Corners.Count >= 2);
        Assert.Contains("map=12 file=f_0000_012", plan.Detail, StringComparison.Ordinal);
        Assert.Contains("cell=0:1,1", plan.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlannerSaysWhyThereIsNoRoute()
    {
        var walled = Graph(Wall(Open(), 4));
        var pending = new TaskCompletionSource<WalkGraph?>();
        var from = new Vector3(15, 0, 15);
        var to = new Vector3(65, 0, 15);

        Assert.Equal(RouteOutcome.NoNavMesh, Planner(walled).Plan(null, from, to).Outcome);
        Assert.Equal(RouteOutcome.NoNavMesh, Planner(null).Plan(12, from, to).Outcome);
        Assert.Equal(RouteOutcome.NoNavMesh,
            new NavRoutePlanner(NavMapFiles.Guess, (_, _) => null).Plan(12, from, to).Outcome);
        Assert.Equal(RouteOutcome.Loading,
            new NavRoutePlanner(NavMapFiles.Guess, (_, _) => pending.Task).Plan(12, from, to).Outcome);
        Assert.Equal(RouteOutcome.OffMesh, Planner(walled).Plan(12, new(5000, 0, 5000), to).Outcome);
        Assert.Equal(RouteOutcome.NoPath, Planner(walled).Plan(12, from, to).Outcome);
    }

    [Theory]
    [InlineData(12, 0, 12)]
    [InlineData(2, 0, 2)]
    [InlineData(1063008, 1063, 8)]
    public void TheMapFileGuessSplitsTheId(int id, int a, int b) => Assert.Equal((a, b), NavMapFiles.Guess(id));

    private static NavRoutePlanner Planner(WalkGraph? graph) =>
        new(NavMapFiles.Guess, (_, _) => Task.FromResult(graph));
}
