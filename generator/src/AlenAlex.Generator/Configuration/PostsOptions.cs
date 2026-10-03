namespace AlenAlex.Generator.Configuration;

internal sealed class PostsOptions
{
    public const string SectionName = "Posts";

    /// <summary>Relative to the working directory; empty means find the nearest blogs/.</summary>
    public string? Directory { get; set; }

    /// <summary>Comma-separated folder names; <c>*</c> or empty means all. CI sets it from the push diff.</summary>
    public string? Changed { get; set; }
}
