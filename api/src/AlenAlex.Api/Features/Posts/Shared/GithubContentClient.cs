using System.Net;
using System.Net.Http.Headers;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Posts.Shared;

/// <summary>Scoped to <c>Github:Repo</c> and <c>Github:BlogsPath</c>. Callers pass validated input only.</summary>
public sealed class GithubContentClient(HttpClient http, IOptions<GithubOptions> options, ILogger<GithubContentClient> logger)
{
    public const string RawMediaType = "application/vnd.github.raw+json";
    public const string JsonMediaType = "application/vnd.github+json";

    public string Repo => options.Value.Repo;

    public string BlogsPath => options.Value.BlogsPath.Trim('/');

    public string ContentsUrl(string relativePath, string gitRef)
    {
        var path = string.Join('/', $"{BlogsPath}/{relativePath}".Split('/').Select(Uri.EscapeDataString));
        return $"repos/{Repo}/contents/{path}?ref={Uri.EscapeDataString(gitRef)}";
    }

    public async Task<string> GetRawTextAsync(string relativePath, string gitRef, CancellationToken ct)
    {
        using var response = await GetAsync(ContentsUrl(relativePath, gitRef), RawMediaType, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Maps GitHub failures to exceptions; the caller disposes the response.</summary>
    /// <exception cref="PostNotFoundException"/>
    /// <exception cref="GithubUpstreamException"/>
    public async Task<HttpResponseMessage> GetAsync(string url, string accept, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "GitHub request {Url} failed", url);
            throw new GithubUpstreamException($"GitHub request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("GitHub request {Url} timed out", url);
            throw new GithubUpstreamException("GitHub request timed out", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        // GitHub also answers 404 for private repos the token cannot see, and 422 for an unknown ref.
        if (status is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            throw new PostNotFoundException();
        }

        logger.LogWarning("GitHub answered {Status} for {Url}", (int)status, url);
        throw new GithubUpstreamException($"GitHub answered {(int)status} for {url}");
    }
}
