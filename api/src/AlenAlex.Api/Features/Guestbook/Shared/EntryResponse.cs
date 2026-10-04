using System.Text.Json.Serialization;

namespace AlenAlex.Api.Features.Guestbook.Shared;

[JsonConverter(typeof(JsonStringEnumConverter<EntryStatus>))]
public enum EntryStatus
{
    [JsonStringEnumMemberName("pending")] Pending,
    [JsonStringEnumMemberName("accepted")] Accepted,
}

/// <param name="Seq">Stable sequential number, shown on the site as gb·0042.</param>
/// <param name="Liked">Whether the caller has liked this entry.</param>
/// <param name="Reactions">The owner's reactions, oldest first (keys of <see cref="GuestbookReactions"/>).</param>
public sealed record EntryResponse(
    string Id,
    long Seq,
    string Name,
    string Message,
    EntryStatus Status,
    DateTimeOffset CreatedAt,
    long LikeCount,
    bool Liked,
    IReadOnlyList<string> Reactions)
{
    /// <summary><c>null</c> for rejected entries, which are never sent to clients.</summary>
    public static EntryResponse? From(GuestbookEntry entry) => entry.Status switch
    {
        GuestbookStatus.PendingApproval => Create(entry, EntryStatus.Pending),
        GuestbookStatus.Accepted => Create(entry, EntryStatus.Accepted),
        _ => null,
    };

    private static EntryResponse Create(GuestbookEntry e, EntryStatus status) =>
        // a reaction removed from GuestbookReactions may still be in the database
        new(e.Id, e.Seq, e.Name, e.Message, status, e.CreatedAt, e.LikeCount, e.Liked, e.Reactions.Where(GuestbookReactions.IsKnown).ToList());
}

public sealed record LikeResponse(long LikeCount, bool Liked)
{
    public static LikeResponse From(LikeState state) => new(state.LikeCount, state.Liked);
}
