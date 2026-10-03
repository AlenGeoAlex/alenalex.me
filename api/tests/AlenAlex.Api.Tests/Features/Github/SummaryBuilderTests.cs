using System.Text.Json;
using AlenAlex.Api.Features.Github.RefreshSummary;

namespace AlenAlex.Api.Tests.Features.Github;

public sealed class SummaryBuilderTests
{
    [Fact]
    public void Summarises_graphql_payload()
    {
        var profile = JsonSerializer.Deserialize(
            """
            { "data": { "user": {
              "login": "octo",
              "contributionsCollection": { "contributionCalendar": {
                "totalContributions": 3,
                "weeks": [ { "contributionDays": [
                  { "date": "2026-09-27", "contributionCount": 1 },
                  { "date": "2026-09-28", "contributionCount": 2 } ] } ] } } } } }
            """, GithubGraphQlJsonContext.Default.GraphQlResponseUserDataProfileUser)!.Data!.User!;

        static string Repo(string name, bool fork, string? pushed, int stars, string languages) =>
            $$"""
            { "name": "{{name}}", "description": null, "url": "https://github.com/octo/{{name}}",
              "stargazerCount": {{stars}}, "isFork": {{(fork ? "true" : "false")}},
              "pushedAt": {{(pushed is null ? "null" : $"\"{pushed}\"")}}, "createdAt": "2020-01-01T00:00:00Z",
              "primaryLanguage": null, "repositoryTopics": { "nodes": [ { "topic": { "name": "rust" } } ] },
              "languages": { "edges": {{languages}} } }
            """;

        var repos = JsonSerializer.Deserialize(
            $$"""
            { "data": { "user": { "repositories": {
              "totalCount": 3, "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": [
                {{Repo("old", false, "2024-01-01T00:00:00Z", 1, """[{ "size": 100, "node": { "name": "Rust" } }]""")}},
                {{Repo("new", false, "2026-09-01T00:00:00Z", 2, """[{ "size": 50, "node": { "name": "TypeScript" } }, { "size": 10, "node": { "name": "Rust" } }]""")}},
                {{Repo("forked", true, null, 5, """[{ "size": 9999, "node": { "name": "HTML" } }]""")}}
              ] } } } }
            """, GithubGraphQlJsonContext.Default.GraphQlResponseUserDataReposUser)!.Data!.User!.Repositories;

        var summary = SummaryBuilder.Build(profile, repos.TotalCount, repos.Nodes, DateTimeOffset.UtcNow);

        Assert.Equal("octo", summary.Login);
        Assert.Equal(3, summary.PublicRepos);
        Assert.Equal(8, summary.TotalStars);
        Assert.Equal("Rust", summary.TopLanguage); // a fork with lots of HTML must not win
        Assert.Equal("new", summary.Repos[0].Name);
        Assert.Equal("forked", summary.Repos[^1].Name); // falls back to created_at
        Assert.Equal(summary.Repos[0].PushedAt, summary.LastPushAt);
        Assert.Equal(3, summary.Contributions.Total);
        Assert.Equal(2, summary.Contributions.Weeks[0].Days[1].Count);
        Assert.Equal(["rust"], summary.Repos[0].Topics);
    }
}
