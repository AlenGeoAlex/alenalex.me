using System.Text.RegularExpressions;

namespace AlenAlex.Generator.Sync;

/// <summary>
/// Post assets keep human names and can be replaced in place, so they get a one-day max-age. Only names
/// with a content hash (<c>diagram.3fa9b2c1.png</c>, <c>diagram-3fa9b2c1.png</c>) are marked immutable.
/// </summary>
internal static partial class CachePolicy
{
    public const string Mutable = "public, max-age=86400";
    public const string Immutable = "public, max-age=31536000, immutable";

    public static string For(string fileName) =>
        ContentAddressedName().IsMatch(Path.GetFileName(fileName)) ? Immutable : Mutable;

    // name.<8+ hex>.ext or name-<8+ hex>.ext; the hash needs a digit so words like "deadbeef" don't count.
    [GeneratedRegex(@"[.-](?=[0-9a-f]*[0-9])[0-9a-f]{8,}\.[A-Za-z0-9]+$")]
    private static partial Regex ContentAddressedName();
}
