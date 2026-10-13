using AlenAlex.Api.Features.Guestbook.Shared;
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
    public void Reaction_buttons_cover_every_reaction_five_per_row()
    {
        var rows = ModerationMessages.ReactionRows("abc:1", ["fire"]);

        var buttons = rows.SelectMany(r => r.Components).Cast<NetCord.Rest.ButtonProperties>().ToList();
        Assert.Equal(GuestbookReactions.All.Count, buttons.Count);
        Assert.All(rows, r => Assert.InRange(r.Components.Count(), 1, 5));
        Assert.Equal("gb:react:heart:abc:1", buttons[0].CustomId);
        Assert.Equal([ButtonStyle.Primary], buttons.Where(b => b.Style == ButtonStyle.Primary).Select(b => b.Style));
        Assert.Equal("gb:react:fire:abc:1", buttons.Single(b => b.Style == ButtonStyle.Primary).CustomId);
    }

    [Fact]
    public void Parses_reaction_button_ids()
    {
        Assert.True(ModerationMessages.TryParseReactionButtonId("gb:react:heart:x:y", out var reaction, out var id));
        Assert.Equal(("heart", "x:y"), (reaction, id));
        Assert.False(ModerationMessages.TryParseReactionButtonId("gb:react:nope:x", out _, out _));
        Assert.False(ModerationMessages.TryParseReactionButtonId("gb:react:heart:", out _, out _));
        Assert.False(ModerationMessages.TryParseReactionButtonId("gb:approve:x", out _, out _));
        Assert.False(ModerationMessages.TryParseButtonId("gb:react:heart:x", out _, out _));
    }

    [Fact]
    public void Reaction_keys_are_unique_lowercase_and_fit_on_one_message()
    {
        Assert.Equal(GuestbookReactions.All.Count, GuestbookReactions.All.Select(r => r.Key).Distinct().Count());
        Assert.All(GuestbookReactions.All, r => Assert.Matches("^[a-z]+$", r.Key));
        Assert.InRange(GuestbookReactions.All.Count, 1, 25);
        // in registry order, whatever order they were added in
        Assert.Equal("❤️ 🔥", ModerationMessages.Describe(["fire", "heart"]));
        Assert.Equal("none", ModerationMessages.Describe([]));
    }

    [Fact]
    public void Same_song_ignores_album_and_art()
    {
        var track = new ListeningTrack("Song", "Artist", "Album", "https://i.scdn.co/image/a");
        Assert.True(track.IsSameSong(track with { Album = null, ArtUrl = null }));
        Assert.False(track.IsSameSong(track with { Track = "Other" }));
        Assert.False(track.IsSameSong(null));
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
        Assert.Equal(new ListeningTrack("Song", "Artist", "Album", "https://i.scdn.co/image/abc"), snapshot.Spotify);
    }

    [Fact]
    public void Maps_presence_with_limusic_and_custom_status()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Online,
        [
            new ActivityInfo(UserActivityType.Custom, "Custom Status", null, "coding", null, null),
            new ActivityInfo(UserActivityType.Listening, "Limusic", "Song", "Artist", null, "mp:external/123/https/i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg"),
        ]);

        Assert.True(snapshot.Connected);
        Assert.Equal(PresenceStatus.Online, snapshot.Status);
        Assert.Equal("coding", snapshot.Activity);
        Assert.Equal(new ListeningTrack("Song", "Artist", null, "https://img.youtube.com/vi/dQw4w9WgXcQ/maxresdefault.jpg"), snapshot.Spotify);
    }

    [Fact]
    public void Maps_presence_with_limusic_googleusercontent_art()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Idle,
        [
            new ActivityInfo(UserActivityType.Listening, "Limusic", "Song", "Artist", null, "mp:external/123/https/lh3.googleusercontent.com/xyz"),
        ]);

        Assert.True(snapshot.Connected);
        Assert.Equal(PresenceStatus.Idle, snapshot.Status);
        Assert.Null(snapshot.Activity);
        Assert.Equal(new ListeningTrack("Song", "Artist", null, "https://lh3.googleusercontent.com/xyz"), snapshot.Spotify);
    }

    [Fact]
    public void Ignores_unsupported_music_providers_and_non_listening_activity_types()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Online,
        [
            new ActivityInfo(UserActivityType.Listening, "Apple Music", "Song", "Artist", null, null),
            new ActivityInfo(UserActivityType.Playing, "Spotify", "Song", "Artist", null, null),
            new ActivityInfo(UserActivityType.Playing, "Limusic", "Song", "Artist", null, null),
        ]);

        Assert.True(snapshot.Connected);
        Assert.Null(snapshot.Spotify);
    }

    [Fact]
    public void Maps_limusic_with_null_fields_to_empty_strings()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Online,
        [
            new ActivityInfo(UserActivityType.Listening, "Limusic", null, null, null, null),
        ]);

        Assert.Equal(new ListeningTrack("", "", null, null), snapshot.Spotify);
    }

    [Fact]
    public void Selects_first_supported_music_activity_when_multiple_present()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Online,
        [
            new ActivityInfo(UserActivityType.Listening, "Limusic", "Limusic Song", "Limusic Artist", null, null),
            new ActivityInfo(UserActivityType.Listening, "Spotify", "Spotify Song", "Spotify Artist", "Album", null),
        ]);

        Assert.Equal(new ListeningTrack("Limusic Song", "Limusic Artist", null, null), snapshot.Spotify);
    }

    [Fact]
    public void Maps_presence_when_non_music_activity_precedes_music_activity()
    {
        var snapshot = PresenceMapper.FromParts(UserStatusType.Online,
        [
            new ActivityInfo(UserActivityType.Playing, "Rider", null, null, null, null),
            new ActivityInfo(UserActivityType.Listening, "Limusic", "Song", "Artist", null, null),
        ]);

        Assert.Equal("Rider", snapshot.Activity);
        Assert.Equal(new ListeningTrack("Song", "Artist", null, null), snapshot.Spotify);
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
        Assert.Equal(new ListeningTrack("Song", "", null, null), snapshot.Spotify);
    }
}
