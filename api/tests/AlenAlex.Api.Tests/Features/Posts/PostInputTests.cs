using AlenAlex.Api.Features.Posts.GetAsset;
using AlenAlex.Api.Features.Posts.GetRevisions;
using AlenAlex.Api.Features.Posts.Shared;

namespace AlenAlex.Api.Tests.Features.Posts;

public sealed class PostInputTests
{
    [Theory]
    [InlineData("hello-world", true)]
    [InlineData("2026.10_post", true)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("a b", false)]
    [InlineData("%2e%2e", false)]
    public void Validates_names(string value, bool valid) => Assert.Equal(valid, PostInput.IsValidName(value));

    [Fact]
    public void Name_length_is_capped_at_100()
    {
        Assert.True(PostInput.IsValidName(new string('a', 100)));
        Assert.False(PostInput.IsValidName(new string('a', 101)));
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/new-post", true)]
    [InlineData("v1.2.3", true)]
    [InlineData("0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("a..b", false)]
    [InlineData("../main", false)]
    [InlineData("main?x=1", false)]
    [InlineData("", false)]
    public void Validates_refs(string value, bool valid) => Assert.Equal(valid, PostInput.IsValidRef(value));

    [Fact]
    public void Defaults_ref_to_main()
    {
        Assert.Equal("main", PostInput.RefOrDefault(null));
        Assert.Equal("main", PostInput.RefOrDefault(""));
        Assert.Equal("dev", PostInput.RefOrDefault("dev"));
    }

    [Theory]
    [InlineData("a.png", "image/png")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.avif", "image/avif")]
    [InlineData("a.html", null)]
    [InlineData("noext", null)]
    public void Maps_image_content_types(string file, string? expected) => Assert.Equal(expected, GetAssetHandler.ImageContentType(file));

    [Fact]
    public void Maps_commit_to_revision()
    {
        var revision = GetRevisionsHandler.ToRevision(new GithubCommit(
            "0123456789abcdef0123456789abcdef01234567",
            "https://github.com/o/r/commit/0123456",
            new GithubCommitDetail("Fix typo\r\n\nLonger body", new GithubCommitSignature(new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero)), null)));

        Assert.Equal(new PostRevision("0123456789abcdef0123456789abcdef01234567", "0123456", "2026-09-30", "Fix typo",
            "https://github.com/o/r/commit/0123456"), revision);
    }
}
