namespace AlenAlex.Generator.Sync;

/// <summary>
/// Comma-separated folder names from <c>--changed</c> or <c>Posts:Changed</c>. <c>*</c> anywhere, or an
/// empty value, means all. Paths such as <c>blogs/my-post/</c> are reduced to the folder name.
/// </summary>
internal sealed record ChangedDirs(IReadOnlySet<string>? Folders)
{
    public static ChangedDirs All { get; } = new((IReadOnlySet<string>?)null);

    public bool IsAll => Folders is null;

    public bool Includes(string folder) => Folders is null || Folders.Contains(folder);

    public static ChangedDirs Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return All;

        var folders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in raw.Split([',', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "*") return All;

            // "blogs/my-post/" or "blogs/my-post/index.md" -> "my-post"
            var segments = part.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var name = segments is ["blogs", var folder, ..] ? folder : segments.FirstOrDefault();
            if (!string.IsNullOrEmpty(name)) folders.Add(name);
        }

        return folders.Count == 0 ? All : new ChangedDirs(folders);
    }

    public override string ToString() => IsAll ? "*" : string.Join(',', Folders!.Order(StringComparer.Ordinal));
}
