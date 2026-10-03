using AlenAlex.Generator.Posts;
using AlenAlex.Generator.Validation;

namespace AlenAlex.Generator.Tests;

public class AssetReferenceScannerTests
{
    [Fact]
    public void Finds_images_links_and_reference_definitions()
    {
        const string markdown = """
            # Title
            ![A rust logo](./assets/rustlogo.png)
            ![no dot](assets/diagram.svg "with title")
            Download [the PDF](assets/docs/paper.pdf) or see [site](https://example.com/assets/x.png).

            [ref]: ./assets/ref-image.webp
            ![spaced](assets/my%20file.png)
            ![angle](<./assets/angle.png>)
            """;

        var refs = AssetReferenceScanner.Scan(markdown);

        Assert.Equal(
            ["rustlogo.png", "diagram.svg", "docs/paper.pdf", "ref-image.webp", "my file.png", "angle.png"],
            refs.Select(r => r.RelativePath));
        Assert.Equal(2, refs[0].Line);
        Assert.Equal("./assets/rustlogo.png", refs[0].Raw);
    }

    [Fact]
    public void Ignores_code_blocks_and_inline_code()
    {
        const string markdown = """
            Use `![x](./assets/inline.png)` in your post.

            ```markdown
            ![x](./assets/fenced.png)
            ```

            ~~~
            ![x](assets/tilde.png)
            ~~~

            ![real](assets/real.png)
            """;

        var refs = AssetReferenceScanner.Scan(markdown);

        var only = Assert.Single(refs);
        Assert.Equal("real.png", only.RelativePath);
        Assert.Equal(11, only.Line);
    }

    [Fact]
    public void Strips_query_and_fragment()
    {
        var refs = AssetReferenceScanner.Scan("![x](assets/a.png?v=2) [y](./assets/b.pdf#page=3)");
        Assert.Equal(["a.png", "b.pdf"], refs.Select(r => r.RelativePath));
    }

    [Theory]
    [InlineData("cover.png", "cover.png")]
    [InlineData("assets/cover.png", "cover.png")]
    [InlineData("./assets/cover.png", "cover.png")]
    public void Normalizes_og_image_like_build_content(string value, string expected) =>
        Assert.Equal(expected, AssetReferenceScanner.NormalizeOgImage(value));
}

public class PostValidatorTests
{
    [Fact]
    public void Valid_post_passes()
    {
        using var blog = new TempBlog();
        blog.AddPost("test-blog", markdown: "![logo](./assets/rustlogo.png)\n", assets: "rustlogo.png");

        var report = PostValidator.ValidateAll(blog.Root);

        var post = Assert.Single(report.Posts);
        Assert.False(report.HasErrors, string.Join("\n", post.Diagnostics));
        Assert.Empty(post.Diagnostics);
        Assert.Equal("how-did-i-use-github-as-cms", post.Slug);
        Assert.Equal(["rustlogo.png"], post.Assets);
    }

    [Fact]
    public void Missing_asset_is_an_error_pointing_at_index_md_line()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", markdown: "intro\n\n![x](./assets/missing.png)\n");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        var error = Assert.Single(post.Diagnostics, d => d.IsError);
        Assert.EndsWith("index.md", error.FilePath);
        Assert.Equal(3, error.Line);
        Assert.Contains("assets/missing.png does not exist", error.Message);
    }

    [Fact]
    public void Case_mismatch_is_reported_even_on_case_insensitive_file_systems()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", markdown: "![x](assets/Logo.png)\n", assets: "logo.png");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.Contains(post.Diagnostics, d => d.IsError && d.Message.Contains("case differs"));
    }

    [Fact]
    public void Path_traversal_is_rejected()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", markdown: "![x](assets/../index.md)\n");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.Contains(post.Diagnostics, d => d.IsError && d.Message.Contains("must stay inside assets/"));
    }

    [Fact]
    public void Missing_og_image_is_an_error_on_the_meta_line()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", meta: "title: T\ndate: 2024-01-01\nog_image_asset: cover.png\n");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        var error = Assert.Single(post.Diagnostics, d => d.IsError);
        Assert.EndsWith(".meta", error.FilePath);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void Missing_og_image_is_reported_even_when_other_fields_are_broken()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", meta: "date: nope\nog_image_asset: cover.png\n");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.Contains(post.Diagnostics, d => d.Message.Contains("og_image_asset: cover.png"));
    }

    [Fact]
    public void Og_image_counts_as_a_reference()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", meta: "title: T\ndate: 2024-01-01\nog_image_asset: assets/cover.png\n", assets: "cover.png");

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.Empty(post.Diagnostics);
    }

    [Fact]
    public void Unreferenced_assets_are_warnings_and_hidden_files_are_ignored()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", assets: ["unused.png", ".DS_Store", "nested/.hidden"]);

        var report = PostValidator.ValidateAll(blog.Root);
        var post = Assert.Single(report.Posts);

        Assert.False(report.HasErrors);
        Assert.Equal(["unused.png"], post.Assets);
        var warning = Assert.Single(post.Diagnostics);
        Assert.Equal(Severity.Warning, warning.Severity);
    }

    [Fact]
    public void Folders_without_meta_are_not_ready_and_skipped_silently()
    {
        using var blog = new TempBlog();
        blog.AddPost("work-in-progress", meta: null, markdown: "![x](assets/nope.png)");
        blog.AddPost("ready");

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.False(report.HasErrors);
        Assert.Equal("ready", Assert.Single(report.Posts).Folder.Name);
        Assert.Equal("work-in-progress", Assert.Single(report.NotReady).Name);
    }

    [Fact]
    public void Meta_without_index_md_is_an_error()
    {
        using var blog = new TempBlog();
        blog.AddPost("p", markdown: null);

        var post = Assert.Single(PostValidator.ValidateAll(blog.Root).Posts);

        Assert.Contains(post.Diagnostics, d => d.IsError && d.Message.Contains("missing index.md"));
    }

    [Fact]
    public void Duplicate_slugs_among_published_posts_are_errors()
    {
        using var blog = new TempBlog();
        blog.AddPost("a");
        blog.AddPost("b");

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.True(report.HasErrors);
        Assert.All(report.Posts, p => Assert.Contains(p.Diagnostics, d => d.Message.Contains("duplicate slug `how-did-i-use-github-as-cms`")));
    }

    [Fact]
    public void Explicit_slug_resolves_a_duplicate_and_drafts_do_not_count()
    {
        using var blog = new TempBlog();
        blog.AddPost("a");
        blog.AddPost("b", meta: TempBlog.ValidMeta + "\nslug: second-take\n");
        blog.AddPost("c", meta: TempBlog.ValidMeta.Replace("published: true", "published: false"));

        var report = PostValidator.ValidateAll(blog.Root);

        Assert.False(report.HasErrors, string.Join("\n", report.Posts.SelectMany(p => p.Diagnostics)));
        Assert.True(report.Posts.Single(p => p.Folder.Name == "c").IsDraft);
    }
}
