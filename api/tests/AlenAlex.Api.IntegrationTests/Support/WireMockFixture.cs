using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>
/// Stands in for api.github.com and the homelab services. Tests are skipped when Docker is
/// unavailable (see <see cref="SkipReason"/>).
/// </summary>
public sealed class WireMockFixture : IAsyncLifetime
{
    public const string Image = "wiremock/wiremock:3.13.1";

    private IContainer? _container;
    private HttpClient? _admin;

    /// <summary>Non-null when the container could not be started (e.g. no Docker).</summary>
    public string? SkipReason { get; private set; }

    public string BaseUrl { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder(Image)
                .WithPortBinding(8080, assignRandomHostPort: true)
                .WithCommand("--disable-banner")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/__admin/health")))
                .Build();
            await _container.StartAsync();
            BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}";
            _admin = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }
        catch (Exception ex)
        {
            SkipReason = $"Docker is not available, skipping WireMock integration tests ({ex.GetType().Name}: {ex.Message})";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _admin?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task ResetAsync() => (await _admin!.PostAsync("/__admin/reset", null)).EnsureSuccessStatusCode();

    public async Task StubAsync(string mappingJson)
    {
        using var content = new StringContent(mappingJson, System.Text.Encoding.UTF8, "application/json");
        (await _admin!.PostAsync("/__admin/mappings", content)).EnsureSuccessStatusCode();
    }

    public async Task<int> CountRequestsAsync(string patternJson)
    {
        using var content = new StringContent(patternJson, System.Text.Encoding.UTF8, "application/json");
        var response = await _admin!.PostAsync("/__admin/requests/count", content);
        response.EnsureSuccessStatusCode();
        return (int)(await response.Content.ReadFromJsonAsync<JsonObject>())!["count"]!;
    }
}

[CollectionDefinition(Name)]
public sealed class WireMockCollection : ICollectionFixture<WireMockFixture>
{
    public const string Name = "WireMock";
}
