using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Features.Status.Shared;
using Npgsql;
using NpgsqlTypes;

namespace AlenAlex.Api.Infrastructure.Persistence.Postgres;

public sealed class PostgresUnitOfWorkFactory(NpgsqlDataSource dataSource) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> CreateAsync(CancellationToken ct = default) =>
        new PostgresUnitOfWork(await dataSource.OpenConnectionAsync(ct));
}

public sealed class PostgresUnitOfWork : IUnitOfWork, INpgsqlSession
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;

    public PostgresUnitOfWork(NpgsqlConnection connection)
    {
        _connection = connection;
        Guestbook = new PostgresGuestbookRepository(this);
        ListeningHistory = new PostgresListeningHistoryRepository(this);
    }

    public IGuestbookRepository Guestbook { get; }

    public IListeningHistoryRepository ListeningHistory { get; }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }
        _transaction = await _connection.BeginTransactionAsync(ct);
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

    public NpgsqlCommand CreateCommand()
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        return command;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            // not committed: disposing rolls back
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        await _connection.DisposeAsync();
    }
}

public interface INpgsqlSession
{
    /// <summary>
    /// An empty command on the session's connection and transaction. Set <c>CommandText</c> from a
    /// <c>const</c> and add typed parameters (<see cref="NpgsqlParameterExtensions"/>); CA2100 fails
    /// the build for anything else.
    /// </summary>
    NpgsqlCommand CreateCommand();
}

/// <summary>Every value is bound with an explicit <see cref="NpgsqlDbType"/>.</summary>
public static class NpgsqlParameterExtensions
{
    public static NpgsqlCommand WithText(this NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = (object?)value ?? DBNull.Value;
        return command;
    }

    public static NpgsqlCommand WithInt(this NpgsqlCommand command, string name, int value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value = value;
        return command;
    }

    /// <summary>timestamptz; Npgsql requires UTC.</summary>
    public static NpgsqlCommand WithTimestamp(this NpgsqlCommand command, string name, DateTimeOffset value)
    {
        command.Parameters.Add(name, NpgsqlDbType.TimestampTz).Value = value.ToUniversalTime();
        return command;
    }
}
