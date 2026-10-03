using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Features.Posts.GetAsset;
using AlenAlex.Api.Features.Posts.GetRevisions;
using AlenAlex.Api.Features.Posts.GetSource;
using AlenAlex.Api.Features.Posts.Shared;
using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Posts;

/// <summary>
/// Post history from the site repository, for showing revisions and previewing older versions.
/// Every slice validates input first (400), then requires GitHub to be configured (503).
/// </summary>
public static class PostsFeature
{
    public static IServiceCollection AddPosts(this IServiceCollection services)
    {
        services.AddSingleton<PostsCache>();
        services.AddHttpClient<GithubContentClient>(GithubHttp.Configure);
        services.AddTransient<GetRevisionsHandler>();
        services.AddTransient<GetSourceHandler>();
        services.AddTransient<GetAssetHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapPosts(this IEndpointRouteBuilder app)
    {
        // Standalone posts are blogs/{folder}; parts of a series are blogs/{series}/{part}.
        // Literal segments win in routing, so /api/posts/a/assets/x is always the asset route
        // and a part folder can't be named `assets`.
        var group = app.MapGroup("/api/posts").WithTags("posts");
        group.MapEndpoint<GetRevisionsEndpoint>();
        group.MapEndpoint<GetSourceEndpoint>();
        group.MapEndpoint<GetAssetEndpoint>();
        return app;
    }
}
