using System.Threading.Channels;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;
using BotToken = NetCord.BotToken;
using NetCord.Rest;

namespace AlenAlex.Api.Infrastructure.Discord;

/// <summary>Entries are queued and posted in the background so HTTP requests never wait on Discord.</summary>
public sealed class DiscordModerationNotifier(
    IOptions<DiscordOptions> options,
    ILoggerFactory loggerFactory) : BackgroundService, IGuestbookModerationNotifier
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DiscordModerationNotifier>();

    private readonly Channel<(GuestbookEntry Entry, string IpHash)> _queue =
        System.Threading.Channels.Channel.CreateBounded<(GuestbookEntry, string)>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public void QueueForReview(GuestbookEntry entry, string ipHash)
    {
        var discord = options.Value;
        if (!discord.IsConfigured || discord.ChannelId is null)
        {
            _logger.LogWarning("Discord not configured: entry {EntryId} not sent for moderation", entry.Id);
            return;
        }
        _queue.Writer.TryWrite((entry, ipHash));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var discord = options.Value;
        if (!discord.IsConfigured)
        {
            return;
        }
        if (discord.ChannelId is not { } channelId)
        {
            _logger.LogWarning("Discord:ChannelId not set: new entries will not be posted for moderation");
            return;
        }

        RestClient rest;
        try
        {
            rest = new RestClient(new BotToken(discord.BotToken!), new RestClientConfiguration
            {
                Logger = new NetCordLogger(loggerFactory.CreateLogger("NetCord.Rest")),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid Discord:BotToken: new entries will not be posted for moderation");
            return;
        }

        using var _ = rest;

        await foreach (var (entry, ipHash) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await rest.SendMessageAsync(channelId, ModerationMessages.NewEntry(entry, ipHash), cancellationToken: stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to post entry {EntryId} to Discord", entry.Id);
            }
        }
    }
}
