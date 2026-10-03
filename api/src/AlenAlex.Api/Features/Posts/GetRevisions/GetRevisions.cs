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
        app.MapGet("/{folder}/revisions", (string folder, string? since, HttpContext http, IOptions<GithubOptions> github,
                GetRevisionsHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(folder, out var path), path, since, http, github, handler, ct))
            .WithSummary("Commits changing blogs/{folder}/index.md, newest first, optionally since a date (cached 10 min)");

        app.MapGet("/{series}/{part}/revisions", (string series, string part, string? since, HttpContext http, IOptions<GithubOptions> github,
                GetRevisionsHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(series, part, out var path), path, since, http, github, handler, ct))
            .WithSummary("Commits changing blogs/{series}/{part}/index.md (a part of a series), newest first, optionally since a date (cached 10 min)");
    }

    private static async Task<IResult> HandleAsync(bool valid, PostPath path, string? since, HttpContext http, IOptions<GithubOptions> github,
        GetRevisionsHandler handler, CancellationToken ct)
    {
        DateOnly? sinceDate = null;
        if (since is not null)
        {
            if (!DateOnly.TryParseExact(since, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return ApiErrors.BadRequest();
            }
            sinceDate = parsed;
        }
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
            var revisions = await handler.HandleAsync(path, sinceDate, ct);
            http.Response.Headers.CacheControl = "public, max-age=600";
            return TypedResults.Ok(revisions);
        }, notFoundMessage: "post not found");
    }
}

/// <summary>
/// A post's revisions: the commits that changed its <c>index.md</c>, so <c>.meta</c> tweaks and image
/// uploads don't show up. Commits with <see cref="SkipMarker"/> in their message are left out, and
/// <c>since</c> (the post's <c>revisions-since:</c>) drops everything before that day.
/// </summary>
public sealed class GetRevisionsHandler(GithubContentClient github, PostsCache cache)
{
    public const int MaxRevisions = 30;
    public const string SkipMarker = "[skip rev]";

    // skipped commits are filtered out after fetching, so read a few pages to still fill the list
    private const int PageSize = 100;
    private const int MaxPages = 3;

    public Task<GetRevisionsResponse> HandleAsync(PostPath post, DateOnly? since = null, CancellationToken ct = default)
    {
        var sinceText = since?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return cache.GetOrCreateAsync($"revisions:{post}:{sinceText}", PostsCache.RevisionsLifetime, async () =>
        {
            var path = $"{github.BlogsPath}/{post}/index.md";
            var query = $"path={Uri.EscapeDataString(path)}&per_page={PageSize}";
            if (sinceText is not null)
            {
                query += $"&since={sinceText}T00:00:00Z";
            }

            var revisions = new List<PostRevision>();
            for (var page = 1; page <= MaxPages && revisions.Count < MaxRevisions; page++)
            {
                using var response = await github.GetAsync($"repos/{github.Repo}/commits?{query}&page={page}", GithubContentClient.JsonMediaType, ct);
                var commits = await response.Content.ReadFromJsonAsync(GithubCommitsJsonContext.Default.ListGithubCommit, ct) ?? [];
                revisions.AddRange(commits.Where(c => !IsSkipped(c.Commit.Message)).Select(ToRevision));
                if (commits.Count < PageSize)
                {
                    break;
                }
            }
            return new GetRevisionsResponse(revisions.Take(MaxRevisions).ToList());
        }, value => 256L * value.Revisions.Count);
    }

    internal static bool IsSkipped(string message) => message.Contains(SkipMarker, StringComparison.OrdinalIgnoreCase);

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
