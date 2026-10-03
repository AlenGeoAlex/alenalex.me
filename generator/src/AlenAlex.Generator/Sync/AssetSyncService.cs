using System.Security.Cryptography;
using AlenAlex.Generator.Posts;
using AlenAlex.Generator.Storage;

namespace AlenAlex.Generator.Sync;

internal enum SyncMode
{
    /// <summary>HEAD each object and upload what is new or changed.</summary>
    Live,

    /// <summary>HEAD each object (read-only) and report what would be uploaded.</summary>
    DryRun,

    /// <summary>No network at all: report every local asset as a candidate upload.</summary>
    Offline,
}

internal enum SyncOutcome
{
    Uploaded,
    Updated,
    Unchanged,
    WouldUpload,
    WouldUpdate,
    Planned,
    Skipped,
    Error,
}

internal sealed record LocalAsset(
    string Folder,
    string RelativePath,
    string FullPath,
    string Key,
    string Sha256,
    long Length,
    string ContentType,
    string CacheControl)
{
    public static async Task<LocalAsset> LoadAsync(IAssetFolder folder, string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(folder.AssetsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        await using var stream = File.OpenRead(fullPath);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));

        return new LocalAsset(
            folder.DisplayName,
            relativePath,
            fullPath,
            folder.KeyFor(relativePath),
            hash,
            stream.Length,
            ContentTypes.For(relativePath),
            CachePolicy.For(relativePath));
    }
}

internal sealed record AssetSyncResult(LocalAsset Asset, SyncOutcome Outcome, string? Detail = null, bool IsFatal = false);

internal sealed record UploadReason(bool IsNew, string Description);

/// <summary>
/// Uploads a folder's assets, skipping files whose remote copy has the same SHA-256
/// (<c>x-amz-meta-file-metadata</c>) and the same headers.
/// </summary>
internal sealed class AssetSyncService(IObjectStore? store, SyncMode mode, int maxParallelism = 4)
{
    private volatile bool _aborted;

    public bool Aborted => _aborted;

    public async Task<IReadOnlyList<AssetSyncResult>> SyncAssetsAsync(
        IAssetFolder folder, IReadOnlyList<string> relativePaths, CancellationToken cancellationToken)
    {
        if (mode != SyncMode.Offline && store is null)
            throw new InvalidOperationException($"{mode} sync needs an object store.");

        var results = new AssetSyncResult[relativePaths.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, relativePaths.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxParallelism, CancellationToken = cancellationToken },
            async (i, ct) => results[i] = await SyncAssetAsync(folder, relativePaths[i], ct));

        return results;
    }

    private async Task<AssetSyncResult> SyncAssetAsync(IAssetFolder folder, string relativePath, CancellationToken cancellationToken)
    {
        LocalAsset local;
        try
        {
            local = await LocalAsset.LoadAsync(folder, relativePath, cancellationToken);
        }
        catch (IOException ex)
        {
            var placeholder = new LocalAsset(folder.DisplayName, relativePath, Path.Combine(folder.AssetsPath, relativePath),
                folder.KeyFor(relativePath), string.Empty, 0, ContentTypes.For(relativePath), CachePolicy.For(relativePath));
            return new AssetSyncResult(placeholder, SyncOutcome.Error, $"cannot read file: {ex.Message}");
        }

        if (mode == SyncMode.Offline)
            return new AssetSyncResult(local, SyncOutcome.Planned, "remote not checked");

        if (_aborted)
            return new AssetSyncResult(local, SyncOutcome.Skipped, "skipped after a fatal storage error");

        try
        {
            var remote = await store!.HeadAsync(local.Key, cancellationToken);
            var reason = Compare(local, remote);
            if (reason is null) return new AssetSyncResult(local, SyncOutcome.Unchanged);

            if (mode == SyncMode.DryRun)
                return new AssetSyncResult(local, reason.IsNew ? SyncOutcome.WouldUpload : SyncOutcome.WouldUpdate, reason.Description);

            var content = await File.ReadAllBytesAsync(local.FullPath, cancellationToken);
            await store.PutAsync(new ObjectUpload(local.Key, content, local.Sha256, local.ContentType, local.CacheControl), cancellationToken);
            return new AssetSyncResult(local, reason.IsNew ? SyncOutcome.Uploaded : SyncOutcome.Updated, reason.Description);
        }
        catch (ObjectStoreException ex)
        {
            if (ex.IsFatal) _aborted = true;
            return new AssetSyncResult(local, SyncOutcome.Error, ex.Message, ex.IsFatal);
        }
        catch (IOException ex)
        {
            return new AssetSyncResult(local, SyncOutcome.Error, $"cannot read file: {ex.Message}");
        }
    }

    internal static UploadReason? Compare(LocalAsset local, RemoteObject? remote)
    {
        if (remote is null) return new UploadReason(IsNew: true, "new");

        if (remote.ContentHash is null) return new UploadReason(false, "remote has no content hash");
        if (!string.Equals(remote.ContentHash, local.Sha256, StringComparison.OrdinalIgnoreCase))
            return new UploadReason(false, "content changed");
        if (!HeaderEquals(remote.ContentType, local.ContentType))
            return new UploadReason(false, $"content-type {remote.ContentType ?? "(none)"} -> {local.ContentType}");
        if (!HeaderEquals(remote.CacheControl, local.CacheControl))
            return new UploadReason(false, $"cache-control {remote.CacheControl ?? "(none)"} -> {local.CacheControl}");

        return null;
    }

    private static bool HeaderEquals(string? remote, string local) =>
        remote is not null
        && string.Equals(Normalize(remote), Normalize(local), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string header) => header.Replace(" ", string.Empty, StringComparison.Ordinal);
}
