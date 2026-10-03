using AlenAlex.Api.Features.Guestbook.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>
/// The real app against its own database in the test PostgreSQL container (migrated on startup),
/// with no Discord, GitHub token or homelab services.
/// Use <paramref name="settings"/> to point GitHub/homelab at WireMock.
/// </summary>
public sealed class ApiFactory(IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "http://localhost:4200";

    private readonly Lazy<string> _connectionString = new(PostgresServer.CreateDatabase);

    /// <summary>Creating the first client creates the database (or skips the test without Docker).</summary>
    public string ConnectionString => _connectionString.Value;

    public FakeModerationNotifier Moderation { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Guestbook"] = ConnectionString,
                ["Database:KeepAliveInterval"] = "00:00:00",
                ["Api:HashingSalt"] = "test",
                ["Api:IpHeader"] = "CF-Connecting-IP",
                ["Api:AllowedOrigins:0"] = AllowedOrigin,
                ["Api:EnableScalar"] = "false",
                // Empty so secrets from the developer's environment don't leak in.
                ["Discord:BotToken"] = "",
                ["Github:Token"] = "",
            });
            if (settings is not null)
            {
                config.AddInMemoryCollection(settings);
            }
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGuestbookModerationNotifier>();
            services.AddSingleton<IGuestbookModerationNotifier>(Moderation);
        });
    }

    public HttpClient CreateClientFor(string ip)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", ip);
        return client;
    }
}
