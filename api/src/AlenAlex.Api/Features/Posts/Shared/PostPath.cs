namespace AlenAlex.Api.Features.Posts.Shared;

/// <summary>
/// <c>{folder}</c> or <c>{series}/{part}</c> under <c>Github:BlogsPath</c>. Only created from
/// segments that pass <see cref="PostInput.IsValidName"/>, so it's safe to put into a GitHub URL.
/// </summary>
public readonly record struct PostPath
{
    private PostPath(string value) => Value = value;

    public string Value { get; }

    /// <summary>A standalone post, or a series folder itself (e.g. for its cover asset).</summary>
    public static bool TryCreate(string? folder, out PostPath path)
    {
        path = PostInput.IsValidName(folder) ? new PostPath(folder) : default;
        return path.Value is not null;
    }

    public static bool TryCreate(string? series, string? part, out PostPath path)
    {
        path = PostInput.IsValidName(series) && PostInput.IsValidName(part) ? new PostPath($"{series}/{part}") : default;
        return path.Value is not null;
    }

    public override string ToString() => Value;
}
