using AlenAlex.Generator.Posts;

namespace AlenAlex.Generator.Validation;

/// <param name="MetaKeyLines">Line of each key in <c>.meta</c>, so later cross-post checks can point at it.</param>
/// <param name="SeriesSlug">For a part: the slug of its series (null if the series' own metadata is broken).</param>
/// <param name="SeriesIsDraft">For a part: the whole series is unpublished, so the part is a draft too.</param>
internal sealed record PostReport(
    PostFolder Folder,
    PostMeta? Meta,
    IReadOnlyList<string> Assets,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyDictionary<string, int>? MetaKeyLines = null,
    string? SeriesSlug = null,
    bool SeriesIsDraft = false)
{
    public bool HasErrors => Meta is null || Diagnostics.Any(d => d.IsError);
    public bool IsDraft => Meta is { Published: false } || SeriesIsDraft;
    public string? Slug => Meta?.EffectiveSlug;
    public int? PartNumber => Meta?.Part;

    /// <summary><c>/writing/&lt;slug&gt;</c>, or <c>/writing/&lt;series&gt;/&lt;slug&gt;</c> for a part.</summary>
    public string? Url => (Slug, Folder.IsPart, SeriesSlug) switch
    {
        (null, _, _) => null,
        (var slug, false, _) => $"/writing/{slug}",
        (var slug, true, { } series) => $"/writing/{series}/{slug}",
        _ => null,
    };

    public int? LineOf(string key) => MetaKeyLines is not null && MetaKeyLines.TryGetValue(key, out var line) ? line : null;
}

/// <param name="Parts">Ready parts in reading order.</param>
internal sealed record SeriesReport(
    SeriesFolder Folder,
    SeriesMeta? Meta,
    IReadOnlyList<string> Assets,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<PostReport> Parts)
{
    public bool HasErrors => Meta is null || Diagnostics.Any(d => d.IsError) || Parts.Any(p => p.HasErrors);
    public bool IsDraft => Meta is { Published: false };
    public string? Slug => Meta?.EffectiveSlug;
    public string? Url => Slug is { } slug ? $"/writing/{slug}" : null;
}

internal sealed record ValidationReport(
    IReadOnlyList<PostReport> Posts,
    IReadOnlyList<SeriesReport> Series,
    IReadOnlyList<PostFolder> NotReady)
{
    private IEnumerable<Diagnostic> AllDiagnostics =>
        Posts.SelectMany(p => p.Diagnostics)
            .Concat(Series.SelectMany(s => s.Diagnostics.Concat(s.Parts.SelectMany(p => p.Diagnostics))));

    public int ErrorCount => AllDiagnostics.Count(d => d.IsError);
    public int WarningCount => AllDiagnostics.Count(d => !d.IsError);
    public bool HasErrors => Posts.Any(p => p.HasErrors) || Series.Any(s => s.HasErrors);
}

/// <summary>
/// Checks posts the way the site build and the R2 sync consume them, so a broken post fails CI
/// with a file and line instead of shipping a broken page.
/// </summary>
internal static class PostValidator
{
    public static ValidationReport ValidateAll(string postsDirectory)
    {
        var layout = BlogLayout.Discover(postsDirectory);

        var posts = layout.Posts.Select(ValidateStandalone).ToList();
        posts.AddRange(layout.Conflicts.Select(folder =>
        {
            var report = ValidateStandalone(folder);
            var conflict = Diagnostic.Error(folder.MetaPath,
                $"folder has both .meta and {SeriesFolder.MetaFileName}; keep .meta for a single post or {SeriesFolder.MetaFileName} for a series");
            return report with { Diagnostics = [.. report.Diagnostics, conflict] };
        }));
        posts.Sort((a, b) => StringComparer.Ordinal.Compare(a.Folder.Name, b.Folder.Name));

        var series = layout.Series.Select(ValidateSeries).ToList();

        AddTopLevelDuplicateSlugErrors(posts, series);
        return new ValidationReport(posts, series, layout.NotReady);
    }

