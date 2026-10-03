#!/usr/bin/env dotnet
// Scaffolds a new blog post, series, or series part under blogs/.
//
//   dotnet run tools/new-post.cs                 interactive
//   dotnet run tools/new-post.cs -- --dry-run    show what would be created, write nothing
//   dotnet run tools/new-post.cs -- --blogs <dir>
//   dotnet run tools/new-post.cs -- --help        how posts, series, drafts and publishing work
//
// Layout it writes (see generator/README.md for the full rules):
//   blogs/<folder>/.meta, index.md, assets/                 a standalone post   → /writing/<slug>
//   blogs/<series>/series.meta, assets/                     a series            → /writing/<series-slug>
//   blogs/<series>/<part>/.meta, index.md, assets/          a part              → /writing/<series-slug>/<part-slug>
#:package Spectre.Console@0.57.2
#:package YamlDotNet@18.1.0
#:property PublishAot=false

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using YamlDotNet.Serialization;

if (args.Any(a => a is "--help" or "-h" or "help"))
{
    Help.PrintAll();
    return 0;
}

var dryRun = args.Contains("--dry-run");
var blogsArg = args.SkipWhile(a => a != "--blogs").Skip(1).FirstOrDefault();

var blogs = Blogs.Locate(blogsArg);
if (blogs is null)
{
    AnsiConsole.MarkupLine("[red]Couldn't find a blogs/ folder.[/] Run this from the repo, or pass [bold]--blogs <dir>[/].");
    return 1;
}

AnsiConsole.Write(new Rule("[bold]alenalex.me[/] · new writing").LeftJustified().RuleStyle("grey"));
AnsiConsole.MarkupLine($"[grey]blogs:[/] {Markup.Escape(blogs.Root)}{(dryRun ? "  [yellow](dry run: nothing is written)[/]" : "")}");
AnsiConsole.WriteLine();

var catalog = Catalog.Read(blogs.Root);

const string HowItWorks = "How does this work?";
string[] kinds = catalog.Series.Count > 0
    ? ["A standalone post", "A new series", "A new part of an existing series", HowItWorks]
    : ["A standalone post", "A new series", HowItWorks];

string kind;
while ((kind = Prompts.Choose("What are you writing?", kinds, k => k == HowItWorks ? "[grey]How does this work?[/]" : k)) == HowItWorks)
    Help.Browse();

Help.Intro(kind switch
{
    "A standalone post" => "post",
    "A new series" => "series",
    _ => "part",
});

var plan = new List<PlannedFile>();
string url;

switch (kind)
{
    case "A standalone post":
    {
        var post = Prompts.Post(catalog, parentDir: blogs.Root, series: null);
        plan.AddRange(post.Files);
        url = $"/writing/{post.Slug}";
        break;
    }
    case "A new series":
    {
        var series = Prompts.Series(catalog, blogs.Root);
        plan.AddRange(series.Files);
        url = $"/writing/{series.Slug}";

        if (AnsiConsole.Confirm("Write the first part now?", defaultValue: true))
        {
            var empty = new SeriesInfo(series.Title, series.Slug, series.Folder, series.Dir, Parts: []);
            var part = Prompts.Post(catalog, parentDir: series.Dir, series: empty);
            plan.AddRange(part.Files);
            url = $"/writing/{series.Slug}/{part.Slug}";
        }
        break;
    }
    default:
    {
        var series = Prompts.Choose("Which series?", catalog.Series,
            s => $"{Markup.Escape(s.Title)} [grey]({s.Folder}, {s.Parts.Count} part{(s.Parts.Count == 1 ? "" : "s")})[/]");
        var part = Prompts.Post(catalog, parentDir: series.Dir, series: series);
        plan.AddRange(part.Files);
        url = $"/writing/{series.Slug}/{part.Slug}";
        break;
    }
}

