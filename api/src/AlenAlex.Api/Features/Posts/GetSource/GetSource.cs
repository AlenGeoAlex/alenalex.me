using AlenAlex.Api.Features.Posts.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Posts.GetSource;

public sealed record GetSourceResponse(string Ref, string Meta, string Markdown);

public sealed class GetSourceEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/{folder}/source", (string folder, [FromQuery(Name = "ref")] string? gitRef, HttpContext http,
                IOptions<GithubOptions> github, GetSourceHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(folder, out var path), path, gitRef, http, github, handler, ct))
            .WithSummary("Raw .meta and index.md of a post at a ref (default main)");

        app.MapGet("/{series}/{part}/source", (string series, string part, [FromQuery(Name = "ref")] string? gitRef, HttpContext http,
                IOptions<GithubOptions> github, GetSourceHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(series, part, out var path), path, gitRef, http, github, handler, ct))
            .WithSummary("Raw .meta and index.md of a part of a series at a ref (default main)");
    }

    private static async Task<IResult> HandleAsync(bool valid, PostPath path, string? gitRef, HttpContext http,
        IOptions<GithubOptions> github, GetSourceHandler handler, CancellationToken ct)
    {
        var resolvedRef = PostInput.RefOrDefault(gitRef);
        if (!valid || !PostInput.IsValidRef(resolvedRef))
        {
            return ApiErrors.BadRequest();
        }
        if (!github.Value.IsConfigured)
        {
            return PostsResults.NotConfigured();
        }

        return await PostsResults.HandleGithubErrorsAsync(async () =>
        {
            var source = await handler.HandleAsync(path, resolvedRef, ct);
            http.Response.Headers.CacheControl = PostsResults.CacheControlFor(resolvedRef);
            return TypedResults.Ok(source);
        }, notFoundMessage: "post not found");
    }
}

public sealed class GetSourceHandler(GithubContentClient github, PostsCache cache)
{
    public Task<GetSourceResponse> HandleAsync(PostPath post, string gitRef, CancellationToken ct = default) =>
        cache.GetOrCreateAsync($"source:{post}@{gitRef}", PostsCache.LifetimeFor(gitRef), async () =>
        {
            var meta = github.GetRawTextAsync($"{post}/.meta", gitRef, ct);
            var markdown = github.GetRawTextAsync($"{post}/index.md", gitRef, ct);
            await Task.WhenAll(meta, markdown);
            return new GetSourceResponse(gitRef, await meta, await markdown);
        }, value => 2L * (value.Meta.Length + value.Markdown.Length));
}
