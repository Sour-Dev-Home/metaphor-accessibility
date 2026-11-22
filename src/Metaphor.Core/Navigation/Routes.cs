using System.Globalization;
using System.Numerics;

namespace Metaphor.Core.Navigation;

/// <summary>What came of planning a route.</summary>
public enum RouteOutcome
{
    /// <summary>A path joins the two; <see cref="RoutePlan.Corners"/> holds it.</summary>
    Found,

    /// <summary>The map's navmesh is still being read; asking again shortly may find it.</summary>
    Loading,

    /// <summary>No navmesh for the map: its id unread, no file for it, or the file unreadable.</summary>
    NoNavMesh,

    /// <summary>The player or the target isn't on, or near, a walkable cell of the map's navmesh.</summary>
    OffMesh,

    /// <summary>Both are on the navmesh, but no path joins them within the search's bound.</summary>
    NoPath,
}

/// <summary>A planned route (#690).</summary>
/// <param name="Outcome">What came of it.</param>
/// <param name="Corners">On <see cref="RouteOutcome.Found"/>, the points to walk to in turn, the last the target's own
/// position; otherwise empty.</param>
/// <param name="Detail">What was read, as numbers and file names, for the development log.</param>
public sealed record RoutePlan(RouteOutcome Outcome, IReadOnlyList<Vector3> Corners, string Detail)
{
    /// <summary>A plan that found no route.</summary>
    public static RoutePlan Failed(RouteOutcome outcome, string detail) => new(outcome, [], detail);
}

/// <summary>Plans walking routes on the player's map. Called off the game's thread.</summary>
public interface IRoutePlanner
{
    /// <summary>The route on map <paramref name="mapId"/> from <paramref name="start"/> to <paramref name="target"/>, in the
    /// game's units (<c>X</c> and <c>Z</c> across the ground, <c>Y</c> up).</summary>
    RoutePlan Plan(int? mapId, Vector3 start, Vector3 target);
}

/// <summary>Which <c>f_&lt;a&gt;_&lt;b&gt;.NAV</c> file holds a map's navmesh. Unknown on game build 548ddc95… (Steam
/// build 18330018): docs/findings/field-navmesh-files-tile-cache-static.md gives the file names' format and the map ids
/// session 6 read (2, 12, 285, 371, 372), not how one gives the other.</summary>
public static class NavMapFiles
{
    /// <summary>A guess, <c>a</c> the id's thousands and <c>b</c> the rest, so id 12 is <c>f_0000_012</c>
    /// [NEEDS VERIFICATION]: ours, from the format alone. A wrong guess costs nothing worse than the straight line:
    /// <see cref="NavRoutePlanner"/> plans only when the player stands on the file's cells, and logs the map id, the file
    /// and the player's cell so a session can confirm or replace it.</summary>
    public static (int A, int B)? Guess(int mapId) => mapId is >= 0 and <= 9_999_999 ? (mapId / 1000, mapId % 1000) : null;
}

/// <summary>Routes over the map's <c>.NAV</c> cells (#690): the player's and the target's nearest walkable cells,
/// <see cref="PathFinder.Path"/> between them and <see cref="PathFinder.Corners"/>, as cell centres, ending at the
/// target's own position. The world-to-cell transform is <see cref="WalkGraph.CellAt"/>'s: the game's positions taken as
/// the file's coordinates, same units and axes, <c>Y</c> up [NEEDS VERIFICATION]; the log's cell for the player's
/// position is the check. Thread-safe: the graphs are read-only.</summary>
public sealed class NavRoutePlanner : IRoutePlanner
{
    /// <summary>How many cells across the player's cell is looked for, when the position isn't on one (the walkable
    /// area is eroded along walls by the agent's radius).</summary>
    public const int StartRadiusCells = 6;

    /// <summary>As <see cref="StartRadiusCells"/>, for the target: a chest or door may stand off the walkable cells.
    /// </summary>
    public const int GoalRadiusCells = 12;

