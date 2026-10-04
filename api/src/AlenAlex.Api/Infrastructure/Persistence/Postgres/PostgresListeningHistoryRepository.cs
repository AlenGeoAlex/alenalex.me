using AlenAlex.Api.Features.Status.Shared;

namespace AlenAlex.Api.Infrastructure.Persistence.Postgres;

/// <summary>Same rules as <see cref="PostgresGuestbookRepository"/>: const SQL, typed parameters.</summary>
public sealed class PostgresListeningHistoryRepository(INpgsqlSession session) : IListeningHistoryRepository
{
    private const string ListRecentSql =
        """
        SELECT track, artist, album, art_url, played_at
        FROM listening_history
        ORDER BY played_at DESC, id DESC
        LIMIT @limit
        """;

    private const string InsertSql =
        """
        INSERT INTO listening_history (track, artist, album, art_url, played_at)
        VALUES (@track, @artist, @album, @art_url, @played_at)
        """;

    private const string TrimSql =
        """
        DELETE FROM listening_history
        WHERE id NOT IN (SELECT id FROM listening_history ORDER BY played_at DESC, id DESC LIMIT @keep)
        """;

    public async Task<IReadOnlyList<RecentTrack>> ListRecentAsync(int limit, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = ListRecentSql;
        command.WithInt("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var tracks = new List<RecentTrack>();
        while (await reader.ReadAsync(ct))
        {
            tracks.Add(new RecentTrack(
                Track: reader.GetString(0),
                Artist: reader.GetString(1),
                Album: reader.IsDBNull(2) ? null : reader.GetString(2),
                ArtUrl: reader.IsDBNull(3) ? null : reader.GetString(3),
                PlayedAt: reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return tracks;
    }

    public async Task InsertAsync(RecentTrack track, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = InsertSql;
        command.WithText("track", track.Track)
            .WithText("artist", track.Artist)
            .WithText("album", track.Album)
            .WithText("art_url", track.ArtUrl)
            .WithTimestamp("played_at", track.PlayedAt);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task TrimAsync(int keep, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = TrimSql;
        command.WithInt("keep", keep);
        await command.ExecuteNonQueryAsync(ct);
    }
}
