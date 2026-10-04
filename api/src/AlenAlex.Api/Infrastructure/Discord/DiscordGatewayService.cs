using AlenAlex.Api.Features.Status.RecordTrack;
using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;

namespace AlenAlex.Api.Infrastructure.Discord;

/// <summary>
/// Tracks the presence of <c>Discord:UserId</c>, registers the moderation slash commands and
/// dispatches interactions. The bot needs the privileged Presence and Server Members intents
/// enabled in the developer portal.
/// </summary>
public sealed class DiscordGatewayService(
    IOptions<DiscordOptions> options,
    LiveStatusStore status,
    DiscordModerationHandler moderation,
    ListeningRecorder listening,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DiscordGatewayService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.IsConfigured)
        {
            _logger.LogWarning("Discord:BotToken not set: running without the bot (no moderation, presence unknown)");
            return;
        }
        if (config.GuildId is null)
        {
            _logger.LogWarning("Discord:GuildId not set: slash commands and presence tracking disabled");
        }
        if (config.UserId is null)
        {
            _logger.LogWarning("Discord:UserId not set: presence unknown and ANY guild member can moderate");
        }

        GatewayClient client;
        try
        {
            client = new GatewayClient(new BotToken(config.BotToken!), new GatewayClientConfiguration
            {
                Intents = GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildPresences,
                Logger = new NetCordLogger(loggerFactory.CreateLogger("NetCord.Gateway")),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid Discord:BotToken: running without the bot");
            return;
        }

        using (client)
        {
            Wire(client, config);
            try
            {
                await client.StartAsync(cancellationToken: stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Discord gateway stopped (if this mentions disallowed intents, enable the Presence and " +
                    "Server Members intents for the bot in the developer portal)");
            }
            finally
            {
                status.UpdatePresence(p => p with { Connected = false });
                try
                {
                    await client.CloseAsync(cancellationToken: CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error closing the Discord gateway");
                }
            }
        }
    }

    private void Wire(GatewayClient client, DiscordOptions config)
    {
        client.Ready += async ready =>
        {
            _logger.LogInformation("Discord bot connected as {User}", ready.User.Username);
            status.UpdatePresence(p => p with { Connected = true });

            if (config.GuildId is { } guildId)
            {
                try
                {
                    await client.Rest.BulkOverwriteGuildApplicationCommandsAsync(ready.ApplicationId, guildId, ModerationMessages.SlashCommands());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register guild commands");
                }
            }
        };

        // /api/status reports "unknown" while disconnected.
        client.Resume += () =>
        {
            status.UpdatePresence(p => p with { Connected = true });
            return default;
        };
        client.Disconnect += _ =>
        {
            status.UpdatePresence(p => p with { Connected = false });
            return default;
        };

        // Initial presences arrive with the guild payload on connect.
        client.GuildCreate += args =>
        {
            if (config.GuildId is { } guildId && config.UserId is { } ownerId && args.GuildId == guildId && args.Guild is { } guild)
            {
                SetPresence(guild.Presences.TryGetValue(ownerId, out var presence)
                    ? PresenceMapper.FromPresence(presence)
                    // Discord omits offline members from the initial presence list.
                    : new PresenceSnapshot(Connected: true, Status: null, Activity: null, Spotify: null));
            }
            return default;
        };

        client.PresenceUpdate += presence =>
        {
            if (presence.GuildId == config.GuildId && presence.User.Id == config.UserId)
            {
                SetPresence(PresenceMapper.FromPresence(presence));
            }
            return default;
        };

        client.InteractionCreate += async interaction => await moderation.HandleAsync(interaction);
    }

    // A new song goes into the listening history; the same one again (pause, seek, status change) doesn't.
    private void SetPresence(PresenceSnapshot snapshot)
    {
        var previous = status.Presence.Spotify;
        status.SetPresence(snapshot);
        if (snapshot.Spotify is { } track && !track.IsSameSong(previous))
        {
            listening.Record(track);
        }
    }
}
