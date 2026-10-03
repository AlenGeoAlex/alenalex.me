using AlenAlex.Api.Features.Guestbook.Shared;
using Npgsql;

namespace AlenAlex.Api.Infrastructure.Persistence.Postgres;

/// <summary>
/// Every method runs exactly one <c>const</c> query (CA2100 fails the build otherwise) and binds
/// every value as a typed parameter.
/// </summary>
public sealed class PostgresGuestbookRepository(INpgsqlSession session) : IGuestbookRepository
{
    // Entry queries select the same columns in the order ReadEntriesAsync expects.

    private const string ListVisibleSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               EXISTS (SELECT 1 FROM guestbook_likes l WHERE l.entry_id = e.id AND l.ip_hash = @viewer) AS liked
        FROM guestbook_entries e
        WHERE e.status = 'accepted' OR (e.status = 'pending_approval' AND e.ip_hash = @viewer)
        ORDER BY e.created_at DESC, e.seq DESC
        """;

    private const string ListPendingSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               FALSE AS liked
        FROM guestbook_entries e
        WHERE e.status = 'pending_approval'
        ORDER BY e.created_at ASC, e.seq ASC
        """;

    private const string GetByIdSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               EXISTS (SELECT 1 FROM guestbook_likes l WHERE l.entry_id = e.id AND l.ip_hash = @viewer) AS liked
        FROM guestbook_entries e
        WHERE e.id = @id
        """;

    // transaction-scoped advisory lock keyed on the visitor; released on commit/rollback
    private const string LockVisitorSql = "SELECT pg_advisory_xact_lock(hashtextextended(@ip, 0))";

    private const string CountCreatedSinceSql =
        "SELECT COUNT(*) FROM guestbook_entries WHERE ip_hash = @ip AND created_at > @since";

    // seq comes from the column's identity sequence.
    private const string InsertSql =
        """
        INSERT INTO guestbook_entries (id, name, message, status, ip_hash, created_at)
        VALUES (@id, @name, @message, 'pending_approval', @ip, @created_at)
        RETURNING seq
        """;

    private const string GetStatusSql = "SELECT status FROM guestbook_entries WHERE id = @id";

    private const string SetStatusIfPendingSql =
        """
        UPDATE guestbook_entries SET status = @status, rejection_reason = @reason
        WHERE id = @id AND status = 'pending_approval'
        """;

    private const string AddLikeSql =
        """
        INSERT INTO guestbook_likes (entry_id, ip_hash, created_at) VALUES (@entry, @ip, @created_at)
        ON CONFLICT (entry_id, ip_hash) DO NOTHING
        """;

    private const string RemoveLikeSql = "DELETE FROM guestbook_likes WHERE entry_id = @entry AND ip_hash = @ip";

    private const string GetLikeStateSql =
        """
        SELECT (SELECT COUNT(*) FROM guestbook_likes WHERE entry_id = @entry),
               EXISTS (SELECT 1 FROM guestbook_likes WHERE entry_id = @entry AND ip_hash = @ip)
        """;

    public async Task<IReadOnlyList<GuestbookEntry>> ListVisibleAsync(string viewerIpHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = ListVisibleSql;
        command.WithText("viewer", viewerIpHash);
        return await ReadEntriesAsync(command, ct);
    }

    public async Task<IReadOnlyList<GuestbookEntry>> ListPendingAsync(CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = ListPendingSql;
        return await ReadEntriesAsync(command, ct);
    }

    public async Task<GuestbookEntry?> GetByIdAsync(string id, string viewerIpHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = GetByIdSql;
        command.WithText("id", id).WithText("viewer", viewerIpHash);
        var entries = await ReadEntriesAsync(command, ct);
        return entries.Count > 0 ? entries[0] : null;
    }

    public async Task LockVisitorAsync(string ipHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = LockVisitorSql;
        command.WithText("ip", ipHash);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> CountCreatedSinceAsync(string ipHash, DateTimeOffset since, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = CountCreatedSinceSql;
        command.WithText("ip", ipHash).WithTimestamp("since", since);
        return (int)(long)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<long> InsertAsync(NewGuestbookEntry entry, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = InsertSql;
        command.WithText("id", entry.Id)
            .WithText("name", entry.Name)
            .WithText("message", entry.Message)
            .WithText("ip", entry.IpHash)
            .WithTimestamp("created_at", entry.CreatedAt);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<GuestbookStatus?> GetStatusAsync(string id, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = GetStatusSql;
        command.WithText("id", id);
        return await command.ExecuteScalarAsync(ct) is string status ? ParseStatus(status) : null;
    }

    public async Task<bool> SetStatusIfPendingAsync(string id, GuestbookStatus status, string? rejectionReason, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = SetStatusIfPendingSql;
        command.WithText("status", ToDbString(status)).WithText("reason", rejectionReason).WithText("id", id);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task AddLikeAsync(string entryId, string ipHash, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = AddLikeSql;
        command.WithText("entry", entryId).WithText("ip", ipHash).WithTimestamp("created_at", createdAt);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveLikeAsync(string entryId, string ipHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = RemoveLikeSql;
        command.WithText("entry", entryId).WithText("ip", ipHash);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<LikeState> GetLikeStateAsync(string entryId, string ipHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = GetLikeStateSql;
        command.WithText("entry", entryId).WithText("ip", ipHash);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new LikeState(reader.GetInt64(0), reader.GetBoolean(1));
    }

    private static async Task<IReadOnlyList<GuestbookEntry>> ReadEntriesAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new List<GuestbookEntry>();
        while (await reader.ReadAsync(ct))
        {
            entries.Add(new GuestbookEntry(
                Id: reader.GetString(0),
                Seq: reader.GetInt64(1),
                Name: reader.GetString(2),
                Message: reader.GetString(3),
                Status: ParseStatus(reader.GetString(4)),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(5),
                LikeCount: reader.GetInt64(6),
                Liked: reader.GetBoolean(7)));
        }
        return entries;
    }

    internal static string ToDbString(GuestbookStatus status) => status switch
    {
        GuestbookStatus.PendingApproval => "pending_approval",
        GuestbookStatus.Accepted => "accepted",
        GuestbookStatus.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    internal static GuestbookStatus ParseStatus(string value) => value switch
    {
        "accepted" => GuestbookStatus.Accepted,
        "rejected" => GuestbookStatus.Rejected,
        _ => GuestbookStatus.PendingApproval,
    };
}
