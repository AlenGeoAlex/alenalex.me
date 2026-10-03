using System.Text.Json.Serialization;

namespace AlenAlex.Api.Features.Github.RefreshSummary;

internal sealed record GraphQlRequest(string Query, GraphQlVariables Variables);

internal sealed record GraphQlVariables(string Login, string? After);

internal sealed record GraphQlResponse<T>(T? Data, IReadOnlyList<GraphQlError>? Errors);

internal sealed record GraphQlError(string Message);

internal sealed record UserData<TUser>(TUser? User);

internal sealed record ProfileUser(string Login, ContributionsCollection ContributionsCollection);

internal sealed record ContributionsCollection(ContributionCalendar ContributionCalendar);

internal sealed record ContributionCalendar(long TotalContributions, IReadOnlyList<GqlWeek> Weeks);

internal sealed record GqlWeek(IReadOnlyList<GqlDay> ContributionDays);

internal sealed record GqlDay(string Date, long ContributionCount);

internal sealed record ReposUser(RepoConnection Repositories);

internal sealed record RepoConnection(long TotalCount, PageInfo PageInfo, IReadOnlyList<GqlRepo> Nodes);

internal sealed record PageInfo(bool HasNextPage, string? EndCursor);

/// <param name="PushedAt">Null for repos that never received a push.</param>
internal sealed record GqlRepo(
    string Name,
    string? Description,
    string Url,
    long StargazerCount,
    bool IsFork,
    DateTimeOffset? PushedAt,
    DateTimeOffset CreatedAt,
    Named? PrimaryLanguage,
    TopicConnection RepositoryTopics,
    LanguageConnection Languages);

internal sealed record Named(string Name);

internal sealed record TopicConnection(IReadOnlyList<TopicNode> Nodes);

internal sealed record TopicNode(Named Topic);

internal sealed record LanguageConnection(IReadOnlyList<LanguageEdge> Edges);

internal sealed record LanguageEdge(long Size, Named Node);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GraphQlRequest))]
[JsonSerializable(typeof(GraphQlResponse<UserData<ProfileUser>>))]
[JsonSerializable(typeof(GraphQlResponse<UserData<ReposUser>>))]
internal sealed partial class GithubGraphQlJsonContext : JsonSerializerContext;
