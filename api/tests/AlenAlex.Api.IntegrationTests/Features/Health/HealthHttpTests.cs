using System.Net;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Features.Health;

public sealed class HealthHttpTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Health_returns_plain_ok()
    {
        var response = await _factory.CreateClient().GetAsync("/_health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType!.ToString());
    }
}
