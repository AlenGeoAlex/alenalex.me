using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AlenAlex.Api.Options;

public sealed class GithubOptions
{
    public const string SectionName = "Github";

    /// <summary>
    /// Needed for the GraphQL profile summary and for reading post history (Contents: read).
    /// Without it those endpoints return 503.
    /// </summary>
    public string? Token { get; set; }

    [Required]
    public string Login { get; set; } = "AlenGeoAlex";

    /// <summary><c>owner/name</c> of the repo holding the blog posts.</summary>
    [Required]
    public string Repo { get; set; } = "AlenGeoAlex/alenalex.me";

    /// <summary>Folder in <see cref="Repo"/> that contains one sub-folder per post.</summary>
    [Required]
    public string BlogsPath { get; set; } = "blogs";

    [Required, Url]
    public string ApiUrl { get; set; } = "https://api.github.com/";

    [Required, Url]
    public string GraphQlUrl { get; set; } = "https://api.github.com/graphql";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}

[OptionsValidator]
public sealed partial class GithubOptionsValidator : IValidateOptions<GithubOptions>;
