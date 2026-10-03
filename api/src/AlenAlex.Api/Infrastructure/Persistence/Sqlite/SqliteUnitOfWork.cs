using AlenAlex.Api.Features.Guestbook.Shared;
using Microsoft.Data.Sqlite;

namespace AlenAlex.Api.Infrastructure.Persistence.Sqlite;

/// <inheritdoc cref="IUnitOfWorkFactory"/>
public sealed class SqliteUnitOfWorkFactory(SqliteConnectionFactory connections) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> CreateAsync(CancellationToken ct = default) =>
        new SqliteUnitOfWork(await connections.OpenAsync(ct));
}

public sealed class SqliteUnitOfWork : IUnitOfWork, ISqliteSession
{
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;

    public SqliteUnitOfWork(SqliteConnection connection)
    {
        _connection = connection;
        Guestbook = new SqliteGuestbookRepository(this);
    }

    public IGuestbookRepository Guestbook { get; }

    public Task BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }
        // Immediate, so concurrent writers wait on the busy timeout instead of failing
        // when a read lock is upgraded.
        _transaction = _connection.BeginTransaction(deferred: false);
        return Task.CompletedTask;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction.");
        }
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
        {
            return;
        }
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public SqliteCommand CreateCommand()
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        await _connection.DisposeAsync();
    }
}

public interface ISqliteSession
{
    /// <summary>
    /// Bound to the current transaction. Set <c>CommandText</c> from a <c>const</c> and add typed
    /// parameters; CA2100 fails the build otherwise.
    /// </summary>
    SqliteCommand CreateCommand();
}

public static class SqliteParameterExtensions
{
    public static SqliteCommand WithText(this SqliteCommand command, string name, string? value)
    {
        command.Parameters.Add(name, SqliteType.Text).Value = (object?)value ?? DBNull.Value;
        return command;
    }
}
