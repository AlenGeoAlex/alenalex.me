using System.Net;
using System.Text.Json.Nodes;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Status;

[Collection(WireMockCollection.Name)]
public sealed class HomelabWireMockTests(WireMockFixture wireMock) : IAsyncLifetime
{
    private ApiFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        if (wireMock.SkipReason is not null)
        {
            return;
        }
        await wireMock.ResetAsync();
        await wireMock.StubAsync("""{ "request": { "method": "GET", "url": "/health/ok" }, "response": { "status": 200 } }""");
        await wireMock.StubAsync("""{ "request": { "method": "GET", "url": "/health/broken" }, "response": { "status": 500 } }""");
        await wireMock.StubAsync("""{ "request": { "method": "GET", "url": "/health/login" }, "response": { "status": 302, "headers": { "Location": "/nowhere" } } }""");
        // Longer than the 5 s check timeout.
        await wireMock.StubAsync("""{ "request": { "method": "GET", "url": "/health/slow" }, "response": { "status": 200, "fixedDelayMilliseconds": 8000 } }""");

        var b = wireMock.BaseUrl;
        string[] names = ["ok", "broken", "login", "slow"];
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < names.Length; i++)
        {
            settings[$"Homelab:Services:{i}:Name"] = names[i];
            settings[$"Homelab:Services:{i}:Url"] = $"{b}/health/{names[i]}";
        }
        _factory = new ApiFactory(settings);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reports_each_service_in_configured_order()
    {
        if (wireMock.SkipReason is { } reason) Assert.Skip(reason);
        var client = _factory!.CreateClient();

        // The first poll runs on startup and finishes once the slow check times out (~5 s).
        JsonNode? homelab = null;
        for (var attempt = 0; attempt < 60 && (homelab is null || (int)homelab["total"]! == 0); attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            var (status, body) = await GetAsync(client, "/api/status");
            Assert.Equal(HttpStatusCode.OK, status);
            homelab = body!["homelab"];
        }

        Assert.Equal(2, (int)homelab!["up"]!);
        Assert.Equal(4, (int)homelab["total"]!);
        var services = homelab["services"]!.AsArray();
        Assert.Equal(["ok", "broken", "login", "slow"], services.Select(s => (string)s!["name"]!));
        Assert.Equal([true, false, true, false], services.Select(s => (bool)s!["up"]!));
        // Any answer (even a 500) records latency; a timeout doesn't.
        Assert.NotNull(services[0]!["latencyMs"]);
        Assert.NotNull(services[1]!["latencyMs"]);
        Assert.NotNull(services[2]!["latencyMs"]);
        Assert.Null(services[3]!["latencyMs"]);
        // Redirects are not followed.
        Assert.Equal(0, await wireMock.CountRequestsAsync("""{ "method": "GET", "url": "/nowhere" }"""));
    }
}
