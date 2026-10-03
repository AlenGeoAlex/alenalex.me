using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AlenAlex.Api.Infrastructure.Persistence.Postgres;

/// <summary>
/// Runs <c>SELECT 1</c> every <c>Database:KeepAliveInterval</c>, so a managed free-tier database
/// (Aiven powers those off after a period without activity) stays on. Failures are logged, never thrown.
/// </summary>
public sealed class DatabaseKeepAlive(
    NpgsqlDataSource dataSource,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseKeepAlive> logger) : BackgroundService
{
    private const string PingSql = "SELECT 1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.KeepAliveInterval;
        if (interval <= TimeSpan.Zero)
        {
            logger.LogInformation("Database keep-alive disabled");
            return;
        }

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PingAsync(stoppingToken);
        }
    }

    internal async Task<bool> PingAsync(CancellationToken ct)
    {
        try
        {
            await using var command = dataSource.CreateCommand();
            command.CommandText = PingSql;
            await command.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Database keep-alive ping failed");
            return false;
        }
    }
}
