using AlenAlex.Generator.Validation;
using YamlDotNet.Serialization;

namespace AlenAlex.Generator.Posts;

/// <param name="Part">Position inside a series (<c>part:</c>); only meaningful for series parts.</param>
/// <param name="RevisionsSince"><c>revisions-since:</c> the revisions list on the site starts at this day.</param>
/// <param name="AiAssist"><c>ai-assist:</c> whether AI helped write the post; <c>null</c> when not stated.</param>
internal sealed record PostMeta(
    string Title,
    string? PageTitle,
    DateOnly Date,
    bool Published,
    string? Icon,
    IReadOnlyList<string> Tags,
    string? Type,
    string? Excerpt,
    string? OgImageAsset,
    string? Slug,
    int? Part = null,
    bool? AiAssist = null,
    DateOnly? RevisionsSince = null)
{
    /// <summary><c>slug</c> from .meta, else the slugified title.</summary>
    public string EffectiveSlug => Slug ?? Slugifier.Slugify(Title);
}

/// <param name="Fields">The raw deserialized fields, or <c>null</c> when the YAML itself is invalid.</param>
/// <param name="KeyLines">1-based line of each top-level key, for pointing diagnostics at the right line.</param>
internal sealed record PostMetaResult(
    PostMeta? Meta,
    IReadOnlyList<Diagnostic> Diagnostics,
    MetaFields? Fields,
    IReadOnlyDictionary<string, int> KeyLines)
{
    public bool HasErrors => Meta is null || Diagnostics.Any(d => d.IsError);
}

internal sealed class MetaFields
{
    public string? Title { get; set; }

    [YamlMember(Alias = "page-title", ApplyNamingConventions = false)]
    public string? PageTitle { get; set; }

    public string? Date { get; set; }

    /// <summary>Kept as text so only true/false are accepted (YamlDotNet would also take YAML 1.1 "yes"/"on").</summary>
    public string? Published { get; set; }

    public string? Icon { get; set; }
    public List<string>? Tags { get; set; }
    public string? Type { get; set; }
    public string? Excerpt { get; set; }

    [YamlMember(Alias = "og_image_asset", ApplyNamingConventions = false)]
    public string? OgImageAsset { get; set; }

    public string? Slug { get; set; }

    [YamlMember(Alias = "revisions-since", ApplyNamingConventions = false)]
    public string? RevisionsSince { get; set; }

    /// <summary>Kept as text for the same reason as <see cref="Published"/>.</summary>
    [YamlMember(Alias = "ai-assist", ApplyNamingConventions = false)]
    public string? AiAssist { get; set; }

    /// <summary>Kept as text so a non-number gets a clear message instead of a YAML type error.</summary>
    public string? Part { get; set; }
}

/// <summary>
/// Applies the rules of <c>parseMeta</c> in <c>site/tools/build-content.mjs</c>, plus stricter checks
/// for things the site would trip over later.
/// </summary>
internal static class PostMetaReader
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "title", "page-title", "date", "published", "icon", "tags", "type", "excerpt", "og_image_asset", "slug", "part", "ai-assist", "revisions-since",
    };

    public static PostMetaResult Read(string metaText, string metaPath)
    {
        var fields = MetaYaml.Read<MetaFields>(metaText, metaPath, KnownKeys, out var context);
        if (fields is null) return new PostMetaResult(null, context.Diagnostics, null, context.KeyLines);

        var title = context.RequiredText(fields.Title, "title");
        var date = context.RequiredDate(fields.Date);
        var published = context.Published(fields.Published);
        var tags = context.Tags(fields.Tags);
        var slug = context.Slug(fields.Slug);
        var aiAssist = context.OptionalBool(fields.AiAssist, "ai-assist");
        var revisionsSince = context.OptionalDate(fields.RevisionsSince, "revisions-since");

        var type = MetaReadContext.NullIfEmpty(fields.Type);
        if (type is not null and not "markdown" and not "html")
            context.Error($"`type` must be `markdown` or `html`, got `{type}`", "type");

        int? part = null;
        if (MetaReadContext.NullIfEmpty(fields.Part) is { } rawPart)
        {
            if (int.TryParse(rawPart, out var number) && number > 0) part = number;
            else context.Error($"`part` must be a positive whole number, got `{rawPart}`", "part");
        }

        var meta = title is not null && date is not null
            ? new PostMeta(
                Title: title,
                PageTitle: MetaReadContext.NullIfEmpty(fields.PageTitle),
                Date: date.Value,
                Published: published,
                Icon: MetaReadContext.NullIfEmpty(fields.Icon),
                Tags: tags,
                Type: type,
                Excerpt: MetaReadContext.NullIfEmpty(fields.Excerpt),
                OgImageAsset: MetaReadContext.NullIfEmpty(fields.OgImageAsset),
                Slug: slug,
                Part: part,
                AiAssist: aiAssist,
                RevisionsSince: revisionsSince)
            : null;

        if (meta is { Slug: null } && meta.EffectiveSlug.Length == 0)
            context.Error("the title slugifies to an empty string; add an explicit `slug:`", "title");

        return new PostMetaResult(meta, context.Diagnostics, fields, context.KeyLines);
    }
}
