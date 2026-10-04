using System.Threading.Channels;
using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Status.RecordTrack;

public sealed class RecordTrackHandler(IUnitOfWorkFactory database)
{
    /// <summary>How many tracks are kept (and shown on the site).</summary>
    public const int Keep = 5;

    /// <summary>Saves the track unless it's the newest one already, trims the history, and returns it.</summary>
    public async Task<IReadOnlyList<RecentTrack>> HandleAsync(SpotifyTrack track, DateTimeOffset playedAt, CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        await uow.BeginAsync(ct);
        var recent = await uow.ListeningHistory.ListRecentAsync(Keep, ct);
        if (recent.Count > 0 && recent[0].Track == track.Track && recent[0].Artist == track.Artist)
        {
            return recent;
        }

        await uow.ListeningHistory.InsertAsync(new RecentTrack(track.Track, track.Artist, track.Album, track.ArtUrl, playedAt), ct);
        await uow.ListeningHistory.TrimAsync(Keep, ct);
        recent = await uow.ListeningHistory.ListRecentAsync(Keep, ct);
        await uow.CommitAsync(ct);
        return recent;
    }

    public async Task<IReadOnlyList<RecentTrack>> ListAsync(CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        return await uow.ListeningHistory.ListRecentAsync(Keep, ct);
    }
}

/// <summary>
/// Saves the songs the Discord gateway sees in the background, so the gateway never waits on the
/// database, and keeps <see cref="LiveStatusStore.RecentTracks"/> up to date.
/// </summary>
public sealed class ListeningRecorder(
    RecordTrackHandler handler,
    LiveStatusStore store,
    TimeProvider time,
    ILogger<ListeningRecorder> logger) : BackgroundService
{
    private readonly Channel<(SpotifyTrack Track, DateTimeOffset PlayedAt)> _queue =
        Channel.CreateBounded<(SpotifyTrack, DateTimeOffset)>(new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public void Record(SpotifyTrack track) => _queue.Writer.TryWrite((track, time.GetUtcNow()));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            store.SetRecentTracks(await handler.ListAsync(stoppingToken));
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to load the listening history");
        }

        await foreach (var (track, playedAt) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                store.SetRecentTracks(await handler.HandleAsync(track, playedAt, stoppingToken));
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to save {Track} to the listening history", track.Track);
            }
        }
    }
}
