using System.Text;

namespace AlenAlex.Generator.Posts;

/// <summary>
/// Mirrors <c>slugify</c> in <c>site/tools/build-content.mjs</c>, which decides the real URLs:
/// <code>s.toLowerCase().normalize('NFKD').replace(/[^\w\s-]/g, '').trim().replace(/[\s_-]+/g, '-')</code>
/// Only used to print URLs and catch duplicate slugs early.
/// </summary>
internal static class Slugifier
{
    public static string Slugify(string value)
    {
        // NFKD splits "é" into "e" + accent; the accent is dropped below.
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormKD);

        var slug = new StringBuilder(normalized.Length);
        var pendingDash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingDash && slug.Length > 0) slug.Append('-');
                slug.Append(c);
                pendingDash = false;
            }
            else if (c is '_' or '-' || char.IsWhiteSpace(c))
            {
                pendingDash = true;
            }
            // anything else (punctuation, emoji, accents, non-Latin letters) is dropped
        }

        return slug.ToString();
    }
}
