using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.ListEntries;

public sealed record ListEntriesResponse(IReadOnlyList<EntryResponse> Entries);

public sealed class ListEntriesEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/", async (HttpContext http, IClientIpResolver clientIp, ListEntriesHandler handler, CancellationToken ct) =>
            {
                var entries = await handler.HandleAsync(clientIp.GetIpHash(http), ct);
                return TypedResults.Ok(new ListEntriesResponse(entries.Select(EntryResponse.From).OfType<EntryResponse>().ToList()));
            })
            .WithSummary("Accepted entries plus the caller's own pending ones, newest first");
}

public sealed class ListEntriesHandler(IUnitOfWorkFactory database)
{
    public async Task<IReadOnlyList<GuestbookEntry>> HandleAsync(string viewerIpHash, CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        return await uow.Guestbook.ListVisibleAsync(viewerIpHash, ct);
    }
}
