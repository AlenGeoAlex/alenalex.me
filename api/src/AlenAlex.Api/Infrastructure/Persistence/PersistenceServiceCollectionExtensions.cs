using AlenAlex.Api.Infrastructure.Persistence.Sqlite;

namespace AlenAlex.Api.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Another database would go in a sibling folder (e.g. <c>Persistence/Postgres</c>) and be
    /// registered here instead.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IUnitOfWorkFactory, SqliteUnitOfWorkFactory>();
        services.AddSingleton<DatabaseMigrator>();
        return services;
    }
}
