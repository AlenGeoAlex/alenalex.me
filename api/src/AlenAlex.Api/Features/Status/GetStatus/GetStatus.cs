using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AlenAlex.Api.Features.Status.GetStatus;

/// <param name="RecentTracks">The last few Spotify tracks, newest first (the current one included).</param>
public sealed record GetStatusResponse(
    DiscordStatus Discord,
    SpotifyTrack? Spotify,
    IReadOnlyList<RecentTrack> RecentTracks,
    HomelabStatus Homelab,
    DateTimeOffset FetchedAt);

/// <param name="Activity">Custom status text, or the name of the current non-Spotify activity.</param>
public sealed record DiscordStatus(PresenceStatus Status, string? Activity);

public sealed record HomelabStatus(int Up, int Total, IReadOnlyList<ServiceStatus> Services);

public sealed class GetStatusEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/status", (GetStatusHandler handler) => handler.Handle())
            .WithTags("dashboard")
            .WithSummary("Discord presence, Spotify now-playing and recently played, and homelab health");
}

public sealed class GetStatusHandler(LiveStatusStore store, TimeProvider time)
{
    public Ok<GetStatusResponse> Handle()
    {
        var presence = store.Presence;

        // Disconnected means unknown. Connected without a presence means offline: Discord
        // omits presences for offline members.
        var (discord, spotify) = presence.Connected
            ? (new DiscordStatus(presence.Status ?? PresenceStatus.Offline, presence.Activity), presence.Spotify)
            : (new DiscordStatus(PresenceStatus.Unknown, null), null);

        var services = store.Homelab;
        var homelab = new HomelabStatus(services.Count(s => s.Up), services.Count, services);

        return TypedResults.Ok(new GetStatusResponse(discord, spotify, store.RecentTracks, homelab, time.GetUtcNow()));
    }
}
