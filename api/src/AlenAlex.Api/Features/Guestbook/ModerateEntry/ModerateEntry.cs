using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.ModerateEntry;

/// <summary>No HTTP endpoint; driven by the Discord buttons and slash commands.</summary>
public sealed class ModerateEntryHandler(IUnitOfWorkFactory database)
{
    /// <exception cref="GuestbookEntryNotFoundException">Unknown, or no longer pending.</exception>
    public Task ApproveAsync(string id, CancellationToken ct = default) =>
        SetStatusAsync(id, GuestbookStatus.Accepted, reason: null, ct);

    /// <exception cref="GuestbookEntryNotFoundException">Unknown, or no longer pending.</exception>
    public Task RejectAsync(string id, string? reason, CancellationToken ct = default) =>
        SetStatusAsync(id, GuestbookStatus.Rejected, reason, ct);

    private async Task SetStatusAsync(string id, GuestbookStatus status, string? reason, CancellationToken ct)
    {
        await using var uow = await database.CreateAsync(ct);
        await uow.BeginAsync(ct);
        if (!await uow.Guestbook.SetStatusIfPendingAsync(id, status, reason, ct))
        {
            throw new GuestbookEntryNotFoundException();
        }
        await uow.CommitAsync(ct);
    }
}
