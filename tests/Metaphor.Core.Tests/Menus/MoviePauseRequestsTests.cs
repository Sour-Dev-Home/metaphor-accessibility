using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The cutscene skip menu's requests (#809): a request of 1 shows the menu, so its observers hear it open; any
/// other value, or none, is the close path and says nothing.</summary>
[Trait("Category", "Unit")]
public sealed class MoviePauseRequestsTests
{
    private readonly MoviePauseRequests requests = new();
    private readonly Counting observer = new();

    [Fact]
    public void NoFeatureNoneWanted()
    {
        Assert.False(requests.Any);

        requests.Observe(observer);

        Assert.True(requests.Any);
    }

    [Fact]
    public void AnObserverWhoseSettingIsOffIsNotWantedAndHearsNothing()
    {
        var on = false;
        requests.Observe(observer, () => on);

        Assert.False(requests.Any);
        requests.Requested(MoviePauseRequests.Show);
        Assert.Equal(0, observer.Opens);

        on = true;

        Assert.True(requests.Any);
        requests.Requested(MoviePauseRequests.Show);
        Assert.Equal(1, observer.Opens);
    }

    [Fact]
    public void AShowRequestIsTheOpen()
    {
        requests.Observe(observer);

        requests.Requested(MoviePauseRequests.Show);

        Assert.Equal(1, observer.Opens);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(null)]
    public void AnyOtherRequestSaysNothing(int? value)
    {
        requests.Observe(observer);

        requests.Requested(value);

        Assert.Equal(0, observer.Opens);
    }

    private sealed class Counting : IMoviePauseObserver
    {
        public int Opens { get; private set; }

        public void Opened() => Opens++;
    }
}
