using AlenAlex.Api.Features.Status.RecordTrack;
using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Features.Status;

public sealed class RecordTrackTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 20, 0, 0, TimeSpan.Zero);

    private TestDatabase _db = null!;
    private RecordTrackHandler _record = null!;

    public ValueTask InitializeAsync()
    {
        _db = TestDatabase.Create();
        _record = new RecordTrackHandler(_db.UnitOfWork);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    private static SpotifyTrack Song(int n) => new($"Song {n}", "Artist", n % 2 == 0 ? "Album" : null, n % 2 == 0 ? $"https://i.scdn.co/image/{n}" : null);

    [Fact]
    public async Task Keeps_the_last_five_newest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 1; i <= 7; i++)
        {
            await _record.HandleAsync(Song(i), T0.AddMinutes(i), ct);
        }

        var recent = await _record.ListAsync(ct);
        Assert.Equal(["Song 7", "Song 6", "Song 5", "Song 4", "Song 3"], recent.Select(t => t.Track));
        Assert.Equal(new RecentTrack("Song 6", "Artist", "Album", "https://i.scdn.co/image/6", T0.AddMinutes(6)), recent[1]);
        Assert.Null(recent[0].Album);
        Assert.Equal(5L, await _db.ScalarAsync("SELECT COUNT(*) FROM listening_history"));
    }

    [Fact]
    public async Task The_same_song_again_is_not_a_new_entry()
    {
        var ct = TestContext.Current.CancellationToken;
        await _record.HandleAsync(Song(1), T0, ct);
        var recent = await _record.HandleAsync(Song(1), T0.AddMinutes(1), ct);

        var only = Assert.Single(recent);
        Assert.Equal(T0, only.PlayedAt);

        // a song played again after another one is listed again
        await _record.HandleAsync(Song(2), T0.AddMinutes(2), ct);
        recent = await _record.HandleAsync(Song(1), T0.AddMinutes(3), ct);
        Assert.Equal(["Song 1", "Song 2", "Song 1"], recent.Select(t => t.Track));
    }
}