    private readonly Func<int, (int A, int B)?> fileFor;
    private readonly Func<int, int, Task<WalkGraph?>?> load;

    /// <summary>A planner that names a map's file with <paramref name="fileFor"/> and gets its graph from
    /// <paramref name="load"/> (as <see cref="NavMeshSource.Load"/>), never waiting on a read that hasn't finished.
    /// </summary>
    public NavRoutePlanner(Func<int, (int A, int B)?> fileFor, Func<int, int, Task<WalkGraph?>?> load)
    {
        ArgumentNullException.ThrowIfNull(fileFor);
        ArgumentNullException.ThrowIfNull(load);
        this.fileFor = fileFor;
        this.load = load;
    }

    /// <inheritdoc/>
    public RoutePlan Plan(int? mapId, Vector3 start, Vector3 target)
    {
        if (mapId is not { } id)
        {
            return RoutePlan.Failed(RouteOutcome.NoNavMesh, "map id unread");
        }
        if (fileFor(id) is not { } named || NavMeshSource.ArchivePath(named.A, named.B) is null)
        {
            return RoutePlan.Failed(RouteOutcome.NoNavMesh, Text($"map={id} file=none"));
        }
        var (a, b) = named;
        var file = Text($"map={id} file=f_{a:D4}_{b:D3}");
        var read = load(a, b);
        if (read is { IsCompleted: false })
        {
            return RoutePlan.Failed(RouteOutcome.Loading, file + " loading");
        }
        if (read is not { IsCompletedSuccessfully: true, Result: { } graph })
        {
            return RoutePlan.Failed(RouteOutcome.NoNavMesh, file + " unreadable");
        }
        var vertical = Vertical(graph.File);
        var player = Text($"{file} player=({start.X:0.#},{start.Y:0.#},{start.Z:0.#})");
        if (graph.NearestCell(start, vertical, StartRadiusCells) is not { } first)
        {
            return RoutePlan.Failed(RouteOutcome.OffMesh, player + " cell=none");
        }
        var cells = Text($"{player} cell={Cell(graph, first)}");
        if (graph.NearestCell(target, vertical, GoalRadiusCells) is not { } goal)
        {
            return RoutePlan.Failed(RouteOutcome.OffMesh, cells + " goal=none");
        }
        cells = Text($"{cells} goal={Cell(graph, goal)}");
        if (PathFinder.Path(graph, first, goal) is not { } path)
        {
            return RoutePlan.Failed(RouteOutcome.NoPath, cells + " path=none");
        }
        var corners = PathFinder.Corners(graph, path).Select(graph.Centre).ToList();
        if (corners.Count == 0)
        {
            corners.Add(target);
        }
        else
        {
            corners[^1] = target;
        }
        return new(RouteOutcome.Found, corners, Text($"{cells} path={path.Count} corners={corners.Count}"));
    }

    // How far above or below a position its cell's floor may be: the agent's height, or 200 units when the file gives
    // none [NEEDS VERIFICATION: by the log's cells].
    private static float Vertical(NavMeshFile file) =>
        float.IsFinite(file.WalkableHeight) && file.WalkableHeight > 0 ? file.WalkableHeight : 200;

