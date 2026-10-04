namespace AlenAlex.Api.Features.Status.Shared;

/// <summary>Obtained from <c>IUnitOfWork.ListeningHistory</c>.</summary>
public interface IListeningHistoryRepository
{
    /// <summary>The newest <paramref name="limit"/> tracks, newest first.</summary>
    Task<IReadOnlyList<RecentTrack>> ListRecentAsync(int limit, CancellationToken ct = default);

    Task InsertAsync(RecentTrack track, CancellationToken ct = default);

    /// <summary>Deletes everything but the newest <paramref name="keep"/> tracks.</summary>
    Task TrimAsync(int keep, CancellationToken ct = default);
}
