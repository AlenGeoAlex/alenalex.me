namespace AlenAlex.Generator.Sync;

/// <summary>
/// <c>assets/hotlink-ok/&lt;folder&gt;/&lt;path&gt;</c> for posts and series-level files,
/// <c>assets/hotlink-ok/&lt;series&gt;/&lt;part&gt;/&lt;path&gt;</c> for parts.
/// <para>
/// Don't change the shape: <c>hotlink-ok</c> is exempt from Cloudflare hotlink protection, existing objects
/// live there, and <c>site/tools/build-content.mjs</c> rewrites <c>assets/x</c> to exactly this path.
/// </para>
/// </summary>
internal static class AssetKey
{
    public const string Prefix = "assets/hotlink-ok";

    public static string For(string folder, string relativePath) => Build([folder], relativePath);

    public static string ForPart(string seriesFolder, string partFolder, string relativePath) =>
        Build([seriesFolder, partFolder], relativePath);

    private static string Build(string[] folders, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        foreach (var folder in folders)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            if (folder.Contains('/') || folder.Contains('\\'))
                throw new ArgumentException($"folder must be a single path segment: {folder}", nameof(folders));
        }

        return $"{Prefix}/{string.Join('/', folders)}/{relativePath.Replace('\\', '/').TrimStart('/')}";
    }
}
