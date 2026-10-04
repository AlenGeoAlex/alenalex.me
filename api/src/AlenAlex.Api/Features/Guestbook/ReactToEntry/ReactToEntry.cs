using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Infrastructure.Persistence;

namespace AlenAlex.Api.Features.Guestbook.ReactToEntry;

/// <summary>The owner's reactions on accepted entries. No HTTP endpoint; driven by the Discord buttons and /guestbook-react.</summary>
public sealed class ReactToEntryHandler(IUnitOfWorkFactory database, TimeProvider time)
{
    /// <summary>The reactions on an accepted entry.</summary>
    /// <exception cref="GuestbookEntryNotFoundException">Unknown, or not accepted.</exception>
    public async Task<IReadOnlyList<string>> GetAsync(string id, CancellationToken ct = default)
    {
        await using var uow = await database.CreateAsync(ct);
        return (await GetAcceptedAsync(uow, id, ct)).Reactions;
    }

    /// <summary>Adds the reaction if it isn't there, removes it if it is. Returns the reactions afterwards.</summary>
    /// <exception cref="GuestbookEntryNotFoundException">Unknown, or not accepted.</exception>
    /// <exception cref="ArgumentException"><paramref name="reaction"/> isn't in <see cref="GuestbookReactions"/>.</exception>
    public async Task<IReadOnlyList<string>> ToggleAsync(string id, string reaction, CancellationToken ct = default)
    {
        if (!GuestbookReactions.IsKnown(reaction))
        {
            throw new ArgumentException($"Unknown reaction '{reaction}'", nameof(reaction));
        }

        await using var uow = await database.CreateAsync(ct);
        await uow.BeginAsync(ct);
        var current = (await GetAcceptedAsync(uow, id, ct)).Reactions;

        IReadOnlyList<string> next;
        if (current.Contains(reaction))
        {
            await uow.Guestbook.RemoveReactionAsync(id, reaction, ct);
            next = current.Where(r => r != reaction).ToList();
        }
        else
        {
            await uow.Guestbook.AddReactionAsync(id, reaction, time.GetUtcNow(), ct);
            next = [.. current, reaction];
        }
        await uow.CommitAsync(ct);
        return next;
    }

    private static async Task<GuestbookEntry> GetAcceptedAsync(IUnitOfWork uow, string id, CancellationToken ct) =>
        await uow.Guestbook.GetByIdAsync(id, viewerIpHash: "", ct) is { Status: GuestbookStatus.Accepted } entry
            ? entry
            : throw new GuestbookEntryNotFoundException();
}
