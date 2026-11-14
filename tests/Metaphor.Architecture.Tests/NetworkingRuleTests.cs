namespace Metaphor.Architecture.Tests;

[Trait("Category", "Unit")]
public sealed class NetworkingRuleTests
{
    [Fact]
    public void HttpClientIsAViolation()
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Planted",
            "public static class C { public static object M() => new System.Net.Http.HttpClient(); }");

        Assert.Contains(Rules.NoNetworking(assembly), v => v.Contains("System.Net.Http.HttpClient"));
    }

    [Fact]
    public void SocketsInTheHookLayerAreAViolation()
    {
        var assembly = Plant.Compile(
            Rules.HookLayer,
            """
            public static class C
            {
                public static object M() => new System.Net.Sockets.TcpClient();
            }
            """);

        Assert.Contains(Rules.NoNetworking(assembly), v => v.Contains("System.Net.Sockets.TcpClient"));
    }

    [Fact]
    public void CodeWithoutNetworkingPasses()
    {
        var assembly = Plant.Compile("Metaphor.Features.Planted", "public static class C { public static int M() => 1; }");

        Assert.Empty(Rules.NoNetworking(assembly));
    }

    [Theory]
    [MemberData(nameof(Scanned.Names), MemberType = typeof(Scanned))]
    public void RealAssembliesHaveNoNetworking(string name) => Assert.Empty(Rules.NoNetworking(Scanned.Load(name)));
}
