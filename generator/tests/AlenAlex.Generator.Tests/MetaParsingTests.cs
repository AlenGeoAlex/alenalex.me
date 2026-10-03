using AlenAlex.Generator.Posts;

namespace AlenAlex.Generator.Tests;

public class MetaParsingTests
{
    private static PostMetaResult Read(string text) => PostMetaReader.Read(text, "/blogs/post/.meta");

    private static string Errors(PostMetaResult result) =>
        string.Join(" | ", result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Line}: {d.Message}"));

    [Fact]
    public void Parses_the_real_test_blog_meta()
    {
        var result = Read(TempBlog.ValidMeta);

        Assert.False(result.HasErrors, Errors(result));
        var meta = result.Meta!;
        Assert.Equal("How did I use GitHub as CMS?", meta.Title);
        Assert.Equal(new DateOnly(2024, 10, 24), meta.Date);
        Assert.True(meta.Published);
        Assert.Equal("⚗️", meta.Icon);
        Assert.Equal(["security", "open-source", "backend"], meta.Tags);
        Assert.Equal("markdown", meta.Type);
        Assert.Null(meta.Slug);
        Assert.Equal("how-did-i-use-github-as-cms", meta.EffectiveSlug);
    }

    [Fact]
    public void Parses_all_optional_fields_quotes_and_comments()
    {
        var result = Read("""
            # a comment
            title: "Part 1: The \"quoted\" intro"
            page-title: 'It''s a page title'
            date: 2025-01-02 # trailing comment
            published: false
            excerpt: A short summary.
            og_image_asset: ./assets/cover.png
            slug: part-1
            tags: [rust, 'web dev']
            """);

        Assert.False(result.HasErrors, Errors(result));
        var meta = result.Meta!;
        Assert.Equal("Part 1: The \"quoted\" intro", meta.Title);
        Assert.Equal("It's a page title", meta.PageTitle);
        Assert.Equal(new DateOnly(2025, 1, 2), meta.Date);
        Assert.False(meta.Published);
        Assert.Equal("A short summary.", meta.Excerpt);
        Assert.Equal("./assets/cover.png", meta.OgImageAsset);
        Assert.Equal("part-1", meta.EffectiveSlug);
        Assert.Equal(["rust", "web dev"], meta.Tags);
    }

    [Fact]
    public void Supports_block_lists_and_crlf()
    {
        var result = Read("title: T\r\ndate: 2024-01-01\r\ntags:\r\n  - one\r\n  - \"two\"\r\n");

        Assert.False(result.HasErrors, Errors(result));
        Assert.Equal(["one", "two"], result.Meta!.Tags);
    }

    [Fact]
    public void Published_defaults_to_true_like_the_site()
    {
        var result = Read("title: T\ndate: 2024-01-01\n");
        Assert.True(result.Meta!.Published);
        Assert.Empty(result.Meta.Tags);
    }

    [Theory]
    [InlineData("date: 2024-01-01\n", "missing or empty required `title`")]
    [InlineData("title: \"\"\ndate: 2024-01-01\n", "missing or empty required `title`")]
    [InlineData("title: T\n", "missing or empty required `date`")]
    [InlineData("title: T\ndate: 24-10-2024\n", "`date` must be a valid YYYY-MM-DD date")]
    [InlineData("title: T\ndate: 2024-02-30\n", "`date` must be a valid YYYY-MM-DD date")]
    [InlineData("title: T\ndate: 2024-1-1\n", "`date` must be a valid YYYY-MM-DD date")]
    [InlineData("title: T\ndate: 2024-01-01\ntags: rust\n", "`tags` must be a list of strings")]
    [InlineData("title: T\ndate: 2024-01-01\ntags: [[\"a\"]]\n", "`tags` must be a list of strings")]
    [InlineData("title: T\ndate: 2024-01-01\npublished: yes\n", "`published` must be `true` or `false`")]
    [InlineData("title: Part 1: Intro\ndate: 2024-01-01\n", "must be quoted")]
    [InlineData("title: T\ntitle: U\ndate: 2024-01-01\n", "duplicate key `title`")]
    [InlineData("title: \"T\ndate: 2024-01-01\n", "double-quoted scalar")]
    [InlineData("title: T\ndate: 2024-01-01\nslug: Not A Slug\n", "`slug` must be lowercase")]
    [InlineData("title: T\ndate: 2024-01-01\ntype: pdf\n", "`type` must be `markdown` or `html`")]
    [InlineData("title: [a, b]\ndate: 2024-01-01\n", "`title` must be a single value")]
    [InlineData("title: T\n  date: 2024-01-01\n", "invalid YAML")]
    [InlineData("title: 日本語\ndate: 2024-01-01\n", "slugifies to an empty string")]
    public void Reports_errors(string text, string expected)
    {
        var result = Read(text);

        Assert.True(
            result.HasErrors && result.Diagnostics.Any(d => d.IsError && d.Message.Contains(expected, StringComparison.Ordinal)),
            $"expected an error containing '{expected}', got: {Errors(result)}");
    }

    [Fact]
    public void Errors_point_at_the_offending_line()
    {
        var result = Read("title: T\nicon: x\ndate: nope\n");

        var error = Assert.Single(result.Diagnostics, d => d.IsError);
        Assert.Equal(3, error.Line);
        Assert.Equal("/blogs/post/.meta", error.FilePath);
    }

    [Fact]
    public void A_syntax_error_is_not_also_reported_as_missing()
    {
        var result = Read("title: Part 1: Intro\ndate: 2024-01-01\n");

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(1, error.Line);
        Assert.Contains("must be quoted", error.Message);
    }

    [Fact]
    public void Unknown_keys_are_warnings_not_errors()
    {
        var result = Read("title: T\ndate: 2024-01-01\nauthor: me\n");

        Assert.False(result.HasErrors);
        var warning = Assert.Single(result.Diagnostics);
        Assert.False(warning.IsError);
        Assert.Contains("author", warning.Message);
    }

    [Fact]
    public void Hash_without_preceding_space_is_part_of_the_value()
    {
        var result = Read("title: C# tips\ndate: 2024-01-01\n");
        Assert.Equal("C# tips", result.Meta!.Title);
    }
}
