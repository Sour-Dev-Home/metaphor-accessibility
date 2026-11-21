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

    // ADR 0018's exception: the update check's assembly, and no other (#576).
    [Fact]
    public void OnlyTheUpdateCheckMayUseHttp()
    {
        const string code = "public static class C { public static object M() => new System.Net.Http.HttpClient(); }";

        Assert.Empty(Rules.NoNetworking(Plant.Compile("Metaphor.Features.Updates", code)));
        Assert.NotEmpty(Rules.NoNetworking(Plant.Compile("Metaphor.Features.Updates2", code)));
        Assert.NotEmpty(Rules.NoNetworking(Plant.Compile(Rules.HookLayer, code)));
    }

    // The exception covers HTTP only: sockets, web sockets and the rest of System.Net still fail there.
    [Theory]
    [InlineData("new System.Net.Sockets.TcpClient()", "System.Net.Sockets.TcpClient")]
    [InlineData("new System.Net.WebSockets.ClientWebSocket()", "System.Net.WebSockets.ClientWebSocket")]
    [InlineData("System.Net.Dns.GetHostName()", "System.Net.Dns")]
    public void TheUpdateCheckMayNotUseOtherNetworking(string expression, string type)
    {
        var assembly = Plant.Compile(
            "Metaphor.Features.Updates", $"public static class C {{ public static object M() => {expression}; }}");

        Assert.Contains(Rules.NoNetworking(assembly), v => v.Contains(type, StringComparison.Ordinal));
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
