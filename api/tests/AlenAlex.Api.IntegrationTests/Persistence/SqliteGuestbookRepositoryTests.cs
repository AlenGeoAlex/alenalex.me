using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Persistence;

/// <summary>Runs the repository contract against SQLite (a temp file migrated by FluentMigrator).</summary>
public sealed class SqliteGuestbookRepositoryTests : GuestbookRepositoryContractTests
{
    private TestDatabase _db = null!;

    protected override Task<IUnitOfWorkFactory> CreateDatabaseAsync()
    {
        _db = TestDatabase.Create();
        return Task.FromResult(_db.UnitOfWork);
    }

    public override async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Reads_legacy_sqlite_timestamps()
    {
        var id = await InsertAsync(Alice, DateTimeOffset.UtcNow, GuestbookStatus.Accepted);
        await _db.ExecuteAsync($"UPDATE guestbook_entries SET created_at = '2026-07-11 10:00:00' WHERE id = '{id}'");

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        var listed = Assert.Single(await uow.Guestbook.ListVisibleAsync(Bob, TestContext.Current.CancellationToken));
        Assert.Equal(new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.Zero), listed.CreatedAt);
    }

    [Fact]
    public async Task Stores_timestamps_in_the_existing_rows_format()
    {
        await InsertAsync(Alice, new DateTimeOffset(2026, 10, 2, 20, 17, 1, 393, 147, TimeSpan.Zero));
        Assert.Equal("2026-10-02T20:17:01.393147+00:00", await _db.ScalarAsync("SELECT created_at FROM guestbook_entries"));
    }

    [Fact]
    public async Task Stores_canonical_status_strings_and_rejection_reason()
    {
        var id = await InsertAsync(Alice, DateTimeOffset.UtcNow);
        Assert.Equal("pending_approval", await _db.ScalarAsync("SELECT status FROM guestbook_entries"));

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
    public async Task Deleting_an_entry_cascades_to_its_likes()
    {
        var id = await InsertAsync(Alice, DateTimeOffset.UtcNow, GuestbookStatus.Accepted);
        await using (var uow = await Database.CreateAsync(TestContext.Current.CancellationToken))
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.Guestbook.AddLikeAsync(id, Bob, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
            await uow.CommitAsync(TestContext.Current.CancellationToken);
        }

        await _db.ExecuteAsync($"DELETE FROM guestbook_entries WHERE id = '{id}'");
        Assert.Equal(0L, await _db.ScalarAsync("SELECT COUNT(*) FROM guestbook_likes"));
    }
}
