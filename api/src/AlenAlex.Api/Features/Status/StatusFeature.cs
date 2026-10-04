using AlenAlex.Api.Features.Status.GetStatus;
using AlenAlex.Api.Features.Status.PollHomelab;
using AlenAlex.Api.Features.Status.RecordTrack;
using AlenAlex.Api.Features.Status.Shared;
using AlenAlex.Api.Infrastructure.Discord;
using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Status;

public static class StatusFeature
{
    public static IServiceCollection AddStatus(this IServiceCollection services)
    {
        services.AddSingleton<LiveStatusStore>();
        services.AddSingleton<GetStatusHandler>();

        services.AddHttpClient(HomelabPoller.HttpClientName, client =>
            {
                client.Timeout = HomelabPoller.Timeout;
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "alenalex.me-api healthcheck");
            })
            // Don't follow redirects: a login redirect still means the service is up.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHostedService<HomelabPoller>();

        services.AddSingleton<RecordTrackHandler>();
        services.AddSingleton<ListeningRecorder>();
        services.AddHostedService(sp => sp.GetRequiredService<ListeningRecorder>());

        // The gateway connection provides presence/Spotify and also handles guestbook moderation.
        services.AddSingleton<DiscordModerationHandler>();
        services.AddHostedService<DiscordGatewayService>();
        return services;
    }

    public static IEndpointRouteBuilder MapStatus(this IEndpointRouteBuilder app) =>
        app.MapEndpoint<GetStatusEndpoint>();
}
