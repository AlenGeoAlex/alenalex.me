namespace AlenAlex.Generator.Cli;

/// <summary>
/// <c>--posts</c>, then <c>Posts:Directory</c> (both relative to the working directory), then the nearest
/// <c>blogs/</c> walking up from the working directory or the executable's directory.
/// </summary>
internal static class PostsDirectory
{
    private const string DefaultFolderName = "blogs";

    public static string? Resolve(string? fromOption, string? configured, string currentDirectory, string baseDirectory)
    {
        var explicitPath = !string.IsNullOrWhiteSpace(fromOption) ? fromOption : configured;
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath, currentDirectory);

        return FindUpwards(currentDirectory) ?? FindUpwards(baseDirectory);
    }

    private static string? FindUpwards(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, DefaultFolderName);
            if (Directory.Exists(candidate)) return candidate;
        }

        return null;
    }
}
