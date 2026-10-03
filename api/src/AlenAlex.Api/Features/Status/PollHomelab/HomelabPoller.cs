using System.Diagnostics;
using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Status.PollHomelab;

public sealed class HomelabPoller(
    IHttpClientFactory httpClientFactory,
    IOptions<HomelabOptions> options,
    LiveStatusStore store,
    ILogger<HomelabPoller> logger) : BackgroundService
{
    public const string HttpClientName = "homelab";
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var services = options.Value.Services;
        if (services.Count == 0)
        {
            logger.LogInformation("Homelab:Services is empty: homelab status will report 0 services");
            return;
        }

        using var timer = new PeriodicTimer(PollEvery);
        do
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            store.SetHomelab(await CheckAllAsync(client, services, logger, stoppingToken));
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Results keep the configured order.</summary>
    public static async Task<IReadOnlyList<ServiceStatus>> CheckAllAsync(
        HttpClient client, IReadOnlyList<HomelabService> services, ILogger logger, CancellationToken ct) =>
        await Task.WhenAll(services.Select(service => CheckAsync(client, service, logger, ct)));

    /// <summary>Any 2xx or 3xx counts as up; the client doesn't follow redirects.</summary>
    public static async Task<ServiceStatus> CheckAsync(HttpClient client, HomelabService service, ILogger logger, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.GetAsync(service.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            var latency = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var code = (int)response.StatusCode;
            return new ServiceStatus(service.Name, code is >= 200 and < 400, latency);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }
            logger.LogDebug("Health check for {Service} failed: {Error}", service.Name, ex.Message);
            return new ServiceStatus(service.Name, false, null);
        }
    }
}
