using System.Net;
using System.Text.Json.Nodes;
using AlenAlex.Api.IntegrationTests.Support;
using static AlenAlex.Api.IntegrationTests.Support.Json;

namespace AlenAlex.Api.IntegrationTests.Features.Github;

[Collection(WireMockCollection.Name)]
public sealed class GetSummaryWireMockTests(WireMockFixture wireMock) : IAsyncLifetime
{
    private ApiFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        if (wireMock.SkipReason is not null)
        {
            return;
        }
        await wireMock.ResetAsync();
        await wireMock.StubAsync(
            """
            { "request": { "method": "POST", "url": "/graphql",
                "headers": { "Authorization": { "equalTo": "Bearer test-token" } },
                "bodyPatterns": [ { "contains": "contributionsCollection" } ] },
              "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody":
                { "data": { "user": { "login": "AlenGeoAlex", "contributionsCollection": { "contributionCalendar": {
                  "totalContributions": 5, "weeks": [ { "contributionDays": [
                    { "date": "2026-09-27", "contributionCount": 2 }, { "date": "2026-09-28", "contributionCount": 3 } ] } ] } } } } } } }
            """);
        await wireMock.StubAsync(
            """
            { "request": { "method": "POST", "url": "/graphql", "bodyPatterns": [ { "contains": "repositories(" } ] },
              "response": { "status": 200, "headers": { "Content-Type": "application/json" }, "jsonBody":
                { "data": { "user": { "repositories": {
                  "totalCount": 2, "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    { "name": "alenalex.me", "description": "personal site", "url": "https://github.com/AlenGeoAlex/alenalex.me",
                      "stargazerCount": 3, "isFork": false, "pushedAt": "2026-10-01T10:00:00Z", "createdAt": "2024-01-01T00:00:00Z",
                      "primaryLanguage": { "name": "C#" }, "repositoryTopics": { "nodes": [ { "topic": { "name": "dotnet" } } ] },
                      "languages": { "edges": [ { "size": 900, "node": { "name": "C#" } }, { "size": 100, "node": { "name": "TypeScript" } } ] } },
                    { "name": "fork", "description": " ", "url": "https://github.com/AlenGeoAlex/fork",
                      "stargazerCount": 1, "isFork": true, "pushedAt": null, "createdAt": "2023-01-01T00:00:00Z",
                      "primaryLanguage": null, "repositoryTopics": { "nodes": [] },
                      "languages": { "edges": [ { "size": 99999, "node": { "name": "HTML" } } ] } } ] } } } } } }
            """);

        _factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["Github:Token"] = "test-token",
            ["Github:GraphQlUrl"] = $"{wireMock.BaseUrl}/graphql",
            ["Github:ApiUrl"] = $"{wireMock.BaseUrl}/",
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Serves_the_summary_once_the_first_refresh_succeeded()
    {
        if (wireMock.SkipReason is { } reason) Assert.Skip(reason);
        var client = _factory!.CreateClient();

        // The refresh job runs on startup; wait for it.
        (HttpStatusCode Status, JsonNode? Body) result = default;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            result = await GetAsync(client, "/api/github");
            if (result.Status == HttpStatusCode.OK)
            {
                break;
            }
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var body = result.Body!.AsObject();
        Assert.Equal(["login", "publicRepos", "totalStars", "topLanguage", "lastPushAt", "contributions", "repos", "fetchedAt"],
            body.Select(p => p.Key));
        Assert.Equal("AlenGeoAlex", (string?)body["login"]);
        Assert.Equal(2, (long)body["publicRepos"]!);
        Assert.Equal(4, (long)body["totalStars"]!);
        Assert.Equal("C#", (string?)body["topLanguage"]);
        Assert.Equal("2026-10-01T10:00:00+00:00", (string?)body["lastPushAt"]);
        AssertEqual("""{"total":5,"weeks":[{"days":[{"date":"2026-09-27","count":2},{"date":"2026-09-28","count":3}]}]}""", body["contributions"]);
        AssertEqual(
            """
            [ { "name": "alenalex.me", "description": "personal site", "url": "https://github.com/AlenGeoAlex/alenalex.me", "stars": 3,
                "language": "C#", "topics": ["dotnet"], "pushedAt": "2026-10-01T10:00:00+00:00", "createdAt": "2024-01-01T00:00:00+00:00", "fork": false },
              { "name": "fork", "description": null, "url": "https://github.com/AlenGeoAlex/fork", "stars": 1,
                "language": null, "topics": [], "pushedAt": "2023-01-01T00:00:00+00:00", "createdAt": "2023-01-01T00:00:00+00:00", "fork": true } ]
            """, body["repos"]);
    }
}
