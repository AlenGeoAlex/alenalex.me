namespace AlenAlex.Api.Features.Github.Shared;

/// <param name="TopLanguage">Language with the most bytes summed across own non-fork repos.</param>
/// <param name="Repos">Own public repos, most recently pushed first.</param>
public sealed record GithubSummary(
    string Login,
    long PublicRepos,
    long TotalStars,
    string? TopLanguage,
    DateTimeOffset? LastPushAt,
    Contributions Contributions,
    IReadOnlyList<Repo> Repos,
    DateTimeOffset FetchedAt);

public sealed record Contributions(long Total, IReadOnlyList<ContributionWeek> Weeks);

public sealed record ContributionWeek(IReadOnlyList<ContributionDay> Days);

/// <param name="Date"><c>YYYY-MM-DD</c></param>
public sealed record ContributionDay(string Date, long Count);

public sealed record Repo(
    string Name,
    string? Description,
    string Url,
    long Stars,
    string? Language,
    IReadOnlyList<string> Topics,
    DateTimeOffset PushedAt,
    DateTimeOffset CreatedAt,
    bool Fork);
