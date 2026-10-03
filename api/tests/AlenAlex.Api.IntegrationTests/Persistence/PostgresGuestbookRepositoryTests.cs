using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.IntegrationTests.Support;
using Npgsql;

namespace AlenAlex.Api.IntegrationTests.Persistence;

/// <summary>Runs the repository contract against PostgreSQL (a fresh database migrated by FluentMigrator).</summary>
public sealed class PostgresGuestbookRepositoryTests : GuestbookRepositoryContractTests
{
    private TestDatabase? _db;

    protected override Task<IUnitOfWorkFactory> CreateDatabaseAsync()
    {
        _db = TestDatabase.Create();
        return Task.FromResult(_db.UnitOfWork);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Stores_canonical_status_strings_and_rejection_reason()
    {
        var id = await InsertAsync(Alice, DateTimeOffset.UtcNow);
        Assert.Equal("pending_approval", await _db!.ScalarAsync("SELECT status FROM guestbook_entries"));

        await using (var uow = await Database.CreateAsync(TestContext.Current.CancellationToken))
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.Guestbook.SetStatusIfPendingAsync(id, GuestbookStatus.Rejected, "spam", TestContext.Current.CancellationToken);
            await uow.CommitAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal("rejected", await _db.ScalarAsync("SELECT status FROM guestbook_entries"));
        Assert.Equal("spam", await _db.ScalarAsync("SELECT rejection_reason FROM guestbook_entries"));
    }

    [Fact]
    public async Task Rejects_unknown_status_values()
    {
        await InsertAsync(Alice, DateTimeOffset.UtcNow);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _db!.ExecuteAsync("UPDATE guestbook_entries SET status = 'pending'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Keeps_timestamps_to_the_microsecond_in_utc()
    {
        var createdAt = new DateTimeOffset(2026, 10, 2, 20, 17, 1, 393, 147, TimeSpan.Zero);
        var id = await InsertAsync(Alice, createdAt);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        var entry = await uow.Guestbook.GetByIdAsync(id, Alice, TestContext.Current.CancellationToken);
        Assert.Equal(createdAt, entry!.CreatedAt);
        Assert.Equal(TimeSpan.Zero, entry.CreatedAt.Offset);
    }

    [Fact]
    public async Task Deleting_an_entry_cascades_to_its_likes()
    {
        var id = await InsertAsync(Alice, DateTimeOffset.UtcNow, GuestbookStatus.Accepted);
        await using (var uow = await Database.CreateAsync(TestContext.Current.CancellationToken))
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.Guestbook.AddLikeAsync(id, Bob, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
            await uow.CommitAsync(TestContext.Current.CancellationToken);
        }

        await _db!.ExecuteAsync($"DELETE FROM guestbook_entries WHERE id = '{id}'");
        Assert.Equal(0L, await _db.ScalarAsync("SELECT COUNT(*) FROM guestbook_likes"));
    }

    /// <summary>The README import recipe: rows copied in with their own seq, then the identity is realigned.</summary>
    [Fact]
    public async Task Imported_rows_keep_their_seq_and_new_rows_continue_after_the_max()
    {
        await _db!.ExecuteAsync(
            """
            INSERT INTO guestbook_entries (id, seq, name, message, status, ip_hash, created_at) VALUES
              ('old-7', 7, 'Ada', 'imported', 'accepted', 'h1', '2026-07-18T13:39:36.985756+00:00'),
              ('old-15', 15, 'Bob', 'imported', 'accepted', 'h2', '2026-07-20 11:09:20.236045+00');
            SELECT setval(pg_get_serial_sequence('guestbook_entries', 'seq'), (SELECT MAX(seq) FROM guestbook_entries));
            """);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        var seq = await uow.Guestbook.InsertAsync(new NewGuestbookEntry("new", "Cy", "after import", Alice, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(16, seq);
        Assert.Equal([15L, 7L], (await uow.Guestbook.ListVisibleAsync(Bob, TestContext.Current.CancellationToken)).Select(e => e.Seq));
    }
}
