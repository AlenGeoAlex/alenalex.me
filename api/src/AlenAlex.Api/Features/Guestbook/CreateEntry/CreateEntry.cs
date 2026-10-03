using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Http;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.CreateEntry;

public sealed record CreateEntryRequest(string? Name, string? Message);

public sealed record CreateEntryResponse(EntryResponse Entry);

public sealed class CreateEntryEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/", async (CreateEntryRequest request, HttpContext http, IClientIpResolver clientIp,
                CreateEntryHandler handler, IGuestbookModerationNotifier moderation, CancellationToken ct) =>
            {
                var ipHash = clientIp.GetIpHash(http);
                var entry = await handler.HandleAsync(request, ipHash, ct);
                moderation.QueueForReview(entry, ipHash);

                var body = EntryResponse.From(entry) ?? throw new InvalidOperationException("new entry was not pending");
                return TypedResults.Created((string?)null, new CreateEntryResponse(body));
            })
            .WithSummary("Create an entry (pending moderation). 422 invalid input, 429 more than 5 per hour per IP");
}

public sealed class CreateEntryHandler(IUnitOfWorkFactory database, TimeProvider time)
{
    public const int MaxEntriesPerHour = 5;

    /// <exception cref="GuestbookValidationException"/>
    /// <exception cref="GuestbookRateLimitedException"/>
    public async Task<GuestbookEntry> HandleAsync(CreateEntryRequest request, string ipHash, CancellationToken ct = default)
    {
        var (name, message) = CreateEntryValidator.Validate(request.Name, request.Message);
        var now = time.GetUtcNow();

        await using var uow = await database.CreateAsync(ct);
        // Count and insert in one transaction, holding a per-visitor lock, so parallel posts
        // from the same IP wait for each other instead of both slipping under the limit.
        await uow.BeginAsync(ct);
        await uow.Guestbook.LockVisitorAsync(ipHash, ct);

        if (await uow.Guestbook.CountCreatedSinceAsync(ipHash, now.AddHours(-1), ct) >= MaxEntriesPerHour)
        {
            throw new GuestbookRateLimitedException();
        }

        var id = NanoId.New();
        await uow.Guestbook.InsertAsync(new NewGuestbookEntry(id, name, message, ipHash, now), ct);
        var entry = await uow.Guestbook.GetByIdAsync(id, ipHash, ct) ?? throw new GuestbookEntryNotFoundException();

        await uow.CommitAsync(ct);
        return entry;
    }
}
