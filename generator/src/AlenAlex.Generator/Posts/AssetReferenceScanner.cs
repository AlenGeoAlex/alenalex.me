using System.Text.RegularExpressions;

namespace AlenAlex.Generator.Posts;

/// <param name="RelativePath">Path inside <c>assets/</c>, '/'-separated and URL-decoded (e.g. <c>diagrams/flow.png</c>).</param>
/// <param name="Raw">The reference exactly as written (e.g. <c>./assets/diagrams/flow.png</c>).</param>
/// <param name="Line">1-based line in <c>index.md</c>.</param>
internal sealed record AssetReference(string RelativePath, string Raw, int Line);

/// <summary>
/// Finds <c>assets/x</c> and <c>./assets/x</c> in markdown images, inline links and reference definitions,
/// the same targets <c>assetUrl()</c> in build-content.mjs rewrites. Code blocks and spans are ignored.
/// </summary>
internal static partial class AssetReferenceScanner
{
    public static IReadOnlyList<AssetReference> Scan(string markdown)
    {
        var results = new List<AssetReference>();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        string? openFence = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var fence = FenceStart().Match(line);
            if (openFence is null && fence.Success)
            {
                openFence = fence.Groups["fence"].Value;
                continue;
            }

            if (openFence is not null)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith(openFence[0]) && trimmed.TrimEnd().All(c => c == openFence[0])
                    && trimmed.TrimEnd().Length >= openFence.Length)
                {
                    openFence = null;
                }

                continue;
            }

            var withoutCode = InlineCode().Replace(line, string.Empty);
            foreach (Match m in InlineTarget().Matches(withoutCode))
                Add(m.Groups["ref"].Value, i + 1);

            var def = ReferenceDefinition().Match(withoutCode);
            if (def.Success)
                Add(def.Groups["ref"].Value, i + 1);
        }

        return results;

        void Add(string raw, int lineNo)
        {
            var path = raw;
            if (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
            path = path["assets/".Length..];

            var cut = path.IndexOfAny(['?', '#']);
            if (cut >= 0) path = path[..cut];

            try
            {
                path = Uri.UnescapeDataString(path);
            }
            catch (UriFormatException)
            {
                // Keep the raw path; the existence check reports it.
            }

            results.Add(new AssetReference(path, raw, lineNo));
        }
    }

    /// <summary>Normalises an <c>og_image_asset</c> value the same way build-content.mjs does.</summary>
    public static string NormalizeOgImage(string value) => OgImagePrefix().Replace(value, string.Empty);

    // ```lang or ~~~ (up to 3 spaces of indentation).
    [GeneratedRegex(@"^ {0,3}(?<fence>`{3,}|~{3,})")]
    private static partial Regex FenceStart();

    [GeneratedRegex(@"(`+).+?\1")]
    private static partial Regex InlineCode();

    // ](assets/x), ](./assets/x), ](<./assets/x>): images and inline links
    [GeneratedRegex(@"\]\(\s*<?(?<ref>(?:\./)?assets/[^\s)>]+)")]
    private static partial Regex InlineTarget();

    // [label]: ./assets/x "optional title"
    [GeneratedRegex(@"^ {0,3}\[[^\]]+\]:\s*<?(?<ref>(?:\./)?assets/[^\s>]+)")]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(@"^(\./)?assets/")]
    private static partial Regex OgImagePrefix();
}
