using AlenAlex.Generator.Posts;

namespace AlenAlex.Generator.Tests;

internal sealed class TempBlog : IDisposable
{
    public TempBlog()
    {
        Root = Path.Combine(Path.GetTempPath(), "alenalex-generator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public const string ValidMeta = """
        title: How did I use GitHub as CMS?
        date: 2024-10-24
        published: true
        icon: ⚗️
        tags: ["security", "open-source", "backend"]
        type: markdown
        """;

    public PostFolder AddPost(string folder, string? meta = ValidMeta, string? markdown = "# Hello\n", params string[] assets)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        if (meta is not null) File.WriteAllText(Path.Combine(dir, ".meta"), meta);
        if (markdown is not null) File.WriteAllText(Path.Combine(dir, "index.md"), markdown);

        WriteAssets(dir, assets);
        return new PostFolder(folder, dir);
    }

    public const string ValidSeriesMeta = """
        title: Guestbook rewrite
        date: 2025-01-10
        description: Rebuilding the guestbook in Rust.
        tags: ["rust", "series"]
        """;

    public static string PartMeta(string title, int? part = null, string date = "2025-01-10", bool published = true) =>
        $"title: {title}\ndate: {date}\npublished: {(published ? "true" : "false")}\n" + (part is { } n ? $"part: {n}\n" : string.Empty);

    public SeriesFolder AddSeries(string folder, string? seriesMeta = ValidSeriesMeta, params string[] assets)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        if (seriesMeta is not null) File.WriteAllText(Path.Combine(dir, SeriesFolder.MetaFileName), seriesMeta);
        WriteAssets(dir, assets);
        return new SeriesFolder(folder, dir);
    }

    public PostFolder AddPart(string series, string part, string? meta, string? markdown = "# Part\n", params string[] assets)
    {
        var folder = AddPost(Path.Combine(series, part), meta, markdown, assets);
        return new PostFolder(part, folder.FullPath, series);
    }

    private static void WriteAssets(string dir, string[] assets)
    {
        foreach (var asset in assets)
        {
            var path = Path.Combine(dir, "assets", asset.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"content of {asset}");
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the OS cleans the temp folder eventually.
        }
    }
}
