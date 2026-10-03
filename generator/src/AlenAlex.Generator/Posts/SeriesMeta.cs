using AlenAlex.Generator.Validation;
using YamlDotNet.Serialization;

namespace AlenAlex.Generator.Posts;

/// <param name="Date">When the series started.</param>
internal sealed record SeriesMeta(
    string Title,
    DateOnly Date,
    bool Published,
    string? Description,
    IReadOnlyList<string> Tags,
    string? Slug,
    string? CoverAsset)
{
    public string EffectiveSlug => Slug ?? Slugifier.Slugify(Title);
}

internal sealed record SeriesMetaResult(
    SeriesMeta? Meta,
    IReadOnlyList<Diagnostic> Diagnostics,
    SeriesMetaFields? Fields,
    IReadOnlyDictionary<string, int> KeyLines);

internal sealed class SeriesMetaFields
{
    public string? Title { get; set; }
    public string? Date { get; set; }
    public string? Published { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public string? Slug { get; set; }

    [YamlMember(Alias = "cover_asset", ApplyNamingConventions = false)]
    public string? CoverAsset { get; set; }
}

internal static class SeriesMetaReader
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "title", "date", "published", "description", "tags", "slug", "cover_asset",
    };

    public static SeriesMetaResult Read(string text, string path)
    {
        var fields = MetaYaml.Read<SeriesMetaFields>(text, path, KnownKeys, out var context);
        if (fields is null) return new SeriesMetaResult(null, context.Diagnostics, null, context.KeyLines);

        var title = context.RequiredText(fields.Title, "title");
        var date = context.RequiredDate(fields.Date);
        var published = context.Published(fields.Published);
        var tags = context.Tags(fields.Tags);
        var slug = context.Slug(fields.Slug);

        var meta = title is not null && date is not null
            ? new SeriesMeta(
                title,
                date.Value,
                published,
                MetaReadContext.NullIfEmpty(fields.Description),
                tags,
                slug,
                MetaReadContext.NullIfEmpty(fields.CoverAsset))
            : null;

        if (meta is { Slug: null } && meta.EffectiveSlug.Length == 0)
            context.Error("the title slugifies to an empty string; add an explicit `slug:`", "title");

        return new SeriesMetaResult(meta, context.Diagnostics, fields, context.KeyLines);
    }
}
