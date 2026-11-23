namespace Metaphor.Core.Navigation;

/// <summary>Paths over a <see cref="WalkGraph"/>'s cells for the beacon (#690): A* over the eight neighbours of a cell
/// with an octile heuristic, and the path cut down to its corners by line of sight. Pure and bounded, as the graph.
/// <para>A step is one of the graph's links whose floors differ by at most the climb limit (the graph's
/// <see cref="WalkGraph.Climb"/> unless given). A diagonal step needs both of the square's sides walkable, each way
/// round, so a path never cuts a wall's corner. Both rules are ours, not read from the game.</para></summary>
public static class PathFinder
{
    /// <summary>The most cells a line-of-sight test spans when cutting a path to corners, so a long straight run costs
    /// a corner every this many cells rather than a test over the whole map.</summary>
    public const int MaxCornerSpan = 64;

    private const int Sides = 4;
    private static readonly float Diagonal = MathF.Sqrt(2);

    /// <summary>The shortest path from <paramref name="from"/> to <paramref name="to"/>, both included, with straight
    /// steps costing one and diagonal ones √2; or null when there is none, either isn't walkable, or the search closed
    /// <paramref name="maxVisited"/> cells first.</summary>
    public static IReadOnlyList<NavCell>? Path(
        WalkGraph graph, NavCell from, NavCell to, float? climb = null, int maxVisited = WalkGraph.DefaultMaxVisited)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.IsWalkable(from) || !graph.IsWalkable(to))
        {
            return null;
        }
        var limit = climb ?? graph.Climb;
        var goal = graph.GridCell(to);
        var costs = new Dictionary<NavCell, float> { [from] = 0 };
        var parents = new Dictionary<NavCell, NavCell> { [from] = from };
        var closed = new HashSet<NavCell>();
        var open = new PriorityQueue<NavCell, float>();
        var steps = new List<(NavCell Cell, float Cost)>(2 * Sides);
        open.Enqueue(from, Octile(graph.GridCell(from), goal));
        while (open.TryDequeue(out var cell, out _))
        {
            if (!closed.Add(cell))
            {
                continue;
            }
            if (cell == to)
            {
                return Trace(parents, from, to);
            }
            if (closed.Count >= maxVisited)
            {
                return null;
            }
            var cost = costs[cell];
            steps.Clear();
            Steps(graph, cell, limit, steps);
            foreach (var (next, step) in steps)
            {
                var reached = cost + step;
                if (closed.Contains(next) || (costs.TryGetValue(next, out var known) && known <= reached))
                {
                    continue;
                }
                costs[next] = reached;
                parents[next] = cell;
                open.Enqueue(next, reached + Octile(graph.GridCell(next), goal));
            }
        }
        return null;
    }

    /// <summary>Whether the straight line from <paramref name="from"/>'s centre to <paramref name="to"/>'s crosses only
    /// steps, as <see cref="Path"/> takes them: through each cell boundary it meets, and at a cell corner it passes
    /// exactly, the diagonal step.</summary>
    public static bool Clear(WalkGraph graph, NavCell from, NavCell to, float? climb = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.IsWalkable(from) || !graph.IsWalkable(to))
        {
            return false;
        }
        var limit = climb ?? graph.Climb;
        var (c0, r0) = graph.GridCell(from);
        var (c1, r1) = graph.GridCell(to);
        var (nx, nz) = (Math.Abs(c1 - c0), Math.Abs(r1 - r0));
        // Recast's sides: 0 is −x, 1 is +z, 2 is +x, 3 is −z (WalkGraph).
        var sideX = c1 > c0 ? 2 : 0;
        var sideZ = r1 > r0 ? 1 : 3;
        var cell = from;
        var (ix, iz) = (0, 0);
        while (ix < nx || iz < nz)
        {
            // The line meets its next x boundary at t = (ix + ½) / nx and its next z boundary at (iz + ½) / nz; compared
            // by cross-multiplying, so a corner is exact.
            var alongX = ((2L * ix) + 1) * nz;
            var alongZ = ((2L * iz) + 1) * nx;
            NavCell? next;
            if (alongX == alongZ)
            {
                next = DiagonalStep(graph, cell, sideX, sideZ, limit);
                (ix, iz) = (ix + 1, iz + 1);
            }
            else if (alongX < alongZ)
            {
                next = Step(graph, cell, sideX, limit);
                ix++;
            }
            else
            {
                next = Step(graph, cell, sideZ, limit);
                iz++;
            }
            if (next is not { } stepped)
            {
                return false;
            }
            cell = stepped;
        }
        return cell == to;
    }

    /// <summary>The cells of <paramref name="path"/> where it turns, after its first and ending with its last: from
    /// each corner, the farthest later cell in line of sight (<see cref="Clear"/>), at most
    /// <see cref="MaxCornerSpan"/> cells on, is the next. Empty for a path of one cell or none.</summary>
    public static IReadOnlyList<NavCell> Corners(WalkGraph graph, IReadOnlyList<NavCell> path, float? climb = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(path);
        var corners = new List<NavCell>();
        if (path.Count < 2)
        {
            return corners;
        }
        var anchor = 0;
        var reach = 1;
        while (reach < path.Count - 1)
        {
            if (reach + 1 - anchor <= MaxCornerSpan && Clear(graph, path[anchor], path[reach + 1], climb))
            {
                reach++;
                continue;
            }
            corners.Add(path[reach]);
            anchor = reach;
            reach = anchor + 1;
        }
        corners.Add(path[^1]);
        return corners;
    }

    // The straight and diagonal steps from cell, with their costs.
    private static void Steps(WalkGraph graph, NavCell cell, float climb, List<(NavCell Cell, float Cost)> steps)
    {
        for (var side = 0; side < Sides; side++)
        {
            if (Step(graph, cell, side, climb) is { } next)
            {
                steps.Add((next, 1));
            }
        }
        for (var side = 0; side < Sides; side++)
        {
            if (DiagonalStep(graph, cell, side, (side + 1) % Sides, climb) is { } corner)
            {
                steps.Add((corner, Diagonal));
            }
        }
    }

    // The linked cell on side, when its floor is within the climb.
    private static NavCell? Step(WalkGraph graph, NavCell cell, int side, float climb) =>
        graph.Neighbour(cell, side) is { } next && Math.Abs(graph.Centre(next).Y - graph.Centre(cell).Y) <= climb
            ? next
            : null;

    // The cell across the corner between sides a and b, when it is reached both ways round and within the climb.
    private static NavCell? DiagonalStep(WalkGraph graph, NavCell cell, int a, int b, float climb)
    {
        if (Step(graph, cell, a, climb) is not { } viaA || Step(graph, cell, b, climb) is not { } viaB ||
            Step(graph, viaA, b, climb) is not { } first || Step(graph, viaB, a, climb) is not { } second ||
            first != second || Math.Abs(graph.Centre(first).Y - graph.Centre(cell).Y) > climb)
        {
            return null;
        }
        return first;
    }

    private static float Octile((int Column, int Row) a, (int Column, int Row) b)
    {
        var dx = Math.Abs(a.Column - b.Column);
        var dz = Math.Abs(a.Row - b.Row);
        return dx + dz + ((Diagonal - 2) * Math.Min(dx, dz));
    }

    private static List<NavCell> Trace(Dictionary<NavCell, NavCell> parents, NavCell from, NavCell to)
    {
        var path = new List<NavCell> { to };
        for (var at = to; at != from; at = parents[at])
        {
            path.Add(parents[at]);
        }
        path.Reverse();
        return path;
    }
}