AnsiConsole.WriteLine();
// assets/.gitkeep is shown as an empty assets/ folder
var tree = new Tree($"[bold]{Markup.Escape(Path.GetFileName(blogs.Root))}/[/]");
var folders = new Dictionary<string, IHasTreeNodes>(StringComparer.Ordinal) { [""] = tree };
IHasTreeNodes FolderNode(string relative)
{
    if (folders.TryGetValue(relative, out var existing)) return existing;
    var parent = FolderNode(Path.GetDirectoryName(relative) ?? "");
    return folders[relative] = parent.AddNode($"[blue]{Markup.Escape(Path.GetFileName(relative))}/[/]");
}
foreach (var file in plan)
{
    var relative = Path.GetRelativePath(blogs.Root, file.Path);
    if (Path.GetFileName(relative) == ".gitkeep") FolderNode(Path.GetDirectoryName(relative)!);
    else FolderNode(Path.GetDirectoryName(relative) ?? "").AddNode(Markup.Escape(Path.GetFileName(relative)));
}
AnsiConsole.Write(new Panel(tree).Header("will create").BorderColor(Color.Grey));
AnsiConsole.MarkupLine($"[grey]url:[/] {Markup.Escape(url)}");

if (dryRun)
{
    AnsiConsole.MarkupLine("[yellow]dry run: nothing was written.[/]");
    return 0;
}
if (!AnsiConsole.Confirm("Create these files?", defaultValue: true))
{
    AnsiConsole.MarkupLine("[grey]Cancelled. Nothing was written.[/]");
    return 0;
}