    private static string Cell(WalkGraph graph, NavCell cell)
    {
        var (column, row) = graph.GridCell(cell);
        return Text($"{cell.Tile}:{column},{row}");
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>One step of <see cref="RouteFollower"/>.</summary>
/// <param name="Aim">The point to guide toward, or null when there is no route (guide straight to the target).</param>
/// <param name="Replan">Why a new route is wanted, or null when none is.</param>
public readonly record struct RouteStep(Vector3? Aim, string? Replan);

/// <summary>Follows a planned route (#690): aims at the next corner, moves on within <see cref="AdvanceRadius"/>, and
/// asks for a new route when the player is more than <see cref="OffPathDistance"/> from the leg being walked or makes no
/// progress toward its corner for <see cref="StuckMilliseconds"/> (the files miss the game's run-time obstacles). With
/// no route it asks again after <see cref="RetryMilliseconds"/> (<see cref="LoadingRetryMilliseconds"/> while the
/// navmesh loads). It asks once and waits for <see cref="Accept"/>. Distances are across the ground (<c>X</c>,
/// <c>Z</c>), in the game's units; the values are ours, to tune by ear [NEEDS VERIFICATION by ear]. Not thread-safe.
/// </summary>
public sealed class RouteFollower
{
    /// <summary>How near a corner counts as passed: five 20-unit cells.</summary>
    public const double AdvanceRadius = 100;

    /// <summary>How far from the leg being walked asks for a new route: eight 20-unit cells.</summary>
    public const double OffPathDistance = 160;

    /// <summary>How near the corner a step must bring the player to count as progress: one cell.</summary>
    public const double ProgressDistance = 20;

    /// <summary>How long without progress asks for a new route.</summary>
    public const long StuckMilliseconds = 3000;

    /// <summary>How long after a plan that found no route the next is asked for.</summary>
    public const long RetryMilliseconds = 5000;

    /// <summary>As <see cref="RetryMilliseconds"/>, while the map's navmesh loads.</summary>
    public const long LoadingRetryMilliseconds = 500;

    private IReadOnlyList<Vector3> corners = [];
    private int index;
    private Vector2 legStart;
    private bool requested;
    private RouteOutcome? last;
    private long acceptedAt;
    private double best;
    private long bestAt;

    /// <summary>Whether a route is being followed.</summary>
    public bool HasRoute => corners.Count > 0;

    /// <summary>The index in the route of the corner aimed at.</summary>
    public int Corner => index;

    /// <summary>Forgets the route and any plan asked for, so the next <see cref="Step"/> asks for one.</summary>
    public void Reset()
    {
        corners = [];
        requested = false;
        last = null;
    }

    /// <summary>Takes the plan last asked for, planned from <paramref name="from"/>: its route on
    /// <see cref="RouteOutcome.Found"/>, otherwise none.</summary>
    public void Accept(RoutePlan plan, Vector3 from, long now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        requested = false;
        last = plan.Outcome;
        acceptedAt = now;
        corners = plan.Outcome == RouteOutcome.Found ? plan.Corners : [];
        index = 0;
        legStart = Flat(from);
        best = double.PositiveInfinity;
        bestAt = now;
    }

    /// <summary>Moves on to the player at <paramref name="player"/>: the corner to aim at, and whether to plan anew.
    /// </summary>
    public RouteStep Step(Vector3 player, long now)
    {
        var at = Flat(player);
        if (corners.Count == 0)
        {
            if (requested)
            {
                return new(null, null);
            }
            var wait = last == RouteOutcome.Loading ? LoadingRetryMilliseconds : RetryMilliseconds;
            if (last is not null && now - acceptedAt < wait)
            {
                return new(null, null);
            }
            requested = true;
            return new(null, last is null ? "start" : "retry");
        }
        while (index < corners.Count - 1 && Vector2.Distance(at, Flat(corners[index])) <= AdvanceRadius)
        {
            legStart = Flat(corners[index]);
            index++;
            best = double.PositiveInfinity;
            bestAt = now;
        }
        var aim = corners[index];
        if (requested)
        {
            return new(aim, null);
        }
        var distance = Vector2.Distance(at, Flat(aim));
        if (distance < best - ProgressDistance || double.IsPositiveInfinity(best))
        {
            (best, bestAt) = (distance, now);
        }
        string? why = null;
        if (SegmentDistance(at, legStart, Flat(aim)) > OffPathDistance)
        {
            why = "off path";
        }
        else if (now - bestAt >= StuckMilliseconds)
        {
            why = "stuck";
        }
        requested = why is not null;
        return new(aim, why);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = ab.LengthSquared();
        var t = length < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / length, 0, 1);
        return Vector2.Distance(p, a + (t * ab));
    }
}
