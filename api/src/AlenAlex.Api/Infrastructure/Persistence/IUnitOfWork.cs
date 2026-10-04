using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Features.Status.Shared;

namespace AlenAlex.Api.Infrastructure.Persistence;

public interface IUnitOfWorkFactory
{
    /// <summary>Opens a connection. Reads can run straight away; call <see cref="IUnitOfWork.BeginAsync"/> before writes.</summary>
    Task<IUnitOfWork> CreateAsync(CancellationToken ct = default);
}

/// <summary>
/// One connection (and optionally one transaction) shared by its repositories.
/// Disposing without <see cref="CommitAsync"/> rolls back.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IGuestbookRepository Guestbook { get; }

    IListeningHistoryRepository ListeningHistory { get; }

    Task BeginAsync(CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}
