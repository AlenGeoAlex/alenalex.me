using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AlenAlex.Generator.Storage;
using AlenAlex.Generator.Sync;

namespace AlenAlex.Generator.Tests;

public class ChangedDirsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("a,*,b")]
    [InlineData(",,")]
    public void Empty_or_wildcard_means_all(string? raw)
    {
        var changed = ChangedDirs.Parse(raw);

        Assert.True(changed.IsAll);
        Assert.True(changed.Includes("anything"));
    }

    [Fact]
    public void Parses_comma_list_with_whitespace_and_trailing_comma()
    {
        var changed = ChangedDirs.Parse(" test-blog , another-post,");

        Assert.False(changed.IsAll);
        Assert.Equal(["another-post", "test-blog"], changed.Folders!.Order());
        Assert.True(changed.Includes("test-blog"));
        Assert.False(changed.Includes("other"));
    }

    [Fact]
    public void Reduces_paths_to_folder_names()
    {
        var changed = ChangedDirs.Parse("blogs/test-blog/,blogs/other/index.md,plain/");

        Assert.Equal(["other", "plain", "test-blog"], changed.Folders!.Order());
    }
}

public class AssetKeyTests
{
    [Fact]
    public void Matches_existing_r2_layout()
    {
        Assert.Equal("assets/hotlink-ok/test-blog/rustlogo.png", AssetKey.For("test-blog", "rustlogo.png"));
    }

    [Fact]
    public void Keeps_nested_paths_like_the_site_rewrites_them()
    {
        // build-content.mjs maps `assets/diagrams/flow.png` to `.../hotlink-ok/<folder>/diagrams/flow.png`.
        Assert.Equal("assets/hotlink-ok/post/diagrams/flow.png", AssetKey.For("post", "diagrams/flow.png"));
    }

    [Fact]
    public void Rejects_folder_with_separator() =>
        Assert.Throws<ArgumentException>(() => AssetKey.For("a/b", "x.png"));
}

public class CachePolicyAndContentTypeTests
{
    [Theory]
    [InlineData("rustlogo.png", CachePolicy.Mutable)]
    [InlineData("deadbeef.png", CachePolicy.Mutable)]
    [InlineData("logo.facade.png", CachePolicy.Mutable)]
    [InlineData("logo.3fa9b2c1.png", CachePolicy.Immutable)]
    [InlineData("diagram-0123456789abcdef.svg", CachePolicy.Immutable)]
    [InlineData("nested/flow.3fa9b2c1d4.webp", CachePolicy.Immutable)]
    public void Only_content_addressed_names_are_immutable(string name, string expected) =>
        Assert.Equal(expected, CachePolicy.For(name));

    [Theory]
    [InlineData("a.png", "image/png")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.unknown", "application/octet-stream")]
    [InlineData("noextension", "application/octet-stream")]
    public void Content_type_by_extension(string name, string expected) =>
        Assert.Equal(expected, ContentTypes.For(name));
}

public class AssetSyncServiceTests
{
    private static string Sha256Of(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed class FakeStore : IObjectStore
    {
        public ConcurrentDictionary<string, RemoteObject> Objects { get; } = new();
        public ConcurrentBag<ObjectUpload> Puts { get; } = [];
        public Exception? HeadFailure { get; init; }
        public int HeadCalls;

        public Task<RemoteObject?> HeadAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref HeadCalls);
            if (HeadFailure is not null) throw HeadFailure;
            return Task.FromResult(Objects.GetValueOrDefault(key));
        }

