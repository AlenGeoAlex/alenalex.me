namespace AlenAlex.Api.Options;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>Without a token the bot is disabled.</summary>
    public string? BotToken { get; set; }

    /// <summary>Guild where slash commands are registered and presence is tracked.</summary>
    public ulong? GuildId { get; set; }

    /// <summary>Channel that receives the moderation embed for every new entry.</summary>
    public ulong? ChannelId { get; set; }

    /// <summary>Whose presence is shown; also the only user allowed to moderate.</summary>
    public ulong? UserId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}
