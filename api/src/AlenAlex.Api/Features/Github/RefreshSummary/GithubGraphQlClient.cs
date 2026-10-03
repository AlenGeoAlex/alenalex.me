using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using AlenAlex.Api.Features.Github.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Features.Github.RefreshSummary;

public sealed class GithubGraphQlClient(HttpClient http, IOptions<GithubOptions> options, TimeProvider time)
{
    // 10 pages of 100 repos
    private const int MaxRepoPages = 10;

    private const string ProfileQuery =
        """
        query($login: String!) {
          user(login: $login) {
            login
            contributionsCollection {
              contributionCalendar {
                totalContributions
                weeks { contributionDays { date contributionCount } }
              }
            }
          }
        }
        """;

    private const string ReposQuery =
        """
        query($login: String!, $after: String) {
          user(login: $login) {
            repositories(first: 100, after: $after, ownerAffiliations: [OWNER], privacy: PUBLIC,
                         orderBy: { field: PUSHED_AT, direction: DESC }) {
              totalCount
              pageInfo { hasNextPage endCursor }
              nodes {
                name
                description
                url
                stargazerCount
                isFork
                pushedAt
                createdAt
                primaryLanguage { name }
                repositoryTopics(first: 20) { nodes { topic { name } } }
                languages(first: 20, orderBy: { field: SIZE, direction: DESC }) {
                  edges { size node { name } }
                }
              }
            }
          }
        }
        """;

    public async Task<GithubSummary> FetchSummaryAsync(string login, CancellationToken ct = default)
    {
        var profile = (await QueryAsync(ProfileQuery, new GraphQlVariables(login, null),
                GithubGraphQlJsonContext.Default.GraphQlResponseUserDataProfileUser, ct)).User
            ?? throw new InvalidOperationException($"GitHub user `{login}` not found");

        var repos = new List<GqlRepo>();
        long publicRepos = 0;
        string? after = null;
        for (var page = 0; page < MaxRepoPages; page++)
        {
            var connection = ((await QueryAsync(ReposQuery, new GraphQlVariables(login, after),
                    GithubGraphQlJsonContext.Default.GraphQlResponseUserDataReposUser, ct)).User
                ?? throw new InvalidOperationException("GitHub user disappeared mid-fetch")).Repositories;

            publicRepos = connection.TotalCount;
            repos.AddRange(connection.Nodes);
            if (!connection.PageInfo.HasNextPage)
            {
                break;
            }
            after = connection.PageInfo.EndCursor;
        }

        return SummaryBuilder.Build(profile, publicRepos, repos, time.GetUtcNow());
    }

    private async Task<T> QueryAsync<T>(string query, GraphQlVariables variables, JsonTypeInfo<GraphQlResponse<T>> typeInfo, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(options.Value.GraphQlUrl, new GraphQlRequest(query, variables),
            GithubGraphQlJsonContext.Default.GraphQlRequest, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync(typeInfo, ct)
            ?? throw new InvalidOperationException("Empty GitHub GraphQL response");
        if (body.Errors is { Count: > 0 } errors)
        {
            throw new InvalidOperationException("GitHub GraphQL errors: " + string.Join("; ", errors.Select(e => e.Message)));
        }
        return body.Data ?? throw new InvalidOperationException("GitHub GraphQL response had no data");
    }
}
