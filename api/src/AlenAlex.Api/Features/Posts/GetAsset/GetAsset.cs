using AlenAlex.Api.Features.Posts.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Posts.GetAsset;

public sealed record PostAsset(byte[] Content, string ContentType);

public sealed class GetAssetEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Also serves series-level assets such as the cover.
        app.MapGet("/{folder}/assets/{file}", (string folder, string file, [FromQuery(Name = "ref")] string? gitRef,
                HttpContext http, IOptions<GithubOptions> github, GetAssetHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(folder, out var path), path, file, gitRef, http, github, handler, ct))
            .WithSummary("Raw bytes of blogs/{folder}/assets/{file} at a ref (images only, max 10 MB)");

        app.MapGet("/{series}/{part}/assets/{file}", (string series, string part, string file, [FromQuery(Name = "ref")] string? gitRef,
                HttpContext http, IOptions<GithubOptions> github, GetAssetHandler handler, CancellationToken ct) =>
                HandleAsync(PostPath.TryCreate(series, part, out var path), path, file, gitRef, http, github, handler, ct))
            .WithSummary("Raw bytes of blogs/{series}/{part}/assets/{file} at a ref (images only, max 10 MB)");
    }

    private static async Task<IResult> HandleAsync(bool valid, PostPath path, string file, string? gitRef,
        HttpContext http, IOptions<GithubOptions> github, GetAssetHandler handler, CancellationToken ct)
    {
        var resolvedRef = PostInput.RefOrDefault(gitRef);
        var contentType = PostInput.IsValidName(file) ? GetAssetHandler.ImageContentType(file) : null;
        if (!valid || contentType is null || !PostInput.IsValidRef(resolvedRef))
        {
            return ApiErrors.BadRequest();
        }
        if (!github.Value.IsConfigured)
        {
            return PostsResults.NotConfigured();
        }

        return await PostsResults.HandleGithubErrorsAsync(async () =>
        {
            var asset = await handler.HandleAsync(path, file, contentType, resolvedRef, ct);
            var headers = http.Response.Headers;
            headers.CacheControl = PostsResults.CacheControlFor(resolvedRef);
            headers.XContentTypeOptions = "nosniff";
            // SVGs can carry script; don't let one run on the API's origin if opened directly.
            headers.ContentSecurityPolicy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; sandbox";
            return TypedResults.Bytes(asset.Content, asset.ContentType);
        }, notFoundMessage: "asset not found");
    }
}

public sealed class GetAssetHandler(GithubContentClient github, PostsCache cache)
{
    public const int MaxAssetBytes = 10 * 1024 * 1024;

    /// <summary>Media type for the image extensions posts may use; <c>null</c> for anything else.</summary>
    public static string? ImageContentType(string file) =>
        Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".avif" => "image/avif",
            _ => null,
        };

    /// <exception cref="PostAssetTooLargeException"/>
    public Task<PostAsset> HandleAsync(PostPath post, string file, string contentType, string gitRef, CancellationToken ct = default) =>
        cache.GetOrCreateAsync($"asset:{post}/assets/{file}@{gitRef}", PostsCache.LifetimeFor(gitRef), async () =>
        {
            using var response = await github.GetAsync(github.ContentsUrl($"{post}/assets/{file}", gitRef), GithubContentClient.RawMediaType, ct);
            if (response.Content.Headers.ContentLength > MaxAssetBytes)
            {
                throw new PostAssetTooLargeException();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaxAssetBytes)
                {
                    throw new PostAssetTooLargeException();
                }
                buffer.Write(chunk, 0, read);
            }
            return new PostAsset(buffer.ToArray(), contentType);
        }, value => value.Content.LongLength);
}
