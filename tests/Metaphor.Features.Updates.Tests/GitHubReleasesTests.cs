using System.Net;
using System.Text;

namespace Metaphor.Features.Updates.Tests;

/// <summary>The one request the update check makes (#576, ADR 0018), against a fake HTTP handler: what is sent, and
/// what is read back.</summary>
[Trait("Category", "Unit")]
public sealed class GitHubReleasesTests
{
    private const string TwoReleases = """
        [
          { "tag_name": "v0.0.1-unstable.2", "draft": false, "prerelease": true },
          { "tag_name": "v0.0.1-unstable.1", "draft": false, "prerelease": true }
        ]
        """;

    private static async Task<IReadOnlyList<string>> Tags(FakeHandler handler, TimeSpan? timeout = null)
    {
        using var releases = new GitHubReleases(handler, timeout ?? TimeSpan.FromSeconds(30));
        return await releases.ReleaseTagsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItAsksThisRepositorysReleasesOnly()
    {
        var handler = FakeHandler.Answering(HttpStatusCode.OK, TwoReleases);

        await Tags(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://api.github.com/repos/Sour-Dev-Home/metaphor-accessibility/releases?per_page=20",
            request.Uri.AbsoluteUri);
    }

    // Nothing but the request: no token, no cookie, no body, and a User-Agent naming the project, not the player.
    [Fact]
    public async Task ItSendsOnlyTheHeadersGitHubAsksFor()
    {
        var handler = FakeHandler.Answering(HttpStatusCode.OK, TwoReleases);

        await Tags(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            ["Accept: application/vnd.github+json", "User-Agent: metaphor-accessibility", "X-GitHub-Api-Version: 2022-11-28"],
            request.Headers);
        Assert.False(request.HadBody);
    }

    [Fact]
    public async Task ItReadsEachReleasesTag()
    {
        Assert.Equal(["v0.0.1-unstable.2", "v0.0.1-unstable.1"], await Tags(FakeHandler.Answering(HttpStatusCode.OK, TwoReleases)));
    }

    // GitHub lists drafts only to accounts that can push, and the check has none; one is skipped all the same.
    [Fact]
    public async Task ItSkipsDraftsAndEntriesWithoutATag()
    {
        var json = """
            [
              { "tag_name": "v9.0.0", "draft": true },
              { "name": "no tag" },
              { "tag_name": 3 },
              "not a release",
              { "tag_name": "v0.0.1-unstable.2", "draft": false }
            ]
            """;

        Assert.Equal(["v0.0.1-unstable.2"], await Tags(FakeHandler.Answering(HttpStatusCode.OK, json)));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AnErrorStatusThrows(HttpStatusCode status)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Tags(FakeHandler.Answering(status, TwoReleases)));
    }

    // A redirect isn't followed, so the request can't end up anywhere but the address above.
    [Fact]
    public async Task ARedirectThrows()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Tags(FakeHandler.Answering(HttpStatusCode.MovedPermanently, "")));
    }

    [Theory]
    [InlineData("{ \"tag_name\": \"v1.0.0\" }")]
    [InlineData("not json")]
    public async Task AnAnswerThatIsNotAListThrows(string body)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Tags(FakeHandler.Answering(HttpStatusCode.OK, body)));
    }

    [Fact]
    public async Task AnAnswerOverTheLimitThrows()
    {
        var body = "[" + new string(' ', GitHubReleases.MaxResponseBytes) + "]";

        await Assert.ThrowsAsync<HttpRequestException>(() => Tags(FakeHandler.Answering(HttpStatusCode.OK, body)));
    }

    [Fact]
    public async Task ASlowAnswerTimesOut()
    {
        var handler = FakeHandler.Hanging();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Tags(handler, TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void TheTimeoutIsShort()
    {
        Assert.True(GitHubReleases.DefaultTimeout <= TimeSpan.FromSeconds(10));
    }

    // The real handler follows no redirect, keeps no cookie and sends no credentials of the player's.
    [Fact]
    public void TheRealHandlerSendsNothingOfThePlayers()
    {
        using var handler = GitHubReleases.NewHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.Null(handler.DefaultProxyCredentials);
        Assert.False(handler.PreAuthenticate);
    }

    /// <summary>One request as the handler saw it: its method, address, headers as "Name: value" in name order, and
    /// whether it had a body.</summary>
    internal sealed record SeenRequest(HttpMethod Method, Uri Uri, IReadOnlyList<string> Headers, bool HadBody);

    /// <summary>An HTTP handler that answers from memory and records what it was asked.</summary>
    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> answer;

        private FakeHandler(Func<CancellationToken, Task<HttpResponseMessage>> answer) => this.answer = answer;

        public List<SeenRequest> Requests { get; } = [];

        public static FakeHandler Answering(HttpStatusCode status, string body) =>
            new(_ => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }));

        public static FakeHandler Hanging() => new(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(
                request.Method,
                request.RequestUri!,
                [.. request.Headers.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")],
                request.Content is not null));
            return answer(cancellationToken);
        }
    }
}
