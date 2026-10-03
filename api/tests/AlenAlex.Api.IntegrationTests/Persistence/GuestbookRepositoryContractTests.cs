using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.IntegrationTests.Persistence;

/// <summary>
/// Shared by every provider. To test another one, subclass this and return a migrated database
/// from <see cref="CreateDatabaseAsync"/>, like <see cref="PostgresGuestbookRepositoryTests"/>.
/// </summary>
public abstract class GuestbookRepositoryContractTests : IAsyncLifetime
{
    protected const string Alice = "hash-alice";
    protected const string Bob = "hash-bob";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected IUnitOfWorkFactory Database { get; private set; } = null!;

    protected abstract Task<IUnitOfWorkFactory> CreateDatabaseAsync();

    public virtual async ValueTask InitializeAsync() => Database = await CreateDatabaseAsync();

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<string> InsertAsync(string ipHash, DateTimeOffset createdAt, GuestbookStatus? moderateTo = null, string name = "Ada")
    {
        var id = Guid.NewGuid().ToString("N")[..21];
        await using var uow = await Database.CreateAsync();
        await uow.BeginAsync();
        await uow.Guestbook.InsertAsync(new NewGuestbookEntry(id, name, "hello there", ipHash, createdAt));
        if (moderateTo is { } status)
        {
            Assert.True(await uow.Guestbook.SetStatusIfPendingAsync(id, status, null));
        }
        await uow.CommitAsync();
        return id;
    }

