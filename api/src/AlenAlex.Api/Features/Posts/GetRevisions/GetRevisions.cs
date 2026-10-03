using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlenAlex.Api.Features.Posts.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Posts.GetRevisions;

public sealed record GetRevisionsResponse(IReadOnlyList<PostRevision> Revisions);

/// <param name="ShortSha">First 7 characters of <see cref="Sha"/>.</param>
/// <param name="Date">Author date, <c>YYYY-MM-DD</c> (UTC).</param>
/// <param name="Message">First line of the commit message.</param>
/// <param name="Url">The commit on github.com.</param>
public sealed record PostRevision(string Sha, string ShortSha, string Date, string Message, string Url);

public sealed class GetRevisionsEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/{folder}/revisions", (string folder, HttpContext http, IOptions<GithubOptions> github,
                GetRevisionsHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(folder, out var path), path, http, github, handler, ct))
            .WithSummary("Commits touching blogs/{folder}, newest first (cached 10 min)");

        app.MapGet("/{series}/{part}/revisions", (string series, string part, HttpContext http, IOptions<GithubOptions> github,
                GetRevisionsHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(series, part, out var path), path, http, github, handler, ct))
            .WithSummary("Commits touching blogs/{series}/{part} (a part of a series), newest first (cached 10 min)");
    }

    private static async Task<IResult> HandleAsync(bool valid, PostPath path, HttpContext http, IOptions<GithubOptions> github,
        GetRevisionsHandler handler, CancellationToken ct)
    {
        if (!valid)
        {
            return ApiErrors.BadRequest();
        }
        if (!github.Value.IsConfigured)
        {
            return PostsResults.NotConfigured();
        }

        return await PostsResults.HandleGithubErrorsAsync(async () =>
        {
            var revisions = await handler.HandleAsync(path, ct);
            http.Response.Headers.CacheControl = "public, max-age=600";
            return TypedResults.Ok(revisions);
        }, notFoundMessage: "post not found");
    }
}

public sealed class GetRevisionsHandler(GithubContentClient github, PostsCache cache)
{
    public Task<GetRevisionsResponse> HandleAsync(PostPath post, CancellationToken ct = default) =>
        cache.GetOrCreateAsync($"revisions:{post}", PostsCache.RevisionsLifetime, async () =>
        {
            var path = $"{github.BlogsPath}/{post}";
            var url = $"repos/{github.Repo}/commits?path={Uri.EscapeDataString(path)}&per_page=30";
            using var response = await github.GetAsync(url, GithubContentClient.JsonMediaType, ct);
            var commits = await response.Content.ReadFromJsonAsync(GithubCommitsJsonContext.Default.ListGithubCommit, ct) ?? [];
            return new GetRevisionsResponse(commits.Select(ToRevision).ToList());
        }, value => 256L * value.Revisions.Count);

    internal static PostRevision ToRevision(GithubCommit item)
    {
        var date = item.Commit.Author?.Date ?? item.Commit.Committer?.Date;
        var firstLine = item.Commit.Message.Split('\n', 2)[0].TrimEnd('\r');
        return new PostRevision(
            Sha: item.Sha,
            ShortSha: item.Sha.Length > 7 ? item.Sha[..7] : item.Sha,
            Date: date?.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            Message: firstLine,
            Url: item.HtmlUrl);
    }
}

// GitHub REST is snake_case, hence a separate context.

internal sealed record GithubCommit(string Sha, string HtmlUrl, GithubCommitDetail Commit);

internal sealed record GithubCommitDetail(string Message, GithubCommitSignature? Author, GithubCommitSignature? Committer);

internal sealed record GithubCommitSignature(DateTimeOffset? Date);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<GithubCommit>))]
internal sealed partial class GithubCommitsJsonContext : JsonSerializerContext;