        public Task PutAsync(ObjectUpload upload, CancellationToken cancellationToken)
        {
            Puts.Add(upload);
            Objects[upload.Key] = new RemoteObject(upload.ContentSha256, upload.ContentType, upload.CacheControl, upload.Content.Length);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Uploads_new_files_with_hash_metadata_and_headers()
    {
        using var blog = new TempBlog();
        var folder = blog.AddPost("test-blog", assets: "rustlogo.png");
        var store = new FakeStore();

        var results = await new AssetSyncService(store, SyncMode.Live).SyncAssetsAsync(folder, ["rustlogo.png"], CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(SyncOutcome.Uploaded, result.Outcome);
        var put = Assert.Single(store.Puts);
        Assert.Equal("assets/hotlink-ok/test-blog/rustlogo.png", put.Key);
        Assert.Equal(Sha256Of("content of rustlogo.png"), put.ContentSha256);
        Assert.Equal("image/png", put.ContentType);
        Assert.Equal(CachePolicy.Mutable, put.CacheControl);
    }

    [Fact]
    public async Task Second_run_is_unchanged()
    {
        using var blog = new TempBlog();
        var folder = blog.AddPost("p", assets: "a.png");
        var store = new FakeStore();
        var service = new AssetSyncService(store, SyncMode.Live);

        await service.SyncAssetsAsync(folder, ["a.png"], CancellationToken.None);
        var second = await service.SyncAssetsAsync(folder, ["a.png"], CancellationToken.None);

        Assert.Equal(SyncOutcome.Unchanged, Assert.Single(second).Outcome);
        Assert.Single(store.Puts);
    }

    [Fact]
    public async Task Changed_content_is_updated()
    {
        using var blog = new TempBlog();
        var folder = blog.AddPost("p", assets: "a.png");
        var store = new FakeStore();
        store.Objects["assets/hotlink-ok/p/a.png"] = new RemoteObject("0000", "image/png", CachePolicy.Mutable, 4);

        var result = Assert.Single(await new AssetSyncService(store, SyncMode.Live).SyncAssetsAsync(folder, ["a.png"], CancellationToken.None));

        Assert.Equal(SyncOutcome.Updated, result.Outcome);
        Assert.Equal("content changed", result.Detail);
    }

    [Fact]
    public async Task Dry_run_never_writes()
    {
        using var blog = new TempBlog();
        var folder = blog.AddPost("p", assets: ["new.png", "old.png"]);
        var store = new FakeStore();
        store.Objects["assets/hotlink-ok/p/old.png"] = new RemoteObject(null, null, null, null);

        var results = await new AssetSyncService(store, SyncMode.DryRun).SyncAssetsAsync(folder, ["new.png", "old.png"], CancellationToken.None);

        Assert.Equal([SyncOutcome.WouldUpload, SyncOutcome.WouldUpdate], results.Select(r => r.Outcome));
        Assert.Empty(store.Puts);
    }

    [Fact]
    public async Task Offline_mode_needs_no_store()
    {
        using var blog = new TempBlog();
        var folder = blog.AddPost("p", assets: "a.png");

        var result = Assert.Single(await new AssetSyncService(null, SyncMode.Offline).SyncAssetsAsync(folder, ["a.png"], CancellationToken.None));

        Assert.Equal(SyncOutcome.Planned, result.Outcome);
        Assert.Equal("assets/hotlink-ok/p/a.png", result.Asset.Key);
    }

    [Fact]
    public async Task Fatal_storage_error_is_reported_and_stops_further_requests()
    {
        using var blog = new TempBlog();
        var files = Enumerable.Range(0, 10).Select(i => $"f{i}.png").ToArray();
        var folder = blog.AddPost("p", assets: files);
        var store = new FakeStore { HeadFailure = new ObjectStoreException("HEAD failed: HTTP 403", isFatal: true) };
        var service = new AssetSyncService(store, SyncMode.DryRun, maxParallelism: 1);

        var results = await service.SyncAssetsAsync(folder, files, CancellationToken.None);

        Assert.True(service.Aborted);
        Assert.Equal(SyncOutcome.Error, results[0].Outcome);
        Assert.Contains("403", results[0].Detail);
        Assert.All(results.Skip(1), r => Assert.Equal(SyncOutcome.Skipped, r.Outcome));
        Assert.Equal(1, store.HeadCalls);
    }

    [Theory]
    [InlineData("image/png", "public, max-age=86400", null)]
    [InlineData("image/png", "public,max-age=86400", null)]
    [InlineData("image/jpeg", "public, max-age=86400", "content-type")]
    [InlineData("image/png", null, "cache-control")]
    public void Compare_checks_hash_then_headers(string contentType, string? cacheControl, string? expectedReason)
    {
        var local = new LocalAsset("p", "a.png", "/x/a.png", "assets/hotlink-ok/p/a.png", "abc", 3, "image/png", CachePolicy.Mutable);
        var remote = new RemoteObject("ABC", contentType, cacheControl, 3);

        var reason = AssetSyncService.Compare(local, remote);

        if (expectedReason is null) Assert.Null(reason);
        else Assert.StartsWith(expectedReason, reason!.Description);
    }
}
