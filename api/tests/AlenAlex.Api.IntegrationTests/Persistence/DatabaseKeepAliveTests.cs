using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using AlenAlex.Api.IntegrationTests.Support;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AlenAlex.Api.IntegrationTests.Persistence;

public sealed class DatabaseKeepAliveTests
{
    [Fact]
    public async Task Pings_a_real_database()
    {
        await using var dataSource = new NpgsqlSlimDataSourceBuilder(await PostgresServer.CreateDatabaseAsync()).Build();
        var keepAlive = new DatabaseKeepAlive(dataSource, Microsoft.Extensions.Options.Options.Create(new DatabaseOptions()),
            NullLogger<DatabaseKeepAlive>.Instance);

        Assert.True(await keepAlive.PingAsync(TestContext.Current.CancellationToken));
    }
}