foreach (var file in plan)
{
    Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
    File.WriteAllText(file.Path, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

AnsiConsole.MarkupLine($"[green]Created {plan.Count} file(s).[/]");
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold]Next[/]");
AnsiConsole.MarkupLine("  1. Write in [blue]index.md[/]; put images in [blue]assets/[/] and reference them as [grey]assets/<file>[/].");
AnsiConsole.MarkupLine("  2. Preview: [grey]cd site && npm start[/] → http://localhost:4200" + Markup.Escape(url));
AnsiConsole.MarkupLine("  3. Check:   [grey]cd generator && dotnet run --project src/AlenAlex.Generator -- validate[/]");
AnsiConsole.MarkupLine("  4. Publish: set [grey]published: true[/] and push to main.");
return 0;

static class Prompts
{
    /// <summary>An arrow-key menu in a real terminal; a numbered list where ANSI isn't available.</summary>
    public static T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> label) where T : notnull
    {
        if (AnsiConsole.Profile.Capabilities.Ansi)
            return AnsiConsole.Prompt(new SelectionPrompt<T>().Title(title).UseConverter(label).AddChoices(choices));

        AnsiConsole.MarkupLine(title);
        for (var i = 0; i < choices.Count; i++) AnsiConsole.MarkupLine($"  {i + 1}. {label(choices[i])}");
        var n = AnsiConsole.Prompt(new TextPrompt<int>("Choose a number:")
            .DefaultValue(1)
            .Validate(x => x >= 1 && x <= choices.Count ? ValidationResult.Success() : ValidationResult.Error($"1 to {choices.Count}")));
        return choices[n - 1];
    }

    /// <summary>A standalone post (series == null) or a part of <paramref name="series"/>.</summary>
    public static Created Post(Catalog catalog, string parentDir, SeriesInfo? series)
    {
        var isPart = series is not null;
        if (isPart) AnsiConsole.MarkupLine($"[grey]part of[/] [bold]{Markup.Escape(series!.Title)}[/]");

        var title = Ask(isPart ? "Part title:" : "Post title:");

        // Slugs: a post shares the top-level namespace with every post and series;
        // a part only needs to be unique within its series.
        var taken = isPart ? series!.Parts.Select(p => p.Slug).ToHashSet() : catalog.TopLevelSlugs;
        var slug = AskSlug(Slug.From(title), taken, isPart ? "in this series" : "by another post or series");
        var folder = AskFolder(parentDir, slug);

        int? part = null;
        if (isPart)
        {
            var next = series!.Parts.Select(p => p.Part ?? 0).DefaultIfEmpty(0).Max() + 1;
            var used = series.Parts.Where(p => p.Part is not null).Select(p => p.Part!.Value).ToHashSet();
            part = AnsiConsole.Prompt(new TextPrompt<int>("Part number:")
                .DefaultValue(next)
                .Validate(n => n < 1 ? ValidationResult.Error("must be 1 or more")
                    : used.Contains(n) ? ValidationResult.Error($"part {n} already exists")
                    : ValidationResult.Success()));
        }

        var date = AskDate();
        var tags = AskTags();
        var excerpt = AnsiConsole.Prompt(new TextPrompt<string>("Excerpt [grey](one line; empty = first paragraph)[/]:").AllowEmpty());
        var aiAssist = AnsiConsole.Confirm("Is AI helping with this one? [grey](shown on the post as ai-assisted or no ai; change it in .meta later)[/]", defaultValue: false);
        // Parts are published in reading order: if an earlier part is still a draft, this one must be too.
        var earlierDraft = isPart
            ? series!.Parts.Where(p => !p.Published && (p.Part is null || p.Part < part)).OrderBy(p => p.Part ?? int.MaxValue).FirstOrDefault()
            : null;
        bool published;
        if (earlierDraft is not null)
        {
            AnsiConsole.MarkupLine($"[yellow]{(earlierDraft.Part is { } n ? $"Part {n}" : "An earlier part")} ([bold]{Markup.Escape(earlierDraft.Folder)}[/]) is still a draft[/], "
                + "so this part starts as a draft too. Parts go live in order.");
            published = false;
        }
        else
        {
            published = AnsiConsole.Confirm("Publish it right away? [grey](no = draft, readable only by link)[/]", defaultValue: false);
        }

        var dir = Path.Combine(parentDir, folder);
        var meta = new MetaWriter()
            .Text("title", title)
            .Raw("date", date)
            .Raw("published", published ? "true" : "false")
            .RawIf(part is not null, "part", part?.ToString(CultureInfo.InvariantCulture))
            .ListIf(tags.Count > 0, "tags", tags)
            .TextIf(excerpt.Length > 0, "excerpt", excerpt)
            .Raw("ai-assist", aiAssist ? "true" : "false")
            .TextIf(slug != Slug.From(title), "slug", slug)
            .ToString();

        var heading = isPart ? $"part {part} of \"{series!.Title}\"" : "a new post";
        var url = isPart ? $"/writing/{series!.Slug}/{slug}" : $"/writing/{slug}";
        var index = $"""
            <!--
              {heading} (this comment isn't shown on the site; delete it whenever you like)

              Images:   put them in assets/ next to this file and write ![alt text](assets/<file>)
              Preview:  cd site && npm start   →   http://localhost:4200{url}
                        (images load from R2, so new ones appear after `sync` or after pushing)
              Check:    cd generator && dotnet run --project src/AlenAlex.Generator -- validate
              Publish:  set `published: true` in .meta and push to main{(isPart ? "\n  Series:   parts go live in order; a part can't be published while an earlier one is a draft" : "")}
            -->

            ## Introduction

            Start writing here.

            """;

        return new Created(slug, folder, dir,
        [
            new PlannedFile(Path.Combine(dir, ".meta"), meta),
            new PlannedFile(Path.Combine(dir, "index.md"), index),
            new PlannedFile(Path.Combine(dir, "assets", ".gitkeep"), ""),
        ]);
    }

    public static CreatedSeries Series(Catalog catalog, string blogsRoot)
    {
        var title = Ask("Series title:");
        var slug = AskSlug(Slug.From(title), catalog.TopLevelSlugs, "by another post or series");
        var folder = AskFolder(blogsRoot, slug);
        var date = AskDate("Start date");
        var description = AnsiConsole.Prompt(new TextPrompt<string>("Description [grey](one or two lines, optional)[/]:").AllowEmpty());
        var tags = AskTags();
        var published = AnsiConsole.Confirm("Publish the series right away? [grey](no = the whole series stays a draft)[/]", defaultValue: false);

        var dir = Path.Combine(blogsRoot, folder);
        var meta = new MetaWriter()
            .Text("title", title)
            .Raw("date", date)
            .Raw("published", published ? "true" : "false")
            .TextIf(description.Length > 0, "description", description)
            .ListIf(tags.Count > 0, "tags", tags)
            .TextIf(slug != Slug.From(title), "slug", slug)
            .ToString();

        return new CreatedSeries(title, slug, folder, dir,
        [
            new PlannedFile(Path.Combine(dir, "series.meta"), meta),
            new PlannedFile(Path.Combine(dir, "assets", ".gitkeep"), ""),
        ]);
    }

    static string Ask(string label) =>
        AnsiConsole.Prompt(new TextPrompt<string>(label)
            .Validate(s => string.IsNullOrWhiteSpace(s) ? ValidationResult.Error("can't be empty") : ValidationResult.Success()))
            .Trim();

    static string AskSlug(string suggested, IReadOnlySet<string> taken, string clashText) =>
        AnsiConsole.Prompt(new TextPrompt<string>("Slug [grey](the URL)[/]:")
            .DefaultValue(suggested)
            .Validate(s => !Slug.IsValid(s) ? ValidationResult.Error("lowercase letters, digits and single dashes, e.g. my-post")
                : taken.Contains(s) ? ValidationResult.Error($"\"{s}\" is already used {clashText}")
                : ValidationResult.Success()));

    static string AskFolder(string parentDir, string suggested) =>
        AnsiConsole.Prompt(new TextPrompt<string>("Folder [grey](permanent: images and history are keyed on it)[/]:")
            .DefaultValue(suggested)
            .Validate(f => !Regex.IsMatch(f, "^[A-Za-z0-9._-]{1,100}$") || f is "." or ".." or "assets"
                    ? ValidationResult.Error("letters, digits, '.', '_' and '-' only (and not 'assets')")
                : Directory.Exists(Path.Combine(parentDir, f)) ? ValidationResult.Error($"{f}/ already exists")
                : ValidationResult.Success()));

    static string AskDate(string label = "Date") =>
        AnsiConsole.Prompt(new TextPrompt<string>($"{label} [grey](YYYY-MM-DD)[/]:")
            .DefaultValue(DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Validate(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? ValidationResult.Success() : ValidationResult.Error("use YYYY-MM-DD")));

    static List<string> AskTags() =>
        AnsiConsole.Prompt(new TextPrompt<string>("Tags [grey](comma separated, optional)[/]:").AllowEmpty())
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();
}

sealed record PartInfo(string Slug, int? Part, bool Published, string Folder);
sealed record SeriesInfo(string Title, string Slug, string Folder, string Dir, List<PartInfo> Parts);

sealed class Catalog
{
    public required HashSet<string> TopLevelSlugs { get; init; }
    public required List<SeriesInfo> Series { get; init; }

    public static Catalog Read(string root)
    {
        var topLevel = new HashSet<string>(StringComparer.Ordinal);
        var series = new List<SeriesInfo>();

        foreach (var dir in Directory.EnumerateDirectories(root).Order())
        {
            if (Meta.TryRead(Path.Combine(dir, ".meta")) is { } post)
            {
                topLevel.Add(post.Slug);
            }
            else if (Meta.TryRead(Path.Combine(dir, "series.meta")) is { } s)
            {
                topLevel.Add(s.Slug);
                var parts = Directory.EnumerateDirectories(dir)
                    .Select(d => (Dir: d, Meta: Meta.TryRead(Path.Combine(d, ".meta"))))
                    .Where(x => x.Meta is not null)
                    .Select(x => new PartInfo(x.Meta!.Slug, x.Meta.Part, x.Meta.Published, Path.GetFileName(x.Dir)))
                    .ToList();
                series.Add(new SeriesInfo(s.Title, s.Slug, Path.GetFileName(dir), dir, parts));
            }
        }
        return new Catalog { TopLevelSlugs = topLevel, Series = series };
    }
}

/// <summary>The few .meta / series.meta fields this tool needs to read back.</summary>
sealed record Meta(string Title, string Slug, int? Part, bool Published)
{
    static readonly IDeserializer Yaml = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    public static Meta? TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var map = Yaml.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path)) ?? [];
            var title = map.GetValueOrDefault("title")?.ToString() ?? Path.GetFileName(Path.GetDirectoryName(path))!;
            var slug = map.GetValueOrDefault("slug")?.ToString() ?? global::Slug.From(title);
            int? part = int.TryParse(map.GetValueOrDefault("part")?.ToString(), out var n) ? n : null;
            var published = !string.Equals(map.GetValueOrDefault("published")?.ToString(), "false", StringComparison.OrdinalIgnoreCase);
            return new Meta(title, slug, part, published);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null; // broken metadata is the validator's job, not ours
        }
    }
}

