using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using AlenAlex.Api.Infrastructure.Persistence.Migrations;
using AlenAlex.Api.Infrastructure.Persistence.Sqlite;
using FluentMigrator;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlenAlex.Api.Infrastructure.Persistence;

/// <summary>
/// FluentMigrator normally scans assemblies for migrations and loads the ADO.NET provider by
/// reflection, neither of which works under NativeAOT. Here the migrations are an explicit
/// list and <see cref="AotSqliteDbFactory"/> returns <see cref="SqliteFactory.Instance"/> directly.
/// The runner gets its own service provider so its services stay out of the app's.
/// </summary>
public sealed class DatabaseMigrator(SqliteConnectionFactory connections, ILoggerFactory loggerFactory)
{
    /// <summary>Order doesn't matter; FluentMigrator sorts by version.</summary>
    public static IMigration[] All() =>
    [
        new M20260711001_InitialSchema(),
        new M20261002001_NormalizeStatusAndSeq(),
    ];

    public void MigrateUp()
    {
        using (var connection = new SqliteConnection(connections.ConnectionString))
        {
            // Persists in the database file.
            connection.Open();
            using var wal = connection.CreateCommand();
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            wal.ExecuteNonQuery();
        }

        var services = new ServiceCollection()
            .AddSingleton(loggerFactory)
            .AddLogging()
            .AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddSQLite()
                .WithGlobalConnectionString(connections.ConnectionString));

        services.Replace(ServiceDescriptor.Scoped<SQLiteDbFactory, AotSqliteDbFactory>());
        services.Replace(ServiceDescriptor.Scoped<IFilteringMigrationSource, ExplicitMigrationSource>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    private sealed class ExplicitMigrationSource : IFilteringMigrationSource
    {
        public IEnumerable<IMigration> GetMigrations(Func<Type, bool> predicate) =>
            All().Where(m => predicate(m.GetType()));
    }

    private sealed class AotSqliteDbFactory() : SQLiteDbFactory(serviceProvider: null!)
    {
        protected override DbProviderFactory CreateFactory() => SqliteFactory.Instance;
    }
}
