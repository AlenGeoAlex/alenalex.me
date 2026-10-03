using AlenAlex.Api.Features.Github.GetSummary;
using AlenAlex.Api.Features.Github.RefreshSummary;
using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Github;

public static class GithubFeature
{
    public static IServiceCollection AddGithub(this IServiceCollection services)
    {
        services.AddSingleton<GithubSummaryStore>();
        services.AddHttpClient<GithubGraphQlClient>(GithubHttp.Configure);
        services.AddHostedService<RefreshSummaryJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapGithub(this IEndpointRouteBuilder app) =>
        app.MapEndpoint<GetSummaryEndpoint>();
}
