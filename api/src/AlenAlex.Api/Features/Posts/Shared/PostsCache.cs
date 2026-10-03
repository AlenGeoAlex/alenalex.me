using Microsoft.Extensions.Caching.Memory;

namespace AlenAlex.Api.Features.Posts.Shared;

/// <summary>
/// Capped by approximate bytes since the endpoints are public. Failures aren't cached.
/// </summary>
public sealed class PostsCache : IDisposable
{
    public const long SizeLimitBytes = 64 * 1024 * 1024;

    public static readonly TimeSpan RevisionsLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BranchLifetime = TimeSpan.FromMinutes(2);
    /// <summary>Content at a commit sha never changes; this only bounds memory churn.</summary>
    public static readonly TimeSpan ShaLifetime = TimeSpan.FromHours(24);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = SizeLimitBytes });

    public static TimeSpan LifetimeFor(string gitRef) => PostInput.IsCommitSha(gitRef) ? ShaLifetime : BranchLifetime;

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan lifetime, Func<Task<T>> factory, Func<T, long> size)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();
        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime,
            Size = Math.Max(1, size(value)),
        });
        return value;
    }

    public void Dispose() => _cache.Dispose();
}
