namespace AlenAlex.Api.Infrastructure.Http;

/// <summary>Mapped explicitly per feature rather than by assembly scanning, which NativeAOT can't do.</summary>
public interface IEndpoint
{
    static abstract void MapEndpoint(IEndpointRouteBuilder app);
}

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : IEndpoint
    {
        TEndpoint.MapEndpoint(app);
        return app;
    }
}
