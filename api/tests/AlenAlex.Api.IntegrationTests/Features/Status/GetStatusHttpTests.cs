using System.Net;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Status;

public sealed class GetStatusHttpTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Without_secrets_discord_is_unknown_and_homelab_empty()
    {
        var (status, body) = await GetAsync(_factory.CreateClient(), "/api/status");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["discord", "spotify", "homelab", "fetchedAt"], body!.AsObject().Select(p => p.Key));
        AssertEqual("""{"status":"unknown","activity":null}""", body["discord"]);
        Assert.Null(body["spotify"]);
        AssertEqual("""{"up":0,"total":0,"services":[]}""", body["homelab"]);
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse((string)body["fetchedAt"]!).Offset);
    }
}
