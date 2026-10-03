using AlenAlex.Generator.Cli;
using AlenAlex.Generator.Posts;
using AlenAlex.Generator.Sync;
using AlenAlex.Generator.Validation;

namespace AlenAlex.Generator.Tests;

public class SeriesMetaTests
{
    private static SeriesMetaResult Read(string text) => SeriesMetaReader.Read(text, "/blogs/s/series.meta");

    [Fact]
    public void Parses_all_fields()
    {
        var result = Read("""
            title: Guestbook rewrite
            date: 2025-01-10
            published: false
            description: Rebuilding the guestbook.
            tags: ["rust", "axum"]
            slug: guestbook
            cover_asset: assets/cover.png
            """);

        Assert.Empty(result.Diagnostics);
        var meta = result.Meta!;
        Assert.Equal("Guestbook rewrite", meta.Title);
        Assert.Equal(new DateOnly(2025, 1, 10), meta.Date);
        Assert.False(meta.Published);
        Assert.Equal("Rebuilding the guestbook.", meta.Description);
        Assert.Equal(["rust", "axum"], meta.Tags);
        Assert.Equal("guestbook", meta.EffectiveSlug);
        Assert.Equal("assets/cover.png", meta.CoverAsset);
    }

    [Fact]
    public void Defaults_published_and_derives_slug_from_title()
    {
        var meta = Read("title: Guestbook rewrite\ndate: 2025-01-10\n").Meta!;

        Assert.True(meta.Published);
        Assert.Equal("guestbook-rewrite", meta.EffectiveSlug);
    }

    [Theory]
    [InlineData("date: 2025-01-10\n", "missing or empty required `title`", 0)]
    [InlineData("title: T\n", "missing or empty required `date`", 0)]
    [InlineData("title: T\ndate: 2025-13-01\n", "`date` must be a valid YYYY-MM-DD date", 2)]
    [InlineData("title: T\ndate: 2025-01-10\ntags: rust\n", "`tags` must be a list of strings", 3)]
    [InlineData("title: T\ndate: 2025-01-10\nslug: Bad Slug\n", "`slug` must be lowercase", 3)]
    [InlineData("title: Part 1: Intro\ndate: 2025-01-10\n", "must be quoted", 1)]
    public void Reports_errors_with_lines(string text, string expected, int line)
    {
        var error = Assert.Single(Read(text).Diagnostics, d => d.IsError);

        Assert.Contains(expected, error.Message);
        Assert.Equal(line == 0 ? null : line, error.Line);
    }

    [Fact]
    public void Unknown_keys_are_warnings()
    {
        var warning = Assert.Single(Read("title: T\ndate: 2025-01-10\nauthor: me\n").Diagnostics);

        Assert.False(warning.IsError);
        Assert.Equal(3, warning.Line);
    }
}

public class SeriesValidationTests
{
    private static SeriesReport SingleSeries(ValidationReport report) => Assert.Single(report.Series);

    private static string Describe(ValidationReport report) =>
        string.Join("\n", report.Posts.SelectMany(p => p.Diagnostics)
            .Concat(report.Series.SelectMany(s => s.Diagnostics.Concat(s.Parts.SelectMany(p => p.Diagnostics)))));

    [Fact]
    public void Valid_series_with_parts_and_urls()
    {
        using var blog = new TempBlog();
        blog.AddSeries("guestbook-rewrite", TempBlog.ValidSeriesMeta + "\ncover_asset: cover.png\n", "cover.png");
        blog.AddPart("guestbook-rewrite", "01-intro", TempBlog.PartMeta("Intro", 1), "![x](assets/a.png)\n", "a.png");
        blog.AddPart("guestbook-rewrite", "02-db", TempBlog.PartMeta("Database", 2));
        blog.AddPart("guestbook-rewrite", "wip", meta: null); // not ready

        var report = PostValidator.ValidateAll(blog.Root);
        var series = SingleSeries(report);

        Assert.False(report.HasErrors, Describe(report));
        Assert.Empty(Describe(report));
        Assert.Equal("/writing/guestbook-rewrite", series.Url);
        Assert.Equal(["cover.png"], series.Assets);
        Assert.Equal(["/writing/guestbook-rewrite/intro", "/writing/guestbook-rewrite/database"], series.Parts.Select(p => p.Url));
        Assert.Equal("wip", Assert.Single(report.NotReady).Name);
        Assert.Empty(report.Posts);
    }

