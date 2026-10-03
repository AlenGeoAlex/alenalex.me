using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>A real SQLite file in the temp folder, migrated by FluentMigrator; deleted on dispose.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase()
    {
        Connections = new SqliteConnectionFactory(ConnectionString);
        UnitOfWork = new SqliteUnitOfWorkFactory(Connections);
    }

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"alenalex-api-it-{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={Path}";

    public SqliteConnectionFactory Connections { get; }

    public IUnitOfWorkFactory UnitOfWork { get; }

    public static TestDatabase Create(bool migrate = true)
    {
        var db = new TestDatabase();
        if (migrate)
        {
            db.Migrate();
        }
        return db;
    }

    public void Migrate() => new DatabaseMigrator(Connections, NullLoggerFactory.Instance).MigrateUp();

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(Path + suffix);
        }
        return ValueTask.CompletedTask;
    }
}
