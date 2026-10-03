using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.ListPendingEntries;

/// <summary>Used by the <c>/guestbook-pending</c> Discord command.</summary>
public sealed class ListPendingEntriesHandler(IUnitOfWorkFactory database)
{
    public async Task<IReadOnlyList<GuestbookEntry>> HandleAsync(CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        return await uow.Guestbook.ListPendingAsync(ct);
    }
}
