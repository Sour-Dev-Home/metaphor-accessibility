using System.Numerics;
using Metaphor.Core.Navigation;

namespace Metaphor.Features.Field;

/// <summary>How far from <paramref name="position"/> along <paramref name="heading"/> (x, z) the player can walk before
/// the navmesh ends, at most <paramref name="maxDistance"/>, or null off the mesh: the shape of
/// <see cref="WalkGraph.DistanceToEdge"/>.</summary>
public delegate float? EdgeDistance(Vector3 position, Vector2 heading, float maxDistance);

/// <summary>The navmesh edges of the player's map, for the wall tones (#830), found without blocking the caller: naming
/// the map's file reads the archive's map table the first time (<see cref="NavMapFiles"/>, #741), so that runs on the
/// thread pool, then the map's graph loads through the same <see cref="NavMeshSource"/> as the beacon's routes. The
/// last map asked for is kept.</summary>
public sealed class MapEdges
{
    private readonly Lock gate = new();
    private readonly Func<int, (int A, int B)?> fileFor;
    private readonly Func<int, int, Task<WalkGraph?>?> load;
    private readonly Func<Func<Task<EdgeDistance?>>, Task<EdgeDistance?>> run;
    private int? map;
    private Task<EdgeDistance?>? edges;

    /// <summary>Edges that name a map's file with <paramref name="fileFor"/> and load its graph with
    /// <paramref name="load"/> (as <see cref="NavMeshSource.Load"/>), both inside <paramref name="run"/> (the thread
    /// pool unless given).</summary>
    public MapEdges(
        Func<int, (int A, int B)?> fileFor, Func<int, int, Task<WalkGraph?>?> load,
        Func<Func<Task<EdgeDistance?>>, Task<EdgeDistance?>>? run = null)
    {
        ArgumentNullException.ThrowIfNull(fileFor);
        ArgumentNullException.ThrowIfNull(load);
        this.fileFor = fileFor;
        this.load = load;
        this.run = run ?? Task.Run;
    }

    /// <summary>Map <paramref name="mapId"/>'s edges: a task that ends with them, or with null when the map has no
    /// navmesh or it couldn't be read. Returns at once.</summary>
    public Task<EdgeDistance?> For(int mapId)
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

    private async Task<EdgeDistance?> Load(int mapId)
    {
        if (fileFor(mapId) is not { } file || load(file.A, file.B) is not { } read)
        {
            return null;
        }
        return await read.ConfigureAwait(false) is { } graph ? graph.DistanceToEdge : null;
    }
}
