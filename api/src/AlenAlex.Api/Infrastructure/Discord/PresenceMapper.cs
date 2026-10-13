using System.Text.RegularExpressions;
using AlenAlex.Api.Features.Status.Shared;
using NetCord;
using NetCord.Gateway;

namespace AlenAlex.Api.Infrastructure.Discord;

public static partial class PresenceMapper
{
    private const string SpotifyArtPrefix = "spotify:";
    private const string Limusic = "Limusic";
    private const string Spotify = "Spotify";

    private static readonly string[] MusicActivityProviders = [Limusic, Spotify];

    // Matches the YouTube video ID format
    [GeneratedRegex(@"/vi/(?<id>[^/]+)/")]
    private static partial Regex YoutubeVideoIdRegex();

    // Captures the underlying URL after the Limusic prefix
    [GeneratedRegex(@"/https/(?<cleanUrl>.*)$")]
    private static partial Regex CleanUrlRegex();
    

    public static PresenceSnapshot FromPresence(Presence presence) =>
        FromParts(presence.Status,
            presence.Activities.Select(a =>
                    new ActivityInfo(a.Type, a.Name, a.Details, a.State, a.Assets?.LargeText, a.Assets?.LargeImageId))
                .ToList());

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

        var listening = activities.Select(FilterFirstMusicActivity).FirstOrDefault(track => track is not null);

        // Prefer the custom status text, then any other non-music activity name.
        var activity = activities.FirstOrDefault(a => a.Type == UserActivityType.Custom)?.State
                       ?? activities.FirstOrDefault(a => a.Type != UserActivityType.Custom && !IsMusicActivity(a))?.Name;

        return new PresenceSnapshot(Connected: true, mapped, activity, listening);
    }

    /// <summary><c>spotify:&lt;id&gt;</c> to <c>https://i.scdn.co/image/&lt;id&gt;</c>.</summary>
    public static string? SpotifyArtUrl(string? largeImage) =>
        largeImage is not null && largeImage.StartsWith(SpotifyArtPrefix, StringComparison.Ordinal)
            ? "https://i.scdn.co/image/" + largeImage[SpotifyArtPrefix.Length..]
            : null;

    public static string? YoutubeMusicArtUrl(string? largeImage)
    {
        if (largeImage is null)
            return null;

        var videoMatch = YoutubeVideoIdRegex().Match(largeImage);
        if (videoMatch.Success)
        {
            var videoId = videoMatch.Groups["id"].Value;
            return $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
        }
        
        if (largeImage.Contains("googleusercontent.com"))
        {
            var urlMatch = CleanUrlRegex().Match(largeImage);
            if (urlMatch.Success)
            {
                return $"https://{urlMatch.Groups["cleanUrl"].Value}";
            }
        }

        return null;
    }

    private static bool IsMusicActivity(ActivityInfo activity) =>
        activity.Type == UserActivityType.Listening && MusicActivityProviders.Contains(activity.Name);

    private static ListeningTrack? FilterFirstMusicActivity(ActivityInfo info)
    {
        if (info.Type != UserActivityType.Listening)
            return null;

        var provider = MusicActivityProviders.FirstOrDefault(p => p == info.Name);

        return provider switch
        {
            Limusic => ExtractLimusic(info),
            Spotify => ExtractSpotify(info),
            _ => null
        };
    }

    private static ListeningTrack? ExtractLimusic(ActivityInfo info) =>
        new(
            info.Details ?? "",
            info.State ?? "",
            null,
            YoutubeMusicArtUrl(info.LargeImage));

    private static ListeningTrack? ExtractSpotify(ActivityInfo info) =>
        new(info.Details ?? "", info.State ?? "", info.LargeText, SpotifyArtUrl(info.LargeImage));
}

public sealed record ActivityInfo(UserActivityType Type, string Name, string? Details, string? State, string? LargeText, string? LargeImage);
