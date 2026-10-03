namespace AlenAlex.Api.Features.Github.Shared;

/// <summary><see cref="Current"/> is <c>null</c> until the first successful fetch.</summary>
public sealed class GithubSummaryStore
{
    private GithubSummary? _summary;

    public GithubSummary? Current => Volatile.Read(ref _summary);

    public void Set(GithubSummary summary) => Volatile.Write(ref _summary, summary);
}
