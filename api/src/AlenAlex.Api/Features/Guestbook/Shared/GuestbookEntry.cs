namespace AlenAlex.Api.Features.Guestbook.Shared;

/// <summary>Moderation state as stored in the <c>status</c> column.</summary>
public enum GuestbookStatus
{
    PendingApproval,
    Accepted,
    Rejected,
}

/// <summary>An entry as seen by one visitor; <see cref="Liked"/> is relative to that visitor.</summary>
public sealed record GuestbookEntry(
    string Id,
    long Seq,
    string Name,
    string Message,
    GuestbookStatus Status,
    DateTimeOffset CreatedAt,
    long LikeCount,
    bool Liked);
