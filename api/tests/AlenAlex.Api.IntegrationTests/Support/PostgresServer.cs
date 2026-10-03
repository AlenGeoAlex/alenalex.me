using Npgsql;
using Testcontainers.PostgreSql;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>
/// One PostgreSQL container (<see cref="Image"/>) for the whole test run; every test gets its own
/// empty database in it. When Docker is unavailable the tests that need it are skipped.
/// </summary>
public static class PostgresServer
{
    public const string Image = "postgres:16-alpine";

    private static readonly Lazy<Task<(string? ConnectionString, string? SkipReason)>> Server = new(StartAsync);

    /// <summary>Creates an empty database and returns its connection string, or skips the test without Docker.</summary>
    public static string CreateDatabase() => CreateDatabaseAsync().GetAwaiter().GetResult();

    public static async Task<string> CreateDatabaseAsync()
    {
        var (admin, skipReason) = await Server.Value;
        if (skipReason is not null)
        {
            Assert.Skip(skipReason);
        }

        var name = $"t_{Guid.NewGuid():N}";
        await using (var dataSource = NpgsqlDataSource.Create(admin!))
        await using (var command = dataSource.CreateCommand($"CREATE DATABASE {name}"))
        {
            await command.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
    }

    private static async Task<(string?, string?)> StartAsync()
    {
        try
        {
            var container = new PostgreSqlBuilder(Image).Build();
            await container.StartAsync();
            // Reaped by Testcontainers' resource reaper when the test process exits.
            return (container.GetConnectionString(), null);
        }
        catch (Exception ex)
        {
            return (null, $"Docker is not available, skipping PostgreSQL tests ({ex.GetType().Name}: {ex.Message})");
        }
    }
}
