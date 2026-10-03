using AlenAlex.Api.Infrastructure.Persistence.Postgres;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AlenAlex.Api.Tests.Infrastructure;

public sealed class DatabaseKeepAliveTests
{
    [Theory]
    [InlineData("01:00:00", true)]
    [InlineData("00:00:00", true)]
    [InlineData("00:00:10", true)]
    [InlineData("00:00:05", false)]
    [InlineData("-00:01:00", false)]
    public void Validates_the_interval(string interval, bool valid) =>
        Assert.Equal(valid, new DatabaseOptionsValidator()
            .Validate(null, new DatabaseOptions { KeepAliveInterval = TimeSpan.Parse(interval) }).Succeeded);

    [Fact]
    public void Defaults_to_one_hour() => Assert.Equal(TimeSpan.FromHours(1), new DatabaseOptions().KeepAliveInterval);

    [Fact]
    public async Task A_failed_ping_is_reported_not_thrown()
    {
        // Nothing listens on port 1.
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=x;Password=x;Timeout=2");
        var keepAlive = new DatabaseKeepAlive(dataSource, Microsoft.Extensions.Options.Options.Create(new DatabaseOptions()),
            NullLogger<DatabaseKeepAlive>.Instance);

        Assert.False(await keepAlive.PingAsync(TestContext.Current.CancellationToken));
    }
}