    private static PostReport ValidateStandalone(PostFolder folder)
    {
        var report = ValidateFolder(folder);
        if (report.PartNumber is null) return report;

        var warning = Diagnostic.Warning(folder.MetaPath, "`part` is only used for posts inside a series folder (ignored here)", report.LineOf("part"));
        return report with { Diagnostics = [.. report.Diagnostics, warning] };
    }

    /// <summary>Everything except cross-post checks.</summary>
    public static PostReport ValidateFolder(PostFolder folder)
    {
        var diagnostics = new List<Diagnostic>();

        var metaResult = PostMetaReader.Read(File.ReadAllText(folder.MetaPath), folder.MetaPath);
        diagnostics.AddRange(metaResult.Diagnostics);

        var assets = folder.ListAssets();
        CheckAssetsIsDirectory(folder.AssetsPath, diagnostics);

        var referenced = new HashSet<string>(StringComparer.Ordinal);

        if (!File.Exists(folder.MarkdownPath))
        {
            diagnostics.Add(Diagnostic.Error(folder.MarkdownPath, "missing index.md (the folder has a .meta, so it is treated as a post)"));
        }
        else
        {
            foreach (var reference in AssetReferenceScanner.Scan(File.ReadAllText(folder.MarkdownPath)))
            {
                referenced.Add(reference.RelativePath);
                var problem = CheckAsset(reference.RelativePath, assets);
                if (problem is not null)
                    diagnostics.Add(Diagnostic.Error(folder.MarkdownPath, $"`{reference.Raw}`: {problem}", reference.Line));
            }
        }

        // Checked from the raw fields so it is reported even when other .meta fields are broken.
        CheckMetaAsset(metaResult.Fields?.OgImageAsset, "og_image_asset", folder.MetaPath, metaResult.KeyLines, assets, referenced, diagnostics);
        WarnUnreferenced(folder.AssetsPath, assets, referenced, "not referenced by index.md or og_image_asset (it is still uploaded)", diagnostics);

        return new PostReport(folder, metaResult.Meta, assets, diagnostics, metaResult.KeyLines);
    }

