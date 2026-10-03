using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Github.RefreshSummary;

/// <summary>
/// Runs on startup and every 15 minutes. A failed refresh keeps the previous value.
/// </summary>
public sealed class RefreshSummaryJob(
    IServiceScopeFactory scopes,
    IOptions<GithubOptions> options,
    GithubSummaryStore store,
    ILogger<RefreshSummaryJob> logger) : BackgroundService
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            logger.LogWarning("Github:Token not set: /api/github will answer 503");
            return;
        }

        using var timer = new PeriodicTimer(RefreshEvery);
        do
        {
            try
            {
                // Typed HttpClients are transient; resolve a fresh one per refresh.
                using var scope = scopes.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<GithubGraphQlClient>();
                var summary = await client.FetchSummaryAsync(options.Value.Login, stoppingToken);
                store.Set(summary);
                logger.LogInformation("GitHub summary refreshed ({Repos} repos)", summary.Repos.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "GitHub refresh failed, keeping last value");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
