using Metaphor.Core.Navigation;
using Metaphor.Core.Tests.Navigation;

namespace Metaphor.Features.Field.Tests;

/// <summary>The wall tones' navmesh lookup (#830): it names the map's file off the caller's thread, keeps the last map,
/// and ends with null when the map has no navmesh.</summary>
[Trait("Category", "Unit")]
public sealed class MapEdgesTests
{
    private readonly List<int> named = [];
    private readonly List<(int A, int B)> loaded = [];
    private readonly List<Func<Task<NavEdges?>>> queued = [];

    [Fact]
    public void NothingIsReadUntilTheRunnerRunsIt()
    {
        var edges = Make((1, 2));

        var found = edges.For(7);

        Assert.False(found.IsCompleted);
        Assert.Empty(named);
        Assert.Single(queued);
    }

    [Fact]
    public async Task AMapWithNoFileHasNoEdges()
    {
        var edges = new MapEdges(id => { named.Add(id); return null; }, Load, work => work());

        var found = edges.For(7);

        Assert.True(found.IsCompletedSuccessfully);
        Assert.Null(await found);
        Assert.Equal([7], named);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task AFileThatWontReadHasNoEdges()
    {
        var edges = new MapEdges(_ => (1, 2), Load, work => work());

        Assert.Null(await edges.For(7));
        Assert.Equal([(1, 2)], loaded);
    }

    [Fact]
    public void TheLastMapIsKeptAndANewOneAskedFor()
    {
        var edges = new MapEdges(id => { named.Add(id); return null; }, Load, work => work());

        var first = edges.For(7);
        Assert.Same(first, edges.For(7));
        edges.For(8);

        Assert.Equal([7, 8], named);
    }

    [Fact]
    public void APointOverTheWalkingMapIsOpenAndOneOffItIsWall()
    {
        // #877's map cursor: two 4 x 4 tiles of 10-unit cells, x 0 to 80, z 0 to 40, at any height.
        var graph = new WalkGraph(NavMeshFile.Parse(NavBytes.File(
            [NavBytes.Layer.Open(0, 0).Entry(), NavBytes.Layer.Open(1, 0).Entry()], climb: 2))!);

        var open = NavEdges.Of(graph).Open!;

        Assert.True(open(new System.Numerics.Vector3(35, 100, 15)));
        Assert.False(open(new System.Numerics.Vector3(500, 0, 500)));
    }

    private MapEdges Make((int A, int B)? file) => new(
        id => { named.Add(id); return file; }, Load,
        work => { queued.Add(work); return new TaskCompletionSource<NavEdges?>().Task; });

    private Task<WalkGraph?>? Load(int a, int b)
    {
        loaded.Add((a, b));
        return Task.FromResult<WalkGraph?>(null);
    }
}
