using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Metaphor.Features.Updates;

/// <summary>Where the update check reads the published releases' tags (#576).</summary>
public interface IReleaseSource
{
    /// <summary>The tags of the published releases, pre-releases included; throws when they can't be read.</summary>
    Task<IReadOnlyList<string>> ReleaseTagsAsync(CancellationToken cancellationToken);
}

/// <summary>The mod's one network request (ADR 0018): GitHub's public REST API, unauthenticated, for this repository's
/// releases. It sends the request line and three headers and nothing else: no token, cookie, body or detail of the
/// player's. It follows no redirect, reads at most <see cref="MaxResponseBytes"/>, and gives up after its timeout.
/// The list endpoint, not <c>releases/latest</c>, because the latest one skips pre-releases
/// (https://docs.github.com/en/rest/releases/releases, API version 2022-11-28).</summary>
public sealed class GitHubReleases : IReleaseSource, IDisposable
{
    /// <summary>This repository's releases, newest first; 20 is more than a check needs to find the newest.</summary>
    public static Uri Endpoint { get; } =
        new("https://api.github.com/repos/Sour-Dev-Home/metaphor-accessibility/releases?per_page=20");

    /// <summary>How long the request may take before it is dropped.</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>The largest answer read; 20 releases are a few tens of kilobytes.</summary>
    public const int MaxResponseBytes = 1024 * 1024;

    // GitHub refuses a request without a User-Agent; this one names the project, never the player or the machine.
    private const string UserAgent = "metaphor-accessibility";

    private readonly HttpClient client;

    /// <summary>A source asking through <paramref name="handler"/>, which it disposes, within
    /// <paramref name="timeout"/>.</summary>
    public GitHubReleases(HttpMessageHandler handler, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(handler);
        client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout,
            MaxResponseContentBufferSize = MaxResponseBytes,
        };
    }

    /// <summary>A source over the network, with <see cref="DefaultTimeout"/>.</summary>
    public static GitHubReleases Create() => new(NewHandler(), DefaultTimeout);

    /// <summary>The handler <see cref="Create"/> uses: no redirects, no cookies, no credentials.</summary>
    internal static SocketsHttpHandler NewHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        Credentials = null,
        DefaultProxyCredentials = null,
        PreAuthenticate = false,
        AutomaticDecompression = DecompressionMethods.None,
    };

    public async Task<IReadOnlyList<string>> ReleaseTagsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, null));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        // Anything but 2xx throws, an unfollowed redirect (3xx) included.
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return Tags(body);
    }

    public void Dispose() => client.Dispose();

    // Each published release's tag_name; drafts and entries without a string tag are skipped.
    private static List<string> Tags(byte[] body)
    {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub's answer is not a list of releases");
        }
        List<string> tags = [];
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object
                || (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                || !release.TryGetProperty("tag_name", out var tag)
                || tag.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            tags.Add(tag.GetString()!);
        }
        return tags;
    }
}
