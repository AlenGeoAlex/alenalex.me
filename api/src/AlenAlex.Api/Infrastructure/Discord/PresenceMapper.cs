using AlenAlex.Api.Features.Status.Shared;
using NetCord;
using NetCord.Gateway;

namespace AlenAlex.Api.Infrastructure.Discord;

public static class PresenceMapper
{
    private const string SpotifyArtPrefix = "spotify:";

    public static PresenceSnapshot FromPresence(Presence presence) =>
        FromParts(presence.Status, presence.Activities.Select(a => new ActivityInfo(a.Type, a.Name, a.Details, a.State, a.Assets?.LargeText, a.Assets?.LargeImageId)).ToList());

    /// <summary>NetCord-independent so it can be unit tested.</summary>
    public static PresenceSnapshot FromParts(UserStatusType status, IReadOnlyList<ActivityInfo> activities)
    {
        var mapped = status switch
        {
            UserStatusType.Online => PresenceStatus.Online,
            UserStatusType.Idle => PresenceStatus.Idle,
            UserStatusType.DoNotDisturb => PresenceStatus.Dnd,
            // Invisible looks offline to everyone else.
            UserStatusType.Invisible or UserStatusType.Offline => PresenceStatus.Offline,
            _ => PresenceStatus.Unknown,
        };

        var spotify = activities.Select(SpotifyFrom).FirstOrDefault(track => track is not null);

        // Prefer the custom status text, then any other non-Spotify activity name.
        var activity = activities.FirstOrDefault(a => a.Type == UserActivityType.Custom)?.State
            ?? activities.FirstOrDefault(a => a.Type != UserActivityType.Custom && !IsSpotify(a))?.Name;

        return new PresenceSnapshot(Connected: true, mapped, activity, spotify);
    }

    /// <summary><c>spotify:&lt;id&gt;</c> to <c>https://i.scdn.co/image/&lt;id&gt;</c>.</summary>
    public static string? SpotifyArtUrl(string? largeImage) =>
        largeImage is not null && largeImage.StartsWith(SpotifyArtPrefix, StringComparison.Ordinal)
            ? "https://i.scdn.co/image/" + largeImage[SpotifyArtPrefix.Length..]
            : null;

    private static bool IsSpotify(ActivityInfo activity) =>
        activity.Type == UserActivityType.Listening && activity.Name == "Spotify";

    // Spotify's presence: details = track, state = artist, large_text = album, large_image = art.
    private static SpotifyTrack? SpotifyFrom(ActivityInfo activity) =>
        IsSpotify(activity) && activity.Details is { } track
            ? new SpotifyTrack(track, activity.State ?? "", activity.LargeText, SpotifyArtUrl(activity.LargeImage))
            : null;
}

public sealed record ActivityInfo(UserActivityType Type, string Name, string? Details, string? State, string? LargeText, string? LargeImage);
