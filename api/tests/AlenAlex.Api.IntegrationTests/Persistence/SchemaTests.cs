using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using AlenAlex.Api.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AlenAlex.Api.IntegrationTests.Persistence;

/// <summary>
/// A role that may not create tables in <c>public</c> (the PostgreSQL 15+ default for non-owners)
/// but owns its own schema, as on a managed database with a dedicated app user.
/// </summary>
public sealed class SchemaTests
{
    [Fact]
    public async Task Restricted_role_fails_in_public_but_works_in_its_own_schema()
    {
        var admin = await PostgresServer.CreateDatabaseAsync();
        var role = $"app_{Guid.NewGuid():N}"[..20];
        await using (var dataSource = NpgsqlDataSource.Create(admin))
        {
            foreach (var sql in new[]
                     {
                         $"CREATE ROLE {role} LOGIN PASSWORD 'app'",
                         $"REVOKE CREATE ON SCHEMA public FROM PUBLIC",
                         $"CREATE SCHEMA guestbook AUTHORIZATION {role}",
                     })
            {
                await using var command = dataSource.CreateCommand(sql);
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
        }

        var app = new NpgsqlConnectionStringBuilder(admin) { Username = role, Password = "app" }.ConnectionString;

        // The default schema reproduces "permission denied for schema public".
        var denied = Assert.ThrowsAny<Exception>(() => Migrator(app, schema: null).MigrateUp());
        Assert.Contains("permission denied for schema public", denied.ToString());

        // With Database:Schema the migration and the app's queries use that schema.
        Migrator(app, schema: "guestbook").MigrateUp();

        await using var appDataSource = NpgsqlDataSource.Create(ConnectionString(app, "guestbook"));
        await using (var command = appDataSource.CreateCommand(
                         "SELECT string_agg(table_name, ',' ORDER BY table_name) FROM information_schema.tables WHERE table_schema = 'guestbook'"))
        {
            Assert.Equal("VersionInfo,guestbook_entries,guestbook_likes,guestbook_reactions,listening_history",
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }

        var entry = await new CreateEntryHandler(new PostgresUnitOfWorkFactory(appDataSource), TimeProvider.System)
            .HandleAsync(new CreateEntryRequest("Ada", "hello from a schema"), "hash-ada", TestContext.Current.CancellationToken);
        Assert.Equal(1, entry.Seq);
    }

    private static DatabaseMigrator Migrator(string connectionString, string? schema) =>
        new(ConnectionString(connectionString, schema), schema ?? AlenAlex.Api.Options.DatabaseOptions.DefaultSchema,
            NullLoggerFactory.Instance);

    /// <summary>The connection string exactly as the app builds it from configuration.</summary>
    private static string ConnectionString(string connectionString, string? schema)
    {
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:Guestbook"] = connectionString };
        if (schema is not null) settings["Database:Schema"] = schema;
        return GuestbookConnectionString.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }
}
