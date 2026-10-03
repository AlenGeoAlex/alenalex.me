using AlenAlex.Generator.Posts;

namespace AlenAlex.Generator.Tests;

/// <summary>Expected slugs are what the site's slugify (site/tools/build-content.mjs) produces.</summary>
public class SlugifierTests
{
    [Theory]
    [InlineData("How did I use GitHub as CMS?", "how-did-i-use-github-as-cms")]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("  Leading and trailing  ", "leading-and-trailing")]
    [InlineData("Crème brûlée à la carte", "creme-brulee-a-la-carte")]
    [InlineData("C# & .NET 10: NativeAOT", "c-net-10-nativeaot")]
    [InlineData("snake_case and kebab-case -- mixed", "snake-case-and-kebab-case-mixed")]
    [InlineData("Rust 🦀 + Axum", "rust-axum")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("", "")]
    public void Matches_the_site(string title, string expected) =>
        Assert.Equal(expected, Slugifier.Slugify(title));
}
