using System.Text.Json.Serialization;

namespace AlenAlex.Api.Features.Status.Shared;

[JsonConverter(typeof(JsonStringEnumConverter<PresenceStatus>))]
public enum PresenceStatus
{
    [JsonStringEnumMemberName("online")] Online,
    [JsonStringEnumMemberName("idle")] Idle,
    [JsonStringEnumMemberName("dnd")] Dnd,
    [JsonStringEnumMemberName("offline")] Offline,
    [JsonStringEnumMemberName("unknown")] Unknown,
}

public sealed record SpotifyTrack(string Track, string Artist, string? Album, string? ArtUrl);

/// <param name="LatencyMs"><c>null</c> if the check failed or timed out.</param>
public sealed record ServiceStatus(string Name, bool Up, long? LatencyMs);

/// <summary>Presence of <c>Discord:UserId</c>.</summary>
/// <param name="Connected">Whether the gateway is connected; if not, the status is unknown.</param>
/// <param name="Status"><c>null</c> while connected means Discord sent no presence, i.e. the user is offline.</param>
public sealed record PresenceSnapshot(bool Connected, PresenceStatus? Status, string? Activity, SpotifyTrack? Spotify)
{
    public static readonly PresenceSnapshot Disconnected = new(false, null, null, null);
}
