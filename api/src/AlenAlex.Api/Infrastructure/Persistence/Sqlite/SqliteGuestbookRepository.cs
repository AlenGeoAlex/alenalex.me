using System.Globalization;
using AlenAlex.Api.Features.Guestbook.Shared;
using Microsoft.Data.Sqlite;

namespace AlenAlex.Api.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Timestamps are text and compared with <c>julianday()</c>, which handles both formats in the
/// existing database (see <see cref="FormatTimestamp"/>). Every query is a <c>const</c> with typed
/// parameters; CA2100 fails the build otherwise.
/// </summary>
public sealed class SqliteGuestbookRepository(ISqliteSession session) : IGuestbookRepository
{
    // Entry queries all select the columns in the order ReadEntriesAsync expects.

    private const string ListVisibleSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               EXISTS (SELECT 1 FROM guestbook_likes l WHERE l.entry_id = e.id AND l.ip_hash = $viewer) AS liked
        FROM guestbook_entries e
        WHERE e.status = 'accepted' OR (e.status = 'pending_approval' AND e.ip_hash = $viewer)
        ORDER BY julianday(e.created_at) DESC, e.seq DESC
        """;

    private const string ListPendingSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               0 AS liked
        FROM guestbook_entries e
        WHERE e.status = 'pending_approval'
        ORDER BY julianday(e.created_at) ASC
        """;

    private const string GetByIdSql =
        """
        SELECT e.id, e.seq, e.name, e.message, e.status, e.created_at,
               (SELECT COUNT(*) FROM guestbook_likes l WHERE l.entry_id = e.id) AS like_count,
               EXISTS (SELECT 1 FROM guestbook_likes l WHERE l.entry_id = e.id AND l.ip_hash = $viewer) AS liked
        FROM guestbook_entries e
        WHERE e.id = $id
        """;

    private const string CountCreatedSinceSql =
        """
        SELECT COUNT(*) FROM guestbook_entries
        WHERE ip_hash = $ip AND julianday(created_at) > julianday($since)
        """;

    // A single statement, so computing seq is atomic.
    private const string InsertSql =
        """
        INSERT INTO guestbook_entries (id, seq, name, message, status, ip_hash, created_at)
        VALUES ($id, (SELECT COALESCE(MAX(seq), 0) + 1 FROM guestbook_entries), $name, $message, 'pending_approval', $ip, $created_at)
        RETURNING seq
        """;

    private const string GetStatusSql = "SELECT status FROM guestbook_entries WHERE id = $id";

    private const string SetStatusIfPendingSql =
        """
        UPDATE guestbook_entries SET status = $status, rejection_reason = $reason
        WHERE id = $id AND status = 'pending_approval'
        """;

    private const string AddLikeSql =
        """
        INSERT INTO guestbook_likes (entry_id, ip_hash, created_at) VALUES ($entry, $ip, $created_at)
        ON CONFLICT (entry_id, ip_hash) DO NOTHING
        """;

    private const string RemoveLikeSql = "DELETE FROM guestbook_likes WHERE entry_id = $entry AND ip_hash = $ip";

    private const string GetLikeStateSql =
        """
        SELECT (SELECT COUNT(*) FROM guestbook_likes WHERE entry_id = $entry),
               EXISTS (SELECT 1 FROM guestbook_likes WHERE entry_id = $entry AND ip_hash = $ip)
        """;

    public async Task<IReadOnlyList<GuestbookEntry>> ListVisibleAsync(string viewerIpHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = ListVisibleSql;
        command.WithText("$viewer", viewerIpHash);
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
        command.WithText("$id", id).WithText("$viewer", viewerIpHash);
        var entries = await ReadEntriesAsync(command, ct);
        return entries.Count > 0 ? entries[0] : null;
    }

    public async Task<int> CountCreatedSinceAsync(string ipHash, DateTimeOffset since, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = CountCreatedSinceSql;
        command.WithText("$ip", ipHash).WithText("$since", FormatTimestamp(since));
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    public async Task<long> InsertAsync(NewGuestbookEntry entry, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = InsertSql;
        command.WithText("$id", entry.Id)
            .WithText("$name", entry.Name)
            .WithText("$message", entry.Message)
            .WithText("$ip", entry.IpHash)
            .WithText("$created_at", FormatTimestamp(entry.CreatedAt));
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<GuestbookStatus?> GetStatusAsync(string id, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = GetStatusSql;
        command.WithText("$id", id);
        return await command.ExecuteScalarAsync(ct) is string status ? ParseStatus(status) : null;
    }

    public async Task<bool> SetStatusIfPendingAsync(string id, GuestbookStatus status, string? rejectionReason, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = SetStatusIfPendingSql;
        command.WithText("$status", ToDbString(status)).WithText("$reason", rejectionReason).WithText("$id", id);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task AddLikeAsync(string entryId, string ipHash, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = AddLikeSql;
        command.WithText("$entry", entryId).WithText("$ip", ipHash).WithText("$created_at", FormatTimestamp(createdAt));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveLikeAsync(string entryId, string ipHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = RemoveLikeSql;
        command.WithText("$entry", entryId).WithText("$ip", ipHash);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<LikeState> GetLikeStateAsync(string entryId, string ipHash, CancellationToken ct = default)
    {
        await using var command = session.CreateCommand();
        command.CommandText = GetLikeStateSql;
        command.WithText("$entry", entryId).WithText("$ip", ipHash);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new LikeState(reader.GetInt64(0), reader.GetInt64(1) != 0);
    }

    private static async Task<IReadOnlyList<GuestbookEntry>> ReadEntriesAsync(SqliteCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new List<GuestbookEntry>();
        while (await reader.ReadAsync(ct))
        {
            entries.Add(new GuestbookEntry(
                Id: reader.GetString(0),
                Seq: reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                Name: reader.GetString(2),
                Message: reader.GetString(3),
                Status: ParseStatus(reader.GetString(4)),
                CreatedAt: ParseTimestamp(reader.GetString(5)),
                LikeCount: reader.GetInt64(6),
                Liked: reader.GetInt64(7) != 0));
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
        // Anything unrecognised counts as pending.
        _ => GuestbookStatus.PendingApproval,
    };

    /// <summary>
    /// <c>2026-10-02T20:17:01.393147+00:00</c>, matching the existing rows. The oldest rows use
    /// SQLite's <c>2026-07-11 10:00:00</c> (UTC); <see cref="ParseTimestamp"/> reads both.
    /// </summary>
    internal static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'+00:00'", CultureInfo.InvariantCulture);

    internal static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
}
