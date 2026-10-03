using AlenAlex.Api.Features.Github.Shared;

namespace AlenAlex.Api.Features.Github.RefreshSummary;

internal static class SummaryBuilder
{
    public static GithubSummary Build(ProfileUser profile, long publicRepos, IReadOnlyList<GqlRepo> gqlRepos, DateTimeOffset now)
    {
        // Most bytes across non-fork repos; ties go to the alphabetically first name.
        var topLanguage = gqlRepos
            .Where(r => !r.IsFork)
            .SelectMany(r => r.Languages.Edges)
            .GroupBy(e => e.Node.Name, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Bytes: g.Sum(e => e.Size)))
            .OrderByDescending(l => l.Bytes)
            .ThenBy(l => l.Name, StringComparer.Ordinal)
            .Select(l => l.Name)
            .FirstOrDefault();

        var repos = gqlRepos
            .Select(r => new Repo(
                Name: r.Name,
                Description: string.IsNullOrWhiteSpace(r.Description) ? null : r.Description,
                Url: r.Url,
                Stars: r.StargazerCount,
                Language: r.PrimaryLanguage?.Name,
                Topics: r.RepositoryTopics.Nodes.Select(n => n.Topic.Name).ToList(),
                // Empty repos have no push.
                PushedAt: r.PushedAt ?? r.CreatedAt,
                CreatedAt: r.CreatedAt,
                Fork: r.IsFork))
            .OrderByDescending(r => r.PushedAt)
            .ToList();

        var calendar = profile.ContributionsCollection.ContributionCalendar;

        return new GithubSummary(
            Login: profile.Login,
            PublicRepos: publicRepos,
            TotalStars: repos.Sum(r => r.Stars),
            TopLanguage: topLanguage,
            LastPushAt: repos.Count > 0 ? repos[0].PushedAt : null,
            Contributions: new Contributions(
                calendar.TotalContributions,
                calendar.Weeks
                    .Select(w => new ContributionWeek(w.ContributionDays.Select(d => new ContributionDay(d.Date, d.ContributionCount)).ToList()))
                    .ToList()),
            Repos: repos,
            FetchedAt: now);
    }
}