sealed record PlannedFile(string Path, string Content);
sealed record Created(string Slug, string Folder, string Dir, List<PlannedFile> Files);
sealed record CreatedSeries(string Title, string Slug, string Folder, string Dir, List<PlannedFile> Files);

/// <summary>One `key: value` per line, strings always double-quoted.</summary>
sealed class MetaWriter
{
    readonly StringBuilder _sb = new();

    public MetaWriter Raw(string key, string value) { _sb.Append(key).Append(": ").Append(value).Append('\n'); return this; }
    public MetaWriter RawIf(bool when, string key, string? value) => when ? Raw(key, value!) : this;
    public MetaWriter Text(string key, string value) => Raw(key, Quote(value));
    public MetaWriter TextIf(bool when, string key, string value) => when ? Text(key, value) : this;
    public MetaWriter ListIf(bool when, string key, IEnumerable<string> values) =>
        when ? Raw(key, "[" + string.Join(", ", values.Select(Quote)) + "]") : this;

    static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public override string ToString() => _sb.ToString();
}

// Must match the slug rule in site/tools/build-content.mjs and the generator.
static class Slug
{
    public static string From(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var slug = new StringBuilder(normalized.Length);
        var pendingDash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingDash && slug.Length > 0) slug.Append('-');
                slug.Append(c);
                pendingDash = false;
            }
            else if (c is '_' or '-' || char.IsWhiteSpace(c))
            {
                pendingDash = true;
            }
        }
        return slug.ToString();
    }

    public static bool IsValid(string s) => Regex.IsMatch(s, "^[a-z0-9]+(-[a-z0-9]+)*$");
}

