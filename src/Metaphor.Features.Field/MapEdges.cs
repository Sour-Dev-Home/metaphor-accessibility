using System.Numerics;
using Metaphor.Core.Navigation;

namespace Metaphor.Features.Field;

/// <summary>How far from <paramref name="position"/> along <paramref name="heading"/> (x, z) the wall is, at most
/// <paramref name="maxDistance"/>, or null off the mesh: the shape of <see cref="WalkGraph.DistanceToWall"/>.</summary>
public delegate float? EdgeDistance(Vector3 position, Vector2 heading, float maxDistance);

/// <summary>One map's walls: the <paramref name="Distance"/> to them and the cell a probe at a position starts from
/// (<paramref name="Start"/>, as <see cref="WalkGraph.WallStart"/>), for the Debug log; and whether a ground point is
/// open floor (<paramref name="Open"/>, for the map cursor, #877), null when not known.</summary>
public sealed record NavEdges(EdgeDistance Distance, Func<Vector3, NavCell?> Start, Func<Vector3, bool>? Open = null)
{
    /// <summary>The walls of <paramref name="graph"/>; a point is open when a walkable cell lies under it on any floor
    /// (<see cref="WalkGraph.CellAt"/>), so the eroded border along a wall counts as wall.</summary>
    public static NavEdges Of(WalkGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new(graph.DistanceToWall, graph.WallStart, point => graph.CellAt(point, float.PositiveInfinity) is not null);
    }
}

/// <summary>The navmesh edges of the player's map, for the wall tones (#830), found without blocking the caller: naming
/// the map's file reads the archive's map table the first time (<see cref="NavMapFiles"/>, #741), so that runs on the
/// thread pool, then the map's graph loads through the same <see cref="NavMeshSource"/> as the beacon's routes. The
/// last map asked for is kept.</summary>
public sealed class MapEdges
{
    private readonly Lock gate = new();
    private readonly Func<int, (int A, int B)?> fileFor;
    private readonly Func<int, int, Task<WalkGraph?>?> load;
    private readonly Func<Func<Task<NavEdges?>>, Task<NavEdges?>> run;
    private int? map;
    private Task<NavEdges?>? edges;

    /// <summary>Edges that name a map's file with <paramref name="fileFor"/> and load its graph with
    /// <paramref name="load"/> (as <see cref="NavMeshSource.Load"/>), both inside <paramref name="run"/> (the thread
    /// pool unless given).</summary>
    public MapEdges(
        Func<int, (int A, int B)?> fileFor, Func<int, int, Task<WalkGraph?>?> load,
        Func<Func<Task<NavEdges?>>, Task<NavEdges?>>? run = null)
    {
        ArgumentNullException.ThrowIfNull(fileFor);
        ArgumentNullException.ThrowIfNull(load);
        this.fileFor = fileFor;
        this.load = load;
        this.run = run ?? Task.Run;
    }

    /// <summary>Map <paramref name="mapId"/>'s edges: a task that ends with them, or with null when the map has no
    /// navmesh or it couldn't be read. Returns at once.</summary>
    public Task<NavEdges?> For(int mapId)
    {
        lock (gate)
        {
            if (map != mapId || edges is null)
            {
                map = mapId;
                edges = run(() => Load(mapId));
            }
            return edges;
        }
    }

    private async Task<NavEdges?> Load(int mapId)
    {
        if (fileFor(mapId) is not { } file || load(file.A, file.B) is not { } read)
        {
            return null;
        }
        return await read.ConfigureAwait(false) is { } graph ? NavEdges.Of(graph) : null;
    }
}