    [Fact]
    public async Task Insert_assigns_sequential_numbers_and_reads_back()
    {
        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await uow.Guestbook.InsertAsync(new NewGuestbookEntry("id-1", "Ada", "first", Alice, T0), TestContext.Current.CancellationToken));
        Assert.Equal(2, await uow.Guestbook.InsertAsync(new NewGuestbookEntry("id-2", "Bob", "second", Bob, T0.AddMinutes(1)), TestContext.Current.CancellationToken));
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        var entry = await uow.Guestbook.GetByIdAsync("id-1", Alice, TestContext.Current.CancellationToken);
        Assert.Equal(new GuestbookEntry("id-1", 1, "Ada", "first", GuestbookStatus.PendingApproval, T0, 0, false), entry);
        Assert.Equal(TimeSpan.Zero, entry!.CreatedAt.Offset);
        Assert.Null(await uow.Guestbook.GetByIdAsync("nope", Alice, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Visible_list_is_accepted_plus_own_pending_newest_first_never_rejected()
    {
        var accepted1 = await InsertAsync(Bob, T0, GuestbookStatus.Accepted);
        var alicePending = await InsertAsync(Alice, T0.AddMinutes(1));
        var bobPending = await InsertAsync(Bob, T0.AddMinutes(2));
        await InsertAsync(Alice, T0.AddMinutes(3), GuestbookStatus.Rejected);
        var accepted2 = await InsertAsync(Bob, T0.AddMinutes(4), GuestbookStatus.Accepted);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        Assert.Equal([accepted2, alicePending, accepted1], (await uow.Guestbook.ListVisibleAsync(Alice, TestContext.Current.CancellationToken)).Select(e => e.Id));
        Assert.Equal([accepted2, bobPending, accepted1], (await uow.Guestbook.ListVisibleAsync(Bob, TestContext.Current.CancellationToken)).Select(e => e.Id));
        Assert.Equal([accepted2, accepted1], (await uow.Guestbook.ListVisibleAsync("someone-else", TestContext.Current.CancellationToken)).Select(e => e.Id));
    }

    [Fact]
    public async Task Pending_list_is_oldest_first_without_viewer()
    {
        var first = await InsertAsync(Alice, T0);
        var second = await InsertAsync(Bob, T0.AddMinutes(1));
        await InsertAsync(Bob, T0.AddMinutes(2), GuestbookStatus.Accepted);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        var pending = await uow.Guestbook.ListPendingAsync(TestContext.Current.CancellationToken);
        Assert.Equal([first, second], pending.Select(e => e.Id));
        Assert.All(pending, e => Assert.False(e.Liked));
    }

    [Fact]
    public async Task Counts_entries_created_since_a_time_per_ip_hash()
    {
        await InsertAsync(Alice, T0);
        await InsertAsync(Alice, T0.AddMinutes(30));
        await InsertAsync(Alice, T0.AddMinutes(90));
        await InsertAsync(Bob, T0.AddMinutes(90));

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, await uow.Guestbook.CountCreatedSinceAsync(Alice, T0.AddMinutes(10), TestContext.Current.CancellationToken));
        Assert.Equal(1, await uow.Guestbook.CountCreatedSinceAsync(Alice, T0.AddMinutes(60), TestContext.Current.CancellationToken));
        Assert.Equal(0, await uow.Guestbook.CountCreatedSinceAsync("nobody", T0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Only_pending_entries_change_status()
    {
        var id = await InsertAsync(Alice, T0);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GuestbookStatus.PendingApproval, await uow.Guestbook.GetStatusAsync(id, TestContext.Current.CancellationToken));
        Assert.True(await uow.Guestbook.SetStatusIfPendingAsync(id, GuestbookStatus.Rejected, "spam", TestContext.Current.CancellationToken));
        Assert.False(await uow.Guestbook.SetStatusIfPendingAsync(id, GuestbookStatus.Accepted, null, TestContext.Current.CancellationToken));
        Assert.False(await uow.Guestbook.SetStatusIfPendingAsync("nope", GuestbookStatus.Accepted, null, TestContext.Current.CancellationToken));
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GuestbookStatus.Rejected, await uow.Guestbook.GetStatusAsync(id, TestContext.Current.CancellationToken));
        Assert.Null(await uow.Guestbook.GetStatusAsync("nope", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Likes_are_idempotent_and_per_viewer()
    {
        var id = await InsertAsync(Alice, T0, GuestbookStatus.Accepted);

        await using var uow = await Database.CreateAsync(TestContext.Current.CancellationToken);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await uow.Guestbook.AddLikeAsync(id, Bob, T0, TestContext.Current.CancellationToken);
        await uow.Guestbook.AddLikeAsync(id, Bob, T0, TestContext.Current.CancellationToken);
        Assert.Equal(new LikeState(1, true), await uow.Guestbook.GetLikeStateAsync(id, Bob, TestContext.Current.CancellationToken));
        Assert.Equal(new LikeState(1, false), await uow.Guestbook.GetLikeStateAsync(id, Alice, TestContext.Current.CancellationToken));
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        var asBob = Assert.Single(await uow.Guestbook.ListVisibleAsync(Bob, TestContext.Current.CancellationToken));
        Assert.Equal((1, true), (asBob.LikeCount, asBob.Liked));
        var asAlice = Assert.Single(await uow.Guestbook.ListVisibleAsync(Alice, TestContext.Current.CancellationToken));
        Assert.Equal((1, false), (asAlice.LikeCount, asAlice.Liked));

        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await uow.Guestbook.RemoveLikeAsync(id, Bob, TestContext.Current.CancellationToken);
        await uow.Guestbook.RemoveLikeAsync(id, Bob, TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new LikeState(0, false), await uow.Guestbook.GetLikeStateAsync(id, Bob, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disposing_without_commit_rolls_back()
    {
        await using (var uow = await Database.CreateAsync(TestContext.Current.CancellationToken))
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.Guestbook.InsertAsync(new NewGuestbookEntry("lost", "Ada", "never saved", Alice, T0), TestContext.Current.CancellationToken);
        }

        await using (var uow = await Database.CreateAsync(TestContext.Current.CancellationToken))
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.Guestbook.InsertAsync(new NewGuestbookEntry("also-lost", "Ada", "rolled back", Alice, T0), TestContext.Current.CancellationToken);
            await uow.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.Null(await uow.Guestbook.GetByIdAsync("also-lost", Alice, TestContext.Current.CancellationToken));
        }

        await using var check = await Database.CreateAsync(TestContext.Current.CancellationToken);
        Assert.Null(await check.Guestbook.GetByIdAsync("lost", Alice, TestContext.Current.CancellationToken));
        Assert.Empty(await check.Guestbook.ListPendingAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Classic injection payloads; every value must be stored and returned verbatim.</summary>
    public static TheoryData<string> InjectionPayloads =>
    [
        "'); DROP TABLE guestbook_entries;--",
        "\" OR 1=1 --",
        "' OR '1'='1",
        "$viewer",
        "@id; DELETE FROM guestbook_likes; --",
        "Robert'); DROP TABLE guestbook_likes; --",
    ];

    [Theory]
    [MemberData(nameof(InjectionPayloads))]
    public async Task Injection_payloads_are_stored_verbatim_and_tables_stay_intact(string payload)
    {
        var ct = TestContext.Current.CancellationToken;
        var bystander = await InsertAsync(Bob, T0, GuestbookStatus.Accepted);

        await using var uow = await Database.CreateAsync(ct);
        await uow.BeginAsync(ct);
        // The payload as id, name, message, ip hash and rejection reason.
        await uow.Guestbook.InsertAsync(new NewGuestbookEntry(payload, payload, payload, payload, T0.AddMinutes(1)), ct);
        await uow.Guestbook.AddLikeAsync(bystander, payload, T0, ct);
        await uow.CommitAsync(ct);

        var stored = await uow.Guestbook.GetByIdAsync(payload, payload, ct);
        Assert.NotNull(stored);
        Assert.Equal((payload, payload, payload), (stored.Id, stored.Name, stored.Message));

        // The payload as the viewer hash: sees exactly the bystander and its own pending entry.
        Assert.Equal([payload, bystander], (await uow.Guestbook.ListVisibleAsync(payload, ct)).Select(e => e.Id));
        Assert.Equal(new LikeState(1, true), await uow.Guestbook.GetLikeStateAsync(bystander, payload, ct));
        Assert.Equal(1, await uow.Guestbook.CountCreatedSinceAsync(payload, T0, ct));
        Assert.Null(await uow.Guestbook.GetByIdAsync(payload + "x", payload, ct));

        await uow.BeginAsync(ct);
        Assert.True(await uow.Guestbook.SetStatusIfPendingAsync(payload, GuestbookStatus.Rejected, payload, ct));
        await uow.Guestbook.RemoveLikeAsync(bystander, payload, ct);
        await uow.CommitAsync(ct);

        // Both tables still exist and hold exactly what was written.
        Assert.Equal(GuestbookStatus.Rejected, await uow.Guestbook.GetStatusAsync(payload, ct));
        Assert.Equal(GuestbookStatus.Accepted, await uow.Guestbook.GetStatusAsync(bystander, ct));
        Assert.Equal(new LikeState(0, false), await uow.Guestbook.GetLikeStateAsync(bystander, payload, ct));
        Assert.Equal([bystander], (await uow.Guestbook.ListVisibleAsync(Alice, ct)).Select(e => e.Id));
    }
}
