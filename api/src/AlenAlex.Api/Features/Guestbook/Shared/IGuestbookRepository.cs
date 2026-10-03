namespace AlenAlex.Api.Features.Guestbook.Shared;

/// <summary>
/// Obtained from <c>IUnitOfWork.Guestbook</c>, so every call shares the unit of work's
/// connection and transaction.
/// </summary>
public interface IGuestbookRepository
{
    /// <summary>Accepted entries plus <paramref name="viewerIpHash"/>'s own pending ones, newest first.</summary>
    Task<IReadOnlyList<GuestbookEntry>> ListVisibleAsync(string viewerIpHash, CancellationToken ct = default);

    /// <summary>Entries awaiting moderation, oldest first (<c>Liked</c> is always false).</summary>
    Task<IReadOnlyList<GuestbookEntry>> ListPendingAsync(CancellationToken ct = default);

    /// <summary>One entry in any status, as seen by <paramref name="viewerIpHash"/>.</summary>
    Task<GuestbookEntry?> GetByIdAsync(string id, string viewerIpHash, CancellationToken ct = default);

    /// <summary>Entries created by <paramref name="ipHash"/> after <paramref name="since"/> (rate limiting).</summary>
    Task<int> CountCreatedSinceAsync(string ipHash, DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Stores a pending entry with the next sequence number; returns that number.</summary>
    Task<long> InsertAsync(NewGuestbookEntry entry, CancellationToken ct = default);

    /// <summary>Current status, or <c>null</c> if the entry does not exist.</summary>
    Task<GuestbookStatus?> GetStatusAsync(string id, CancellationToken ct = default);

    /// <summary>Moves a <em>pending</em> entry to <paramref name="status"/>. False if unknown or not pending.</summary>
    Task<bool> SetStatusIfPendingAsync(string id, GuestbookStatus status, string? rejectionReason, CancellationToken ct = default);

    /// <summary>Adds a like; a second like from the same IP hash is ignored.</summary>
    Task AddLikeAsync(string entryId, string ipHash, DateTimeOffset createdAt, CancellationToken ct = default);

    /// <summary>Removes a like if present.</summary>
    Task RemoveLikeAsync(string entryId, string ipHash, CancellationToken ct = default);

    Task<LikeState> GetLikeStateAsync(string entryId, string ipHash, CancellationToken ct = default);
}

public sealed record NewGuestbookEntry(string Id, string Name, string Message, string IpHash, DateTimeOffset CreatedAt);

/// <summary>Like count of one entry and whether one visitor liked it.</summary>
public sealed record LikeState(long LikeCount, bool Liked);
