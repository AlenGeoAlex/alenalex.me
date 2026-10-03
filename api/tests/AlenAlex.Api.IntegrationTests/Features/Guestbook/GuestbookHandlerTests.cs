using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Features.Guestbook.LikeEntry;
using AlenAlex.Api.Features.Guestbook.ListEntries;
using AlenAlex.Api.Features.Guestbook.ListPendingEntries;
using AlenAlex.Api.Features.Guestbook.ModerateEntry;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Features.Guestbook.UnlikeEntry;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Features.Guestbook;

public sealed class GuestbookHandlerTests : IAsyncLifetime
{
    private const string Alice = "hash-alice";
    private const string Bob = "hash-bob";

    private TestDatabase _db = null!;
    private CreateEntryHandler _create = null!;
    private ListEntriesHandler _list = null!;
    private ListPendingEntriesHandler _pending = null!;
    private ModerateEntryHandler _moderate = null!;
    private LikeEntryHandler _like = null!;
    private UnlikeEntryHandler _unlike = null!;

    public ValueTask InitializeAsync()
    {
        _db = TestDatabase.Create();
        _create = new CreateEntryHandler(_db.UnitOfWork, TimeProvider.System);
        _list = new ListEntriesHandler(_db.UnitOfWork);
        _pending = new ListPendingEntriesHandler(_db.UnitOfWork);
        _moderate = new ModerateEntryHandler(_db.UnitOfWork);
        _like = new LikeEntryHandler(_db.UnitOfWork, TimeProvider.System);
        _unlike = new UnlikeEntryHandler(_db.UnitOfWork);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    private Task<GuestbookEntry> CreateAsync(string name, string message, string ipHash) =>
        _create.HandleAsync(new CreateEntryRequest(name, message), ipHash);

    [Fact]
    public async Task Create_list_like_unlike_roundtrip()
    {
        // Create: stored trimmed, pending, with a sequence number.
        var entry = await CreateAsync("  Alice ", "  hello from alice ", Alice);
        Assert.Equal("Alice", entry.Name);
        Assert.Equal("hello from alice", entry.Message);
        Assert.Equal(GuestbookStatus.PendingApproval, entry.Status);
        Assert.Equal(1, entry.Seq);
        Assert.Equal((0, false), (entry.LikeCount, entry.Liked));
        Assert.Equal(21, entry.Id.Length);

        // The author sees their own pending entry; others do not.
        Assert.Single(await _list.HandleAsync(Alice, TestContext.Current.CancellationToken));
        Assert.Empty(await _list.HandleAsync(Bob, TestContext.Current.CancellationToken));

        // Pending entries cannot be liked.
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _like.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));

        await _moderate.ApproveAsync(entry.Id, TestContext.Current.CancellationToken);
        // Approving twice fails: it is no longer pending.
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _moderate.ApproveAsync(entry.Id, TestContext.Current.CancellationToken));

        // Like is idempotent and reads back through the list.
        Assert.Equal(new LikeState(1, true), await _like.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));
        Assert.Equal(new LikeState(1, true), await _like.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));

        var asBob = Assert.Single(await _list.HandleAsync(Bob, TestContext.Current.CancellationToken));
        Assert.Equal((1, true), (asBob.LikeCount, asBob.Liked));
        var asAlice = Assert.Single(await _list.HandleAsync(Alice, TestContext.Current.CancellationToken));
        Assert.Equal((1, false), (asAlice.LikeCount, asAlice.Liked));

        // Unlike is idempotent too.
        Assert.Equal(new LikeState(0, false), await _unlike.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));
        Assert.Equal(new LikeState(0, false), await _unlike.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));
        Assert.Equal(0, (await _list.HandleAsync(Bob, TestContext.Current.CancellationToken))[0].LikeCount);

        // Unknown ids are not found.
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _like.HandleAsync("nope", Bob, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _unlike.HandleAsync("nope", Bob, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejected_entries_are_never_listed()
    {
        var entry = await CreateAsync("Mallory", "spam spam spam", Alice);
        await _moderate.RejectAsync(entry.Id, "spam", TestContext.Current.CancellationToken);

        Assert.Empty(await _list.HandleAsync(Alice, TestContext.Current.CancellationToken));
        Assert.Empty(await _list.HandleAsync(Bob, TestContext.Current.CancellationToken));
        Assert.Empty(await _pending.HandleAsync(TestContext.Current.CancellationToken));
        Assert.Equal("spam", await _db.ScalarAsync($"SELECT rejection_reason FROM guestbook_entries WHERE id = '{entry.Id}'"));
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _like.HandleAsync(entry.Id, Bob, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Newest_first_and_sequential_numbers()
    {
        for (var i = 1; i <= 3; i++)
        {
            var entry = await CreateAsync("Ada", $"message {i}", Alice);
            Assert.Equal(i, entry.Seq);
            await _moderate.ApproveAsync(entry.Id, TestContext.Current.CancellationToken);
        }

        Assert.Equal([3L, 2L, 1L], (await _list.HandleAsync(Bob, TestContext.Current.CancellationToken)).Select(e => e.Seq));
    }

    [Fact]
    public async Task Pending_list_is_oldest_first()
    {
        var first = await CreateAsync("Ada", "first one", Alice);
        var second = await CreateAsync("Bob", "second one", Bob);

        Assert.Equal([first.Id, second.Id], (await _pending.HandleAsync(TestContext.Current.CancellationToken)).Select(e => e.Id));
    }

    [Fact]
    public async Task Rate_limits_after_five_entries_per_hour()
    {
        for (var i = 0; i < 5; i++)
        {
            await CreateAsync("Ada", $"message {i}", Alice);
        }

        await Assert.ThrowsAsync<GuestbookRateLimitedException>(() => CreateAsync("Ada", "one too many", Alice));
        // Someone else is unaffected.
        await CreateAsync("Bob", "hi there", Bob);
    }

    [Fact]
    public async Task Simultaneous_posts_from_one_visitor_cannot_get_past_the_limit()
    {
        var attempts = Enumerable.Range(0, 10)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    await CreateAsync("Ada", $"burst {i}", Alice);
                    return true;
                }
                catch (GuestbookRateLimitedException)
                {
                    return false;
                }
            }, TestContext.Current.CancellationToken));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(CreateEntryHandler.MaxEntriesPerHour, results.Count(accepted => accepted));
        Assert.Equal(CreateEntryHandler.MaxEntriesPerHour, (await _list.HandleAsync(Alice, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Entries_older_than_an_hour_do_not_count_towards_the_rate_limit()
    {
        for (var i = 0; i < 5; i++)
        {
            await CreateAsync("Ada", $"message {i}", Alice);
        }
        await _db.ExecuteAsync("UPDATE guestbook_entries SET created_at = '2020-01-01T00:00:00.000000+00:00'");

        await CreateAsync("Ada", "allowed again", Alice);
    }

    [Fact]
    public async Task Rejects_invalid_input_without_writing()
    {
        await Assert.ThrowsAsync<GuestbookValidationException>(() => CreateAsync("Ada", "hi", Alice));
        await Assert.ThrowsAsync<GuestbookValidationException>(() => CreateAsync(" ", "hello", Alice));
        Assert.Equal(0L, await _db.ScalarAsync("SELECT COUNT(*) FROM guestbook_entries"));
    }
}
