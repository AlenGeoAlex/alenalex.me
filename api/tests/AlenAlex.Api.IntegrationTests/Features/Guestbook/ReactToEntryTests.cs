using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Features.Guestbook.ListEntries;
using AlenAlex.Api.Features.Guestbook.ModerateEntry;
using AlenAlex.Api.Features.Guestbook.ReactToEntry;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Features.Guestbook;

public sealed class ReactToEntryTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private CreateEntryHandler _create = null!;
    private ModerateEntryHandler _moderate = null!;
    private ReactToEntryHandler _react = null!;

    public ValueTask InitializeAsync()
    {
        _db = TestDatabase.Create();
        _create = new CreateEntryHandler(_db.UnitOfWork, TimeProvider.System);
        _moderate = new ModerateEntryHandler(_db.UnitOfWork);
        _react = new ReactToEntryHandler(_db.UnitOfWork, TimeProvider.System);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Toggles_reactions_on_accepted_entries()
    {
        var ct = TestContext.Current.CancellationToken;
        var entry = await _create.HandleAsync(new CreateEntryRequest("Ada", "hello"), "hash-ada", ct);

        // not before it's approved
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _react.ToggleAsync(entry.Id, "heart", ct));
        await _moderate.ApproveAsync(entry.Id, ct);

        Assert.Equal(["heart"], await _react.ToggleAsync(entry.Id, "heart", ct));
        Assert.Equal(["heart", "fire"], await _react.ToggleAsync(entry.Id, "fire", ct));
        Assert.Equal(["fire"], await _react.ToggleAsync(entry.Id, "heart", ct));
        Assert.Equal(["fire"], await _react.GetAsync(entry.Id, ct));

        var listed = Assert.Single(await new ListEntriesHandler(_db.UnitOfWork).HandleAsync("someone", ct));
        Assert.Equal(["fire"], EntryResponse.From(listed)!.Reactions);
    }

    [Fact]
    public async Task Rejects_unknown_reactions_and_entries()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentException>(() => _react.ToggleAsync("whatever", "thumbsdown", ct));
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _react.ToggleAsync("nope", "heart", ct));
        await Assert.ThrowsAsync<GuestbookEntryNotFoundException>(() => _react.GetAsync("nope", ct));
    }

    [Fact]
    public async Task Reactions_no_longer_in_the_registry_are_not_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        var entry = await _create.HandleAsync(new CreateEntryRequest("Ada", "hello"), "hash-ada", ct);
        await _moderate.ApproveAsync(entry.Id, ct);
        await _react.ToggleAsync(entry.Id, "heart", ct);
        await _db.ExecuteAsync($"INSERT INTO guestbook_reactions (entry_id, reaction) VALUES ('{entry.Id}', 'retired')");

        var listed = Assert.Single(await new ListEntriesHandler(_db.UnitOfWork).HandleAsync("someone", ct));
        Assert.Equal(["heart", "retired"], listed.Reactions);
        Assert.Equal(["heart"], EntryResponse.From(listed)!.Reactions);
    }
}