    public static SeriesReport ValidateSeries(SeriesFolder folder)
    {
        var diagnostics = new List<Diagnostic>();
        var metaResult = SeriesMetaReader.Read(File.ReadAllText(folder.MetaPath), folder.MetaPath);
        diagnostics.AddRange(metaResult.Diagnostics);
        var meta = metaResult.Meta;

        var assets = folder.ListAssets();
        CheckAssetsIsDirectory(folder.AssetsPath, diagnostics);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        CheckMetaAsset(metaResult.Fields?.CoverAsset, "cover_asset", folder.MetaPath, metaResult.KeyLines, assets, referenced, diagnostics);
        WarnUnreferenced(folder.AssetsPath, assets, referenced, "not referenced by cover_asset (it is still uploaded)", diagnostics);

        var parts = folder.PartFolders().Where(p => p.IsReady).Select(ValidateFolder).ToList();

        if (parts.Count == 0)
            diagnostics.Add(Diagnostic.Warning(folder.MetaPath, "series has no ready parts (no subfolder with a .meta)"));

        // Numbering: duplicates are errors, gaps and numbered/unnumbered mixes are warnings.
        var numbered = parts.Where(p => p.PartNumber is not null).ToList();
        var unnumbered = parts.Where(p => p.Meta is not null && p.PartNumber is null).ToList();

        foreach (var group in numbered.GroupBy(p => p.PartNumber!.Value).Where(g => g.Count() > 1))
        {
            foreach (var part in group)
            {
                var others = string.Join(", ", group.Where(p => p != part).Select(p => p.Folder.Name));
                AddTo(parts, part, Diagnostic.Error(part.Folder.MetaPath, $"duplicate `part: {group.Key}` in this series, also used by: {others}", part.LineOf("part")));
            }
        }

        var numbers = numbered.Select(p => p.PartNumber!.Value).Distinct().Order().ToList();
        if (numbers.Count > 0)
        {
            var missing = Enumerable.Range(1, numbers[^1]).Except(numbers).ToList();
            if (missing.Count > 0)
                diagnostics.Add(Diagnostic.Warning(folder.MetaPath, $"part numbers have gaps: no part {string.Join(", ", missing)}"));
        }

        if (numbered.Count > 0 && unnumbered.Count > 0)
        {
            diagnostics.Add(Diagnostic.Warning(folder.MetaPath,
                $"some parts have `part:` and some do not ({string.Join(", ", unnumbered.Select(p => p.Folder.Name))}); "
                + "unnumbered parts are listed after the numbered ones"));
        }

        // Part slugs only need to be unique within their series (they live under /writing/<series>/).
        foreach (var (name, diagnostic) in DuplicateSlugErrors(parts
                     .Where(p => p.Slug is not null)
                     .Select(p => new SlugOwner(p.Folder.Name, p.Folder.Name, p.Slug!, p.Folder.MetaPath, p.Meta!.Slug is null)),
                     "in this series"))
        {
            AddTo(parts, parts.Single(p => p.Folder.Name == name), diagnostic);
        }

        // Parts publish in reading order; otherwise readers would meet part 2 with no part 1
        // (and the site would renumber it).
        if (meta is not { Published: false })
        {
            PostReport? firstDraft = null;
            var position = 0;
            foreach (var part in Order(parts).Where(p => p.Meta is not null))
            {
                position++;
                if (part.Meta!.Published is false)
                {
                    firstDraft ??= part;
                }
                else if (firstDraft is not null)
                {
                    var draftName = firstDraft.PartNumber is { } n ? $"part {n}" : "an earlier part";
                    AddTo(parts, part, Diagnostic.Error(part.Folder.MetaPath,
                        $"published, but {draftName} (`{firstDraft.Folder.Name}`) is still a draft; publish parts in order "
                        + $"(set `published: false` here, or publish `{firstDraft.Folder.Name}` first)",
                        part.LineOf("published") ?? part.LineOf("title")));
                }
            }
        }

        var ordered = Order(parts)
            .Select(p => p with { SeriesSlug = meta?.EffectiveSlug, SeriesIsDraft = meta is { Published: false } })
            .ToList();

        return new SeriesReport(folder, meta, assets, diagnostics, ordered);
    }

    /// <summary>
    /// Reading order: numbered parts by <c>part</c>; then unnumbered ones by date, then slug;
    /// parts whose .meta is broken go last, by folder name.
    /// </summary>
    internal static IEnumerable<PostReport> Order(IEnumerable<PostReport> parts) =>
        parts
            .OrderBy(p => p.Meta is null ? 2 : p.PartNumber is null ? 1 : 0)
            .ThenBy(p => p.PartNumber ?? 0)
            .ThenBy(p => p.Meta?.Date ?? DateOnly.MaxValue)
            .ThenBy(p => p.Slug ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.Folder.Name, StringComparer.Ordinal);

    /// <summary><c>null</c> when the asset exists, else the reason.</summary>
    internal static string? CheckAsset(string relativePath, IReadOnlyList<string> assets)
    {
        if (relativePath.Length == 0) return "empty asset path";
        if (relativePath.Split('/').Any(s => s is ".." or "."))
            return "asset paths must stay inside assets/ (no `.` or `..` segments)";
        if (assets.Contains(relativePath, StringComparer.Ordinal)) return null;

        // macOS is case-insensitive; Linux CI, R2 keys and URLs are not.
        var caseMismatch = assets.FirstOrDefault(a => string.Equals(a, relativePath, StringComparison.OrdinalIgnoreCase));
        return caseMismatch is not null
            ? $"file name case differs from assets/{caseMismatch} (R2 keys and URLs are case-sensitive)"
            : $"assets/{relativePath} does not exist";
    }

