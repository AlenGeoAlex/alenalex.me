using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>A fresh database in the shared PostgreSQL container, migrated by FluentMigrator.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    private TestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        _dataSource = NpgsqlDataSource.Create(connectionString);
        UnitOfWork = new PostgresUnitOfWorkFactory(_dataSource);
    }

    public string ConnectionString { get; }

    public IUnitOfWorkFactory UnitOfWork { get; }

    /// <summary>Skips the calling test when Docker is unavailable.</summary>
    public static TestDatabase Create(bool migrate = true)
    {
        var db = new TestDatabase(PostgresServer.CreateDatabase());
        if (migrate)
        {
            db.Migrate();
        }
        return db;
    }

    public void Migrate() => new DatabaseMigrator(ConnectionString, AlenAlex.Api.Options.DatabaseOptions.DefaultSchema, NullLoggerFactory.Instance).MigrateUp();

    public async Task ExecuteAsync(string sql)
    {
        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var command = _dataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
