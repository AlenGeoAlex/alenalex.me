using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Features.Guestbook.LikeEntry;
using AlenAlex.Api.Features.Guestbook.ListEntries;
using AlenAlex.Api.Features.Guestbook.ListPendingEntries;
using AlenAlex.Api.Features.Guestbook.ModerateEntry;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Features.Guestbook.UnlikeEntry;
using AlenAlex.Api.Infrastructure.Discord;
using AlenAlex.Api.Infrastructure.Http;

namespace AlenAlex.Api.Features.Guestbook;

public static class GuestbookFeature
{
    public static IServiceCollection AddGuestbook(this IServiceCollection services)
    {
        services.AddSingleton<ListEntriesHandler>();
        services.AddSingleton<CreateEntryHandler>();
        services.AddSingleton<LikeEntryHandler>();
        services.AddSingleton<UnlikeEntryHandler>();
        services.AddSingleton<ModerateEntryHandler>();
        services.AddSingleton<ListPendingEntriesHandler>();

        // Without a bot token this only logs a warning.
        services.AddSingleton<DiscordModerationNotifier>();
        services.AddSingleton<IGuestbookModerationNotifier>(sp => sp.GetRequiredService<DiscordModerationNotifier>());
        services.AddHostedService(sp => sp.GetRequiredService<DiscordModerationNotifier>());
        return services;
    }

    public static IEndpointRouteBuilder MapGuestbook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guestbook")
            .WithTags("guestbook")
            .AddEndpointFilter(GuestbookErrorFilter.InvokeAsync);

        group.MapEndpoint<ListEntriesEndpoint>();

        var writes = group.MapGroup("").RequireRateLimiting(RateLimiting.WritePolicy);
        writes.MapEndpoint<CreateEntryEndpoint>();
        writes.MapEndpoint<LikeEntryEndpoint>();
        writes.MapEndpoint<UnlikeEntryEndpoint>();
        return app;
    }
}
