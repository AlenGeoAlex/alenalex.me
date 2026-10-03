using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Infrastructure.Discord;
using NetCord;
using NetCord.Gateway;

namespace AlenAlex.Api.Tests.Infrastructure;

public sealed class DiscordTests
{
    [Fact]
    public void Parses_button_ids()
    {
        Assert.True(ModerationMessages.TryParseButtonId("gb:approve:abc_123", out var action, out var id));
        Assert.Equal((ModerationMessages.ButtonAction.Approve, "abc_123"), (action, id));
        Assert.True(ModerationMessages.TryParseButtonId("gb:reject:x:y", out action, out id));
        Assert.Equal((ModerationMessages.ButtonAction.Reject, "x:y"), (action, id));
        Assert.False(ModerationMessages.TryParseButtonId("gb:delete:abc", out _, out _));
        Assert.False(ModerationMessages.TryParseButtonId("other:approve:abc", out _, out _));
    }

    [Fact]
    public void Builds_spotify_art_url()
    {
        Assert.Equal("https://i.scdn.co/image/ab67616d0000b273", PresenceMapper.SpotifyArtUrl("spotify:ab67616d0000b273"));
        Assert.Null(PresenceMapper.SpotifyArtUrl("mp:external/xyz"));
    }

    [Fact]
    public void Maps_presence_with_spotify_and_custom_status()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.DoNotDisturb,
        [
            new ActivityInfo(UserActivityType.Listening, "Spotify", "Song", "Artist", "Album", "spotify:abc"),
            new ActivityInfo(UserActivityType.Playing, "Rider", null, null, null, null),
            new ActivityInfo(UserActivityType.Custom, "Custom Status", null, "shipping", null, null),
        ]);

        Assert.True(snapshot.Connected);
        Assert.Equal(PresenceStatus.Dnd, snapshot.Status);
        Assert.Equal("shipping", snapshot.Activity);
        Assert.Equal(new SpotifyTrack("Song", "Artist", "Album", "https://i.scdn.co/image/abc"), snapshot.Spotify);
    }

    [Fact]
    public void Falls_back_to_activity_name_and_reports_invisible_as_offline()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Invisible,
        [
            new ActivityInfo(UserActivityType.Listening, "Spotify", "Song", null, null, null),
            new ActivityInfo(UserActivityType.Playing, "Rider", null, null, null, null),
        ]);

        Assert.Equal(PresenceStatus.Offline, snapshot.Status);
        Assert.Equal("Rider", snapshot.Activity);
        Assert.Equal(new SpotifyTrack("Song", "", null, null), snapshot.Spotify);
    }
}
