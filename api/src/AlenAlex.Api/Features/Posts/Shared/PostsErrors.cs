using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Posts.Shared;

/// <summary>GitHub answered 404 (or 422 for an unknown ref) for the requested post/file/ref.</summary>
public sealed class PostNotFoundException() : Exception("not found on GitHub");

/// <summary>The asset exceeds the 10 MB cap.</summary>
public sealed class PostAssetTooLargeException() : Exception("asset too large");

/// <summary>GitHub failed in some other way (rate limit, 5xx, network).</summary>
public sealed class GithubUpstreamException(string message, Exception? inner = null) : Exception(message, inner);

public static class PostsResults
{
    public const string ShaCacheControl = "public, max-age=86400";
    public const string BranchCacheControl = "public, max-age=120";

    public static string CacheControlFor(string gitRef) =>
        PostInput.IsCommitSha(gitRef) ? ShaCacheControl : BranchCacheControl;

    public static IResult NotConfigured() =>
        ApiErrors.Create(StatusCodes.Status503ServiceUnavailable, "github not configured");

    public static async Task<IResult> HandleGithubErrorsAsync(Func<Task<IResult>> action, string notFoundMessage)
    {
        try
        {
            return await action();
        }
        catch (PostNotFoundException)
        {
            return ApiErrors.Create(StatusCodes.Status404NotFound, notFoundMessage);
        }
        catch (PostAssetTooLargeException)
        {
            return ApiErrors.Create(StatusCodes.Status502BadGateway, "asset too large");
        }
        catch (GithubUpstreamException)
        {
            return ApiErrors.Create(StatusCodes.Status502BadGateway, "github unavailable");
        }
    }
}
