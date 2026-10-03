using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AlenAlex.Api.Features.Posts.Shared;

/// <summary>
/// Everything here ends up in a GitHub API path, so only a conservative character set is
/// allowed and path traversal isn't possible.
/// </summary>
public static partial class PostInput
{
    public const string DefaultRef = "main";

    /// <summary>Folder or file name: <c>^[A-Za-z0-9._-]{1,100}$</c>, and not <c>.</c> or <c>..</c>.</summary>
    public static bool IsValidName([NotNullWhen(true)] string? value) =>
        value is not null && value is not "." and not ".." && NamePattern().IsMatch(value);

    /// <summary>Branch, tag or sha: <c>^[A-Za-z0-9._/-]{1,100}$</c> without <c>..</c>.</summary>
    public static bool IsValidRef([NotNullWhen(true)] string? value) =>
        value is not null && RefPattern().IsMatch(value) && !value.Contains("..", StringComparison.Ordinal);

    public static bool IsCommitSha(string value) => ShaPattern().IsMatch(value);

    public static string RefOrDefault(string? gitRef) => string.IsNullOrEmpty(gitRef) ? DefaultRef : gitRef;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[A-Za-z0-9._/-]{1,100}$")]
    private static partial Regex RefPattern();

    [GeneratedRegex("^[0-9a-fA-F]{40}$")]
    private static partial Regex ShaPattern();
}
