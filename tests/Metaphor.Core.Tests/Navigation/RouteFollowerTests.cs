using System.Numerics;
using Metaphor.Core.Navigation;

namespace Metaphor.Core.Tests.Navigation;

/// <summary>Following a planned route (#690): aiming at the next corner, moving on near it, and asking for a new route
/// off the path, when stuck, or after a plan found none. Made-up routes in game units.</summary>
[Trait("Category", "Unit")]
public sealed class RouteFollowerTests
{
    private static readonly Vector3 Start = new(0, 0, 0);

    // East 1000, then north 1000 to the target.
    private static readonly RoutePlan LShape =
        new(RouteOutcome.Found, [new(1000, 0, 0), new(1000, 0, 1000)], "");

    private readonly RouteFollower follower = new();

    private RouteFollower Following(long now = 0)
    {
        Assert.Equal("start", follower.Step(Start, now).Replan);
        follower.Accept(LShape, Start, now);
        return follower;
    }

    [Fact]
    public void ItAimsAtTheFirstCorner()
    {
        var step = Following().Step(new(100, 0, 0), 100);

        Assert.Equal(new Vector3(1000, 0, 0), step.Aim);
        Assert.Null(step.Replan);
    }

    [Fact]
    public void ItMovesOnWithinTheRadiusButNotPastTheTarget()
    {
        Following();

        Assert.Equal(new Vector3(1000, 0, 1000), follower.Step(new(950, 0, 30), 1000).Aim);
        Assert.Equal(1, follower.Corner);
        Assert.Equal(new Vector3(1000, 0, 1000), follower.Step(new(1000, 0, 990), 2000).Aim);
        Assert.Equal(1, follower.Corner);
    }

    [Fact]
    public void LeavingThePathAsksOnceForANewRoute()
    {
        Following();

        Assert.Equal("off path", follower.Step(new(500, 0, -400), 100).Replan);
        Assert.Null(follower.Step(new(500, 0, -400), 200).Replan);
        Assert.Equal(new Vector3(1000, 0, 0), follower.Step(new(500, 0, -400), 200).Aim);

        follower.Accept(LShape, new(500, 0, -400), 300);

        Assert.Null(follower.Step(new(500, 0, -400), 400).Replan);
    }

    [Fact]
    public void NoProgressAsksForANewRoute()
    {
        Following();
        follower.Step(new(200, 0, 0), 0);

        Assert.Null(follower.Step(new(210, 0, 0), RouteFollower.StuckMilliseconds - 1).Replan);
        Assert.Equal("stuck", follower.Step(new(210, 0, 0), RouteFollower.StuckMilliseconds).Replan);
    }

    [Fact]
    public void ProgressKeepsTheRoute()
    {
        Following();

        for (var t = 0; t <= 4 * RouteFollower.StuckMilliseconds; t += 500)
        {
            Assert.Null(follower.Step(new(t / 20f, 0, 0), t).Replan);
        }
    }

    [Fact]
    public void WithNoRouteItGuidesStraightAndRetriesLater()
    {
        Assert.Equal("start", follower.Step(Start, 0).Replan);
        Assert.Null(follower.Step(Start, 10).Replan);

        follower.Accept(RoutePlan.Failed(RouteOutcome.NoPath, ""), Start, 100);

        Assert.Null(follower.Step(Start, 100 + RouteFollower.RetryMilliseconds - 1).Aim);
        Assert.Null(follower.Step(Start, 100 + RouteFollower.RetryMilliseconds - 1).Replan);
        Assert.Equal("retry", follower.Step(Start, 100 + RouteFollower.RetryMilliseconds).Replan);
    }

    [Fact]
    public void WhileTheNavMeshLoadsItRetriesSooner()
    {
        follower.Step(Start, 0);
        follower.Accept(RoutePlan.Failed(RouteOutcome.Loading, ""), Start, 0);

        Assert.Equal("retry", follower.Step(Start, RouteFollower.LoadingRetryMilliseconds).Replan);
    }

    [Fact]
    public void ResetForgetsTheRoute()
    {
        Following();
        follower.Reset();

        Assert.False(follower.HasRoute);
        Assert.Equal("start", follower.Step(Start, 0).Replan);
    }
}
