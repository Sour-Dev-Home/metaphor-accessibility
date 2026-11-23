using Metaphor.Core.Navigation;

namespace Metaphor.Features.Field.Tests;

/// <summary>The wall tones' navmesh lookup (#830): it names the map's file off the caller's thread, keeps the last map,
/// and ends with null when the map has no navmesh.</summary>
[Trait("Category", "Unit")]
public sealed class MapEdgesTests
{
    private readonly List<int> named = [];
    private readonly List<(int A, int B)> loaded = [];
    private readonly List<Func<Task<EdgeDistance?>>> queued = [];

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

    private MapEdges Make((int A, int B)? file) => new(
        id => { named.Add(id); return file; }, Load,
        work => { queued.Add(work); return new TaskCompletionSource<EdgeDistance?>().Task; });

    private Task<WalkGraph?>? Load(int a, int b)
    {
        loaded.Add((a, b));
        return Task.FromResult<WalkGraph?>(null);
    }
}
