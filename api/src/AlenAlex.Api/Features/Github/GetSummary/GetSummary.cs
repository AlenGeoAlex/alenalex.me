using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AlenAlex.Api.Features.Github.GetSummary;

public sealed class GetSummaryEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/github", (GithubSummaryStore store) => Handle(store))
            .WithTags("dashboard")
            .WithSummary("Cached GitHub summary (refreshed every 15 minutes); 503 until the first fetch or without Github:Token");

    public static Results<Ok<GithubSummary>, JsonHttpResult<ErrorResponse>> Handle(GithubSummaryStore store) =>
        store.Current is { } summary
            ? TypedResults.Ok(summary)
            : ApiErrors.Create(StatusCodes.Status503ServiceUnavailable, "warming up");
}
