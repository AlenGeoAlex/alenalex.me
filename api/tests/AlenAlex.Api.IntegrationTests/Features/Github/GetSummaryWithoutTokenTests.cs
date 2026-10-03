using System.Net;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Github;

public sealed class GetSummaryWithoutTokenTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Answers_503_warming_up()
    {
        var (status, body) = await GetAsync(_factory.CreateClient(), "/api/github");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        AssertEqual("""{"error":"warming up"}""", body);
    }
}