    [Fact]
    public void Numbered_parts_are_ordered_by_part_number()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("Third", 3, date: "2025-01-01"));
        blog.AddPart("s", "b", TempBlog.PartMeta("First", 1, date: "2025-03-01"));
        blog.AddPart("s", "c", TempBlog.PartMeta("Second", 2, date: "2025-02-01"));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.Equal(["first", "second", "third"], series.Parts.Select(p => p.Slug));
        Assert.Equal([1, 2, 3], series.Parts.Select(p => p.PartNumber));
    }

    [Fact]
    public void Unnumbered_parts_are_ordered_by_date_then_slug()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "x", TempBlog.PartMeta("Zeta", date: "2025-01-01"));
        blog.AddPart("s", "y", TempBlog.PartMeta("Beta", date: "2025-02-01"));
        blog.AddPart("s", "z", TempBlog.PartMeta("Alpha", date: "2025-02-01"));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.Equal(["zeta", "alpha", "beta"], SingleSeries(report).Parts.Select(p => p.Slug));
        Assert.Empty(Describe(report));
    }

    [Fact]
    public void Mixed_numbering_puts_unnumbered_last_and_warns()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "extra", TempBlog.PartMeta("Appendix", date: "2024-01-01"));
        blog.AddPart("s", "two", TempBlog.PartMeta("Two", 2));
        blog.AddPart("s", "one", TempBlog.PartMeta("One", 1));

        var report = PostValidator.ValidateAll(blog.Root);
        var series = SingleSeries(report);

        Assert.False(report.HasErrors);
        Assert.Equal(["one", "two", "appendix"], series.Parts.Select(p => p.Slug));
        var warning = Assert.Single(series.Diagnostics);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("some parts have `part:` and some do not (extra)", warning.Message);
    }

    [Fact]
    public void Duplicate_part_numbers_are_errors_on_each_part()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 1));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.True(report.HasErrors);
        Assert.All(SingleSeries(report).Parts, p =>
        {
            var error = Assert.Single(p.Diagnostics, d => d.IsError);
            Assert.Contains("duplicate `part: 1`", error.Message);
            Assert.Equal(4, error.Line); // the `part:` line in .meta
        });
    }

    [Fact]
    public void Gaps_in_part_numbers_are_a_warning()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2));
        blog.AddPart("s", "d", TempBlog.PartMeta("D", 4));

        var report = PostValidator.ValidateAll(blog.Root);
        var warning = Assert.Single(SingleSeries(report).Diagnostics);

        Assert.False(report.HasErrors);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("gaps: no part 3", warning.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("two")]
    public void Part_must_be_a_positive_integer(string value)
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", $"title: A\ndate: 2025-01-01\npart: {value}\n");

        var part = Assert.Single(SingleSeries(PostValidator.ValidateAll(blog.Root)).Parts);

        Assert.Contains(part.Diagnostics, d => d.IsError && d.Message.Contains("`part` must be a positive whole number") && d.Line == 3);
    }

    [Fact]
    public void Series_slug_colliding_with_a_post_slug_is_an_error_on_both()
    {
        using var blog = new TempBlog();
        blog.AddPost("my-post", "title: Guestbook rewrite\ndate: 2024-01-01\n");
        blog.AddSeries("series-folder");
        blog.AddPart("series-folder", "p", TempBlog.PartMeta("Intro", 1));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.Contains(Assert.Single(report.Posts).Diagnostics,
            d => d.IsError && d.Message.Contains("duplicate slug `guestbook-rewrite`") && d.Message.Contains("series series-folder"));
        Assert.Contains(SingleSeries(report).Diagnostics,
            d => d.IsError && d.Message.Contains("duplicate slug `guestbook-rewrite`") && d.Message.Contains("my-post"));
    }

    [Fact]
    public void Two_series_with_the_same_slug_collide()
    {
        using var blog = new TempBlog();
        blog.AddSeries("a");
        blog.AddSeries("b");

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.All(report.Series, s => Assert.Contains(s.Diagnostics, d => d.IsError && d.Message.Contains("duplicate slug")));
    }

    [Fact]
    public void Part_slugs_must_be_unique_within_a_series()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("Intro", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("Intro", 2));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.All(SingleSeries(report).Parts, p =>
            Assert.Contains(p.Diagnostics, d => d.IsError && d.Message.Contains("duplicate slug `intro` in this series")));
    }

    [Fact]
    public void Same_part_slug_in_two_different_series_is_fine()
    {
        using var blog = new TempBlog();
        blog.AddSeries("one", "title: Series One\ndate: 2025-01-01\n");
        blog.AddSeries("two", "title: Series Two\ndate: 2025-01-01\n");
        blog.AddPart("one", "intro", TempBlog.PartMeta("Intro", 1));
        blog.AddPart("two", "intro", TempBlog.PartMeta("Intro", 1));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.False(report.HasErrors, Describe(report));
        Assert.Equal(["/writing/series-one/intro", "/writing/series-two/intro"],
            report.Series.SelectMany(s => s.Parts).Select(p => p.Url));
    }

    [Fact]
    public void Part_slug_may_equal_a_top_level_post_slug()
    {
        using var blog = new TempBlog();
        blog.AddPost("standalone", "title: Intro\ndate: 2025-01-01\n");
        blog.AddSeries("s");
        blog.AddPart("s", "p", TempBlog.PartMeta("Intro", 1));

        Assert.False(PostValidator.ValidateAll(blog.Root).HasErrors);
    }

    [Fact]
    public void Unpublished_series_makes_every_part_a_draft()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s", "title: S\ndate: 2025-01-01\npublished: false\n");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.True(series.IsDraft);
        Assert.All(series.Parts, p => Assert.True(p.IsDraft));
    }

    [Fact]
    public void Unpublished_part_is_a_draft_part_only()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2, published: false));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.False(series.IsDraft);
        Assert.Equal([false, true], series.Parts.Select(p => p.IsDraft));
    }

    [Fact]
    public void Publishing_a_part_after_a_draft_part_is_an_error()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1, published: false));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.True(series.HasErrors);
        var error = Assert.Single(series.Parts.Single(p => p.Folder.Name == "b").Diagnostics, d => d.IsError);
        Assert.Contains("part 1 (`a`) is still a draft", error.Message);
        Assert.DoesNotContain(series.Parts.Single(p => p.Folder.Name == "a").Diagnostics, d => d.IsError);
    }

    [Fact]
    public void Later_drafts_after_published_parts_are_fine()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2, published: false));
        blog.AddPart("s", "c", TempBlog.PartMeta("C", 3, published: false));

        Assert.False(SingleSeries(PostValidator.ValidateAll(blog.Root)).HasErrors);
    }

    [Fact]
    public void Publish_order_follows_dates_for_unnumbered_parts()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "early", TempBlog.PartMeta("Early", date: "2025-01-01", published: false));
        blog.AddPart("s", "late", TempBlog.PartMeta("Late", date: "2025-02-01"));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.Contains(series.Parts.Single(p => p.Folder.Name == "late").Diagnostics,
            d => d.IsError && d.Message.Contains("an earlier part (`early`) is still a draft"));
    }

    [Fact]
    public void Draft_series_skips_the_publish_order_rule()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s", TempBlog.ValidSeriesMeta + "\npublished: false\n");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1, published: false));
        blog.AddPart("s", "b", TempBlog.PartMeta("B", 2));

        Assert.False(SingleSeries(PostValidator.ValidateAll(blog.Root)).HasErrors);
    }

    [Fact]
    public void Unpublished_series_does_not_count_for_top_level_slug_collisions()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", "title: Guestbook rewrite\ndate: 2024-01-01\n");
        blog.AddSeries("s", TempBlog.ValidSeriesMeta + "\npublished: false\n");

        Assert.False(PostValidator.ValidateAll(blog.Root).HasErrors);
    }

    [Fact]
    public void Folder_with_both_meta_files_is_an_error()
    {
        using var blog = new TempBlog();
        blog.AddPost("both");
        File.WriteAllText(Path.Combine(blog.Root, "both", SeriesFolder.MetaFileName), TempBlog.ValidSeriesMeta);

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.True(report.HasErrors);
        Assert.Empty(report.Series);
        Assert.Contains(Assert.Single(report.Posts).Diagnostics, d => d.IsError && d.Message.Contains("both .meta and series.meta"));
    }

    [Fact]
    public void Missing_cover_asset_is_an_error_on_its_line()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s", "title: S\ndate: 2025-01-01\ncover_asset: cover.png\n");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1));

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        var error = Assert.Single(series.Diagnostics, d => d.IsError);
        Assert.EndsWith("series.meta", error.FilePath);
        Assert.Equal(3, error.Line);
        Assert.Contains("assets/cover.png does not exist", error.Message);
    }

    [Fact]
    public void Cover_asset_in_a_part_folder_does_not_count()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s", "title: S\ndate: 2025-01-01\ncover_asset: cover.png\n");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1), "![c](assets/cover.png)\n", "cover.png");

        Assert.True(SingleSeries(PostValidator.ValidateAll(blog.Root)).HasErrors);
    }

    [Fact]
    public void Series_without_ready_parts_warns()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");

        var series = SingleSeries(PostValidator.ValidateAll(blog.Root));

        Assert.False(series.HasErrors);
        Assert.Contains(series.Diagnostics, d => d.Message.Contains("no ready parts"));
    }

    [Fact]
    public void Errors_in_a_part_fail_the_series()
    {
        using var blog = new TempBlog();
        blog.AddSeries("s");
        blog.AddPart("s", "a", TempBlog.PartMeta("A", 1), "![x](assets/missing.png)\n");

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.True(report.HasErrors);
        Assert.True(SingleSeries(report).HasErrors);
        Assert.Equal(1, report.ErrorCount);
    }

    [Fact]
    public void Part_number_on_a_standalone_post_is_a_warning()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", TempBlog.ValidMeta + "\npart: 1\n");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.False(post.HasErrors);
        Assert.Contains(post.Diagnostics, d => !d.IsError && d.Message.Contains("only used for posts inside a series"));
    }
}