sealed record Blogs(string Root)
{
    /// <summary>--blogs, else the nearest blogs/ walking up from the working directory.</summary>
    public static Blogs? Locate(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Directory.Exists(explicitPath) ? new Blogs(Path.GetFullPath(explicitPath)) : null;

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "blogs");
            if (Directory.Exists(candidate)) return new Blogs(candidate);
        }
        return null;
    }
}

// Shown by --help, from the menu, and before each flow.
static class Help
{
    sealed record Topic(string Key, string Title, string Body);

    static readonly Topic[] Topics =
    [
        new("overview", "How it all fits together", """
            The repo is the CMS. Everything you write lives under [blue]blogs/[/] as plain files:
              [grey]•[/] the [bold]site build[/] turns them into pages at alenalex.me/writing
              [grey]•[/] the [bold]generator[/] checks them ([grey]validate[/]) and uploads images to R2 ([grey]sync[/])
              [grey]•[/] pushing to [bold]main[/] runs both in CI; nothing else to deploy by hand
            This tool only creates the files. Writing happens in [blue]index.md[/].
            """),
        new("post", "Standalone posts", """
            One folder, one post:
              [blue]blogs/<folder>/.meta[/]      title, date, tags, published …
              [blue]blogs/<folder>/index.md[/]   the post, in markdown
              [blue]blogs/<folder>/assets/[/]    its images
            It's served at [bold]/writing/<slug>[/] and gets the next catalog number ([grey]WR 004[/]) once published.
            """),
        new("series", "Series", """
            A series is a story told in parts. Its folder has [blue]series.meta[/] (instead of [blue].meta[/]) and one subfolder per part:
              [blue]blogs/<series>/series.meta[/]    title, date, description, tags
              [blue]blogs/<series>/assets/[/]        optional cover ([grey]cover_asset[/])
              [blue]blogs/<series>/<part>/…[/]       each part is a normal post folder
            The series page is [bold]/writing/<series-slug>[/]: its description plus a timeline of parts. It gets its own catalog number ([grey]SR 001[/]).
            """),
        new("part", "Parts of a series", """
            A part is a normal post folder inside a series folder, served at [bold]/writing/<series-slug>/<part-slug>[/].
              [grey]•[/] Order comes from [blue]part: 1[/], [blue]part: 2[/] … in its .meta. Parts without a number go after, by date.
              [grey]•[/] A part shows "part 2 of 3", the list of parts beside the text, and previous/next within the series.
              [grey]•[/] Its slug only has to be unique inside its series.
            """),
        new("drafts", "Drafts and publishing", """
            [blue]published: false[/] makes a draft. A draft is built and readable [bold]by its link[/] (handy for sharing before release), but it's never listed, never numbered, not in RSS, and hidden from search engines.
              [grey]•[/] New things start as drafts by default.
              [grey]•[/] A draft [bold]series[/] keeps every one of its parts a draft.
              [grey]•[/] Parts go live [bold]in order[/]: a part can't be published while an earlier part is a draft. The validator and the site build both refuse it.
            To publish: set [blue]published: true[/] and push to main.
            """),
        new("ai", "Saying whether AI helped", """
            [blue]ai-assist: true[/] puts an [bold]ai-assisted[/] mark next to the reading time, with a tooltip saying AI was used in some capacity to write the post.
            [blue]ai-assist: false[/] shows [bold]no ai[/] instead. Leave the key out and nothing is shown.
            """),
        new("names", "Slugs and folders", """
            The [bold]slug[/] is the URL. It defaults to the title in lowercase with dashes ("Why I rewrote it" → [grey]why-i-rewrote-it[/]); change it with [blue]slug:[/] in .meta.
            The [bold]folder[/] name is permanent: R2 image paths and the revision history are keyed on it, so renaming it later breaks old image links and starts the history over. Changing the title or slug later is fine.
            """),
        new("images", "Images", """
            Put images in the post's [blue]assets/[/] folder and reference them exactly as [blue]![[alt text]](assets/diagram.png)[/]. The site rewrites that to [grey]assets.alenalex.me/assets/hotlink-ok/<folder>/diagram.png[/], and the generator uploads it there.
            New images show up in a local preview only after they're uploaded ([grey]sync[/], or push and let CI do it).
            Use [blue]og_image_asset: cover.png[/] in .meta for the picture shown in link previews.
            """),
        new("revisions", "What the revisions list shows", """
            The [bold]revisions[/] button on a post lists the commits that changed its [blue]index.md[/]. Commits that only touch .meta or assets/ aren't listed.
              [grey]•[/] Put [blue][[skip rev]][/] anywhere in a commit message to leave that commit out (still on GitHub, just not listed).
              [grey]•[/] [blue]revisions-since: 2026-10-03[/] in .meta starts the list at that day, so the drafting before release isn't shown.
            """),
        new("workflow", "Writing, previewing, shipping", """
              [grey]1.[/] [bold]create[/]    dotnet run tools/new-post.cs
              [grey]2.[/] [bold]write[/]     edit index.md, add images to assets/
              [grey]3.[/] [bold]preview[/]   cd site && npm start   →  http://localhost:4200/writing/<path>
              [grey]4.[/] [bold]check[/]     cd generator && dotnet run --project src/AlenAlex.Generator -- validate
              [grey]5.[/] [bold]ship[/]      set published: true, commit, push to main
            Every commit to a post becomes a revision readers can browse on the post page.
            """),
    ];

