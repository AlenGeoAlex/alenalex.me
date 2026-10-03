using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AlenAlex.Api.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // The slim builder keeps the NativeAOT binary small; only TLS is added (managed Postgres requires it).
        services.AddSingleton(sp => new NpgsqlSlimDataSourceBuilder(
                GuestbookConnectionString.FromConfiguration(sp.GetRequiredService<IConfiguration>()))
            .EnableTransportSecurity()
            .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
            .Build());
        services.AddSingleton<IUnitOfWorkFactory, PostgresUnitOfWorkFactory>();
        services.AddSingleton<DatabaseMigrator>();

        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddHostedService<DatabaseKeepAlive>();
        return services;
    }
}