public class SeriesSyncTests
{
    [Fact]
    public void Asset_keys_for_series_and_parts()
    {
        Assert.Equal("assets/hotlink-ok/guestbook/cover.png", AssetKey.For("guestbook", "cover.png"));
        Assert.Equal("assets/hotlink-ok/guestbook/01-intro/diagram.png", AssetKey.ForPart("guestbook", "01-intro", "diagram.png"));
        Assert.Equal("assets/hotlink-ok/guestbook/01-intro/img/a.png", AssetKey.ForPart("guestbook", "01-intro", "img/a.png"));
        Assert.Throws<ArgumentException>(() => AssetKey.ForPart("guestbook", "a/b", "x.png"));
    }

    [Fact]
    public void Folders_map_to_their_key_shapes()
    {
        Assert.Equal("assets/hotlink-ok/post/x.png", new PostFolder("post", "/b/post").KeyFor("x.png"));
        Assert.Equal("assets/hotlink-ok/s/x.png", new SeriesFolder("s", "/b/s").KeyFor("x.png"));
        Assert.Equal("assets/hotlink-ok/s/p/x.png", new PostFolder("p", "/b/s/p", "s").KeyFor("x.png"));
    }

    [Fact]
    public async Task Offline_sync_of_series_and_part_assets_uses_the_nested_keys()
    {
        using var blog = new TempBlog();
        var series = blog.AddSeries("s", TempBlog.ValidSeriesMeta, "cover.png");
        var part = blog.AddPart("s", "p1", TempBlog.PartMeta("A", 1), "![x](assets/a.png)\n", "a.png");
        var syncer = new AssetSyncService(null, SyncMode.Offline);

        var seriesResults = await syncer.SyncAssetsAsync(series, series.ListAssets(), CancellationToken.None);
        var partResults = await syncer.SyncAssetsAsync(part, part.ListAssets(), CancellationToken.None);

        Assert.Equal("assets/hotlink-ok/s/cover.png", Assert.Single(seriesResults).Asset.Key);
        Assert.Equal("assets/hotlink-ok/s/p1/a.png", Assert.Single(partResults).Asset.Key);
    }

    [Fact]
    public void Changed_dirs_inside_a_series_select_the_whole_series()
    {
        using var blog = new TempBlog();
        blog.AddPost("post");
        blog.AddSeries("s");
        blog.AddPart("s", "p1", TempBlog.PartMeta("A", 1));

        var report = PostValidator.ValidateAll(blog.Root);
        var changed = ChangedDirs.Parse("blogs/s/p1/index.md");
        var selected = SyncCommand.SelectFolders(new SyncSettings(blog.Root, changed, DryRun: true, Offline: true), report);

        Assert.Equal(["s"], selected);
    }

    [Fact]
    public void All_selects_posts_and_series()
    {
        using var blog = new TempBlog();
        blog.AddPost("post");
        blog.AddSeries("s");

        var report = PostValidator.ValidateAll(blog.Root);
        var selected = SyncCommand.SelectFolders(new SyncSettings(blog.Root, ChangedDirs.All, DryRun: true, Offline: true), report);

        Assert.Equal(["post", "s"], selected);
    }
}