    public static void Intro(string key)
    {
        var topic = Topics.Single(t => t.Key == key);
        AnsiConsole.Write(new Panel(new Markup(Dedent(topic.Body)))
            .Header($"[bold]{topic.Title}[/]")
            .BorderColor(Color.Grey)
            .Padding(1, 0));
        AnsiConsole.MarkupLine("[grey]More: pick \"How does this work?\" from the menu, or run with --help.[/]");
        AnsiConsole.WriteLine();
    }

    public static void Browse()
    {
        const string Back = "← back";
        while (true)
        {
            var choice = Prompts.Choose("Which part do you want explained?",
                [.. Topics.Select(t => t.Title), Back], t => t == Back ? "[grey]← back[/]" : t);
            if (choice == Back) break;
            var topic = Topics.Single(t => t.Title == choice);
            AnsiConsole.Write(new Panel(new Markup(Dedent(topic.Body))).Header($"[bold]{topic.Title}[/]").BorderColor(Color.Grey).Padding(1, 0));
            AnsiConsole.WriteLine();
        }
        AnsiConsole.WriteLine();
    }

    public static void PrintAll()
    {
        AnsiConsole.Write(new Rule("[bold]alenalex.me[/] · new writing · help").LeftJustified().RuleStyle("grey"));
        AnsiConsole.MarkupLine("""

            [bold]Usage[/]
              dotnet run tools/new-post.cs                   create a post, series or part
              dotnet run tools/new-post.cs -- --dry-run      show what would be created
              dotnet run tools/new-post.cs -- --blogs <dir>  use another blogs/ folder
              dotnet run tools/new-post.cs -- --help         this help

            """);
        foreach (var topic in Topics)
        {
            AnsiConsole.Write(new Panel(new Markup(Dedent(topic.Body))).Header($"[bold]{topic.Title}[/]").BorderColor(Color.Grey).Padding(1, 0));
        }
    }

    /// <summary>Raw string literals keep their relative indent; trim the trailing newline.</summary>
    static string Dedent(string body) => body.TrimEnd();
}
