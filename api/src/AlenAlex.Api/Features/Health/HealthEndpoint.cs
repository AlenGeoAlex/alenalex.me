using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Health;

public sealed class HealthEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/_health", () => "OK").ExcludeFromDescription();
}
