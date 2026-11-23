using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The layout animation hook runs on every animation any layout starts, so it hands on only the classes a
/// feature asked for (#809), and reads nothing when none did.</summary>
[Trait("Category", "Unit")]
public sealed class LayoutAnimationsTests
{
    private const string Asked = "SampleLayout@ui@app";
    private const string Other = "OtherLayout@ui@app";

    [Fact]
    public void NoObserverWantsNothing()
    {
        var layouts = new LayoutAnimations();

        Assert.False(layouts.Any);
        Assert.False(layouts.Wants(Asked));
    }

    [Fact]
    public void OnlyAnAskedClassIsWanted()
    {
        var layouts = new LayoutAnimations();
        layouts.Observe(new Heard([Asked]));

        Assert.True(layouts.Any);
        Assert.True(layouts.Wants(Asked));
        Assert.False(layouts.Wants(Other));
        Assert.False(layouts.Wants(null));
    }

    [Fact]
    public void EachObserverHearsOnlyItsOwnClasses()
    {
        var layouts = new LayoutAnimations();
        var first = new Heard([Asked]);
        var second = new Heard([Other]);
        layouts.Observe(first);
        layouts.Observe(second);

        layouts.Played(new LayoutAnimation(Asked, 7));

        Assert.Equal([new LayoutAnimation(Asked, 7)], first.Played);
        Assert.Empty(second.Played);
    }

    private sealed class Heard(IReadOnlyCollection<string> layouts) : ILayoutAnimationObserver
    {
        public List<LayoutAnimation> Played { get; } = [];

        public IReadOnlyCollection<string> Layouts => layouts;

        void ILayoutAnimationObserver.Played(LayoutAnimation played) => Played.Add(played);
    }
}
