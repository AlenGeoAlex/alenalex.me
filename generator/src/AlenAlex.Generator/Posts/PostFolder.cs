using AlenAlex.Generator.Sync;

namespace AlenAlex.Generator.Posts;

/// <summary>A folder whose <c>assets/</c> are synced to R2: a post, a series part, or a series itself.</summary>
internal interface IAssetFolder
{
    /// <summary>How the folder is shown in output, e.g. <c>my-post</c> or <c>my-series/intro</c>.</summary>
    string DisplayName { get; }

    string AssetsPath { get; }

    string KeyFor(string relativePath);
}

/// <summary>
/// A top-level post under <c>blogs/</c>, or a part inside a series folder. R2 keys use folder names, not
/// slugs: <c>assets/hotlink-ok/&lt;folder&gt;/...</c> or <c>assets/hotlink-ok/&lt;series&gt;/&lt;part&gt;/...</c>.
/// </summary>
/// <param name="SeriesFolderName">The series folder this part lives in, or <c>null</c> for a standalone post.</param>
internal sealed record PostFolder(string Name, string FullPath, string? SeriesFolderName = null) : IAssetFolder
{
    public const string MetaFileName = ".meta";
    public const string MarkdownFileName = "index.md";
    public const string AssetsDirectoryName = "assets";

    public string MetaPath => Path.Combine(FullPath, MetaFileName);
    public string MarkdownPath => Path.Combine(FullPath, MarkdownFileName);
    public string AssetsPath => Path.Combine(FullPath, AssetsDirectoryName);

    public bool IsPart => SeriesFolderName is not null;
    public string DisplayName => IsPart ? $"{SeriesFolderName}/{Name}" : Name;

    /// <summary>A folder without <c>.meta</c> is "not ready" and silently skipped everywhere.</summary>
    public bool IsReady => File.Exists(MetaPath);

    public string KeyFor(string relativePath) =>
        IsPart ? AssetKey.ForPart(SeriesFolderName!, Name, relativePath) : AssetKey.For(Name, relativePath);

    public IReadOnlyList<string> ListAssets() => AssetFiles.List(AssetsPath);
}

/// <summary>
/// Has <c>series.meta</c> (and no <c>.meta</c>), optional series-level <c>assets/</c>, and one post folder per part.
/// </summary>
internal sealed record SeriesFolder(string Name, string FullPath) : IAssetFolder
{
    public const string MetaFileName = "series.meta";

    public string MetaPath => Path.Combine(FullPath, MetaFileName);
    public string AssetsPath => Path.Combine(FullPath, PostFolder.AssetsDirectoryName);
    public string DisplayName => Name;

    public string KeyFor(string relativePath) => AssetKey.For(Name, relativePath);

    public IReadOnlyList<string> ListAssets() => AssetFiles.List(AssetsPath);

    /// <summary>Every subfolder except <c>assets/</c> and hidden ones; ready parts are the ones with a <c>.meta</c>.</summary>
    public IReadOnlyList<PostFolder> PartFolders() =>
        BlogLayout.Subdirectories(FullPath)
            .Where(d => d.Name != PostFolder.AssetsDirectoryName)
            .Select(d => new PostFolder(d.Name, d.FullName, Name))
            .ToList();
}

/// <param name="Conflicts">Folders with both <c>.meta</c> and <c>series.meta</c> (an error).</param>
/// <param name="NotReady">Folders with neither file, including not-ready part folders inside series.</param>
internal sealed record BlogLayout(
    IReadOnlyList<PostFolder> Posts,
    IReadOnlyList<SeriesFolder> Series,
    IReadOnlyList<PostFolder> Conflicts,
    IReadOnlyList<PostFolder> NotReady)
{
    public static BlogLayout Discover(string postsDirectory)
    {
        List<PostFolder> posts = [], conflicts = [], notReady = [];
        List<SeriesFolder> series = [];

        foreach (var dir in Subdirectories(postsDirectory))
        {
            var post = new PostFolder(dir.Name, dir.FullName);
            var isSeries = File.Exists(Path.Combine(dir.FullName, SeriesFolder.MetaFileName));

            if (post.IsReady && isSeries) conflicts.Add(post);
            else if (post.IsReady) posts.Add(post);
            else if (isSeries) series.Add(new SeriesFolder(dir.Name, dir.FullName));
            else notReady.Add(post);
        }

        notReady.AddRange(series.SelectMany(s => s.PartFolders()).Where(p => !p.IsReady));
        return new BlogLayout(posts, series, conflicts, notReady);
    }

    internal static IEnumerable<DirectoryInfo> Subdirectories(string path) =>
        new DirectoryInfo(path)
            .EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.'))
            .OrderBy(d => d.Name, StringComparer.Ordinal);
}

internal static class AssetFiles
{
    /// <summary>All files under <paramref name="assetsPath"/> as '/'-separated relative paths, skipping hidden ones.</summary>
    public static IReadOnlyList<string> List(string assetsPath)
    {
        var root = new DirectoryInfo(assetsPath);
        if (!root.Exists) return [];

        return root
            .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .Select(f => Path.GetRelativePath(root.FullName, f.FullName).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(rel => !rel.Split('/').Any(segment => segment.StartsWith('.')))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();
    }
}