    private static void CheckAssetsIsDirectory(string assetsPath, List<Diagnostic> diagnostics)
    {
        if (!Directory.Exists(assetsPath) && File.Exists(assetsPath))
            diagnostics.Add(Diagnostic.Error(assetsPath, "`assets` must be a directory"));
    }

    /// <summary>og_image_asset or cover_asset.</summary>
    private static void CheckMetaAsset(
        string? value, string key, string metaPath, IReadOnlyDictionary<string, int> keyLines,
        IReadOnlyList<string> assets, HashSet<string> referenced, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(value)) return;

        var path = AssetReferenceScanner.NormalizeOgImage(value);
        referenced.Add(path);
        var problem = CheckAsset(path, assets);
        if (problem is not null)
            diagnostics.Add(Diagnostic.Error(metaPath, $"`{key}: {value}`: {problem}", keyLines.TryGetValue(key, out var line) ? line : null));
    }

    private static void WarnUnreferenced(
        string assetsPath, IReadOnlyList<string> assets, HashSet<string> referenced, string message, List<Diagnostic> diagnostics)
    {
        foreach (var asset in assets.Where(a => !referenced.Contains(a)))
            diagnostics.Add(Diagnostic.Warning(Path.Combine(assetsPath, asset), message));
    }

    private static void AddTo(List<PostReport> reports, PostReport report, Diagnostic diagnostic)
    {
        var index = reports.FindIndex(r => r.Folder == report.Folder);
        reports[index] = reports[index] with { Diagnostics = [.. reports[index].Diagnostics, diagnostic] };
    }

    /// <summary>Published posts and series share /writing/&lt;slug&gt;; drafts don't count (as in build-content.mjs).</summary>
    private static void AddTopLevelDuplicateSlugErrors(List<PostReport> posts, List<SeriesReport> series)
    {
        var owners = posts
            .Where(p => p.Meta is { Published: true })
            .Select(p => new SlugOwner(p.Folder.Name, p.Folder.Name, p.Slug!, p.Folder.MetaPath, p.Meta!.Slug is null))
            .Concat(series
                .Where(s => s.Meta is { Published: true })
                .Select(s => new SlugOwner(s.Folder.Name, $"series {s.Folder.Name}", s.Slug!, s.Folder.MetaPath, s.Meta!.Slug is null)));

        foreach (var (name, diagnostic) in DuplicateSlugErrors(owners, scope: null))
        {
            var postIndex = posts.FindIndex(p => p.Folder.Name == name);
            if (postIndex >= 0)
            {
                posts[postIndex] = posts[postIndex] with { Diagnostics = [.. posts[postIndex].Diagnostics, diagnostic] };
                continue;
            }

            var seriesIndex = series.FindIndex(s => s.Folder.Name == name);
            series[seriesIndex] = series[seriesIndex] with { Diagnostics = [.. series[seriesIndex].Diagnostics, diagnostic] };
        }
    }

    /// <param name="Label">How the owner is named in other owners' messages.</param>
    /// <param name="Derived">The slug comes from the title, not an explicit <c>slug:</c>.</param>
    private sealed record SlugOwner(string Name, string Label, string Slug, string MetaPath, bool Derived);

    private static IEnumerable<(string Name, Diagnostic Diagnostic)> DuplicateSlugErrors(IEnumerable<SlugOwner> owners, string? scope)
    {
        foreach (var group in owners.GroupBy(o => o.Slug, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            foreach (var owner in group)
            {
                var others = string.Join(", ", group.Where(o => o != owner).Select(o => o.Label));
                var hint = owner.Derived ? " (derived from the title; set `slug:` to override)" : string.Empty;
                var where = scope is null ? string.Empty : $" {scope}";
                yield return (owner.Name, Diagnostic.Error(owner.MetaPath, $"duplicate slug `{group.Key}`{where}{hint}, also used by: {others}"));
            }
        }
    }
}
