using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.UnlikeEntry;

public sealed class UnlikeEntryEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/{id}/likes", async (string id, HttpContext http, IClientIpResolver clientIp, UnlikeEntryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(LikeResponse.From(await handler.HandleAsync(id, clientIp.GetIpHash(http), ct))))
            .WithSummary("Remove a like (idempotent). 404 unless accepted");
}

public sealed class UnlikeEntryHandler(IUnitOfWorkFactory database)
{
    /// <exception cref="GuestbookEntryNotFoundException">Unknown or not accepted.</exception>
    public async Task<LikeState> HandleAsync(string entryId, string ipHash, CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        await uow.BeginAsync(ct);

        if (await uow.Guestbook.GetStatusAsync(entryId, ct) != GuestbookStatus.Accepted)
        {
            throw new GuestbookEntryNotFoundException();
        }
        await uow.Guestbook.RemoveLikeAsync(entryId, ipHash, ct);
        var state = await uow.Guestbook.GetLikeStateAsync(entryId, ipHash, ct);

        await uow.CommitAsync(ct);
        return state;
    }
}
