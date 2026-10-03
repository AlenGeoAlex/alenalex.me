using System.Data.Common;
using AlenAlex.Api.Infrastructure.Persistence.Migrations;
using FluentMigrator;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors.Postgres;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace AlenAlex.Api.Infrastructure.Persistence;

/// <summary>
/// FluentMigrator normally scans assemblies for migrations and loads the ADO.NET provider by
/// reflection, neither of which works under NativeAOT. Here the migrations are an explicit list
/// and <see cref="AotPostgresDbFactory"/> returns <see cref="NpgsqlFactory.Instance"/> directly.
/// The runner gets its own service provider so its services stay out of the app's.
/// </summary>
public sealed class DatabaseMigrator(string connectionString, ILoggerFactory loggerFactory)
{
    public DatabaseMigrator(IConfiguration configuration, ILoggerFactory loggerFactory)
        : this(Postgres.GuestbookConnectionString.FromConfiguration(configuration), loggerFactory)
    {
    }

    /// <summary>Order doesn't matter; FluentMigrator sorts by version.</summary>
    public static IMigration[] All() =>
    [
        new M20261004001_InitialSchema(),
    ];

    public void MigrateUp()
    {
        var services = new ServiceCollection()
            .AddSingleton(loggerFactory)
            .AddLogging()
            .AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres15_0()
                .WithGlobalConnectionString(connectionString));

        services.Replace(ServiceDescriptor.Scoped<PostgresDbFactory, AotPostgresDbFactory>());
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

    private sealed class AotPostgresDbFactory() : PostgresDbFactory(serviceProvider: null!)
    {
        protected override DbProviderFactory CreateFactory() => NpgsqlFactory.Instance;
    }
}
