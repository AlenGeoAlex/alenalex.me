using System.Globalization;
using System.Text.RegularExpressions;
using AlenAlex.Generator.Validation;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AlenAlex.Generator.Posts;

/// <summary>Diagnostics for one <c>.meta</c> or <c>series.meta</c>, plus the field rules both share.</summary>
internal sealed partial class MetaReadContext(string path, IReadOnlyDictionary<string, int> keyLines)
{
    public string Path { get; } = path;
    public IReadOnlyDictionary<string, int> KeyLines { get; } = keyLines;
    public List<Diagnostic> Diagnostics { get; } = [];

    public void Error(string message, string? key = null) =>
        Diagnostics.Add(Diagnostic.Error(Path, message, LineOf(key)));

    public void Warning(string message, string? key = null) =>
        Diagnostics.Add(Diagnostic.Warning(Path, message, LineOf(key)));

    public int? LineOf(string? key) => key is not null && KeyLines.TryGetValue(key, out var line) ? line : null;

    public static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public string? RequiredText(string? value, string key)
    {
        var text = NullIfEmpty(value);
        if (text is null) Error($"missing or empty required `{key}`", key);
        return text;
    }

    public DateOnly? RequiredDate(string? value, string key = "date")
    {
        if (NullIfEmpty(value) is not { } raw)
        {
            Error($"missing or empty required `{key}` (YYYY-MM-DD)", key);
            return null;
        }

        if (DatePattern().IsMatch(raw)
            && DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        Error($"`{key}` must be a valid YYYY-MM-DD date, got `{raw}`", key);
        return null;
    }

    /// <summary>Missing means published (build-content.mjs: <c>meta.published !== false</c>).</summary>
    public bool Published(string? value)
    {
        switch (value)
        {
            case null or "" or "true" or "True" or "TRUE": return true;
            case "false" or "False" or "FALSE": return false;
            default:
                Error($"`published` must be `true` or `false`, got `{value}`", "published");
                return true;
        }
    }

    /// <summary>A true/false flag that may be left out (<c>null</c>).</summary>
    public bool? OptionalBool(string? value, string key)
    {
        switch (value)
        {
            case null or "": return null;
            case "true" or "True" or "TRUE": return true;
            case "false" or "False" or "FALSE": return false;
            default:
                Error($"`{key}` must be `true` or `false`, got `{value}`", key);
                return null;
        }
    }

    public IReadOnlyList<string> Tags(List<string>? tags)
    {
        if (tags?.Any(string.IsNullOrWhiteSpace) == true) Error("`tags` must not contain empty values", "tags");
        return tags ?? [];
    }

    public string? Slug(string? value)
    {
        var slug = NullIfEmpty(value);
        if (slug is not null && !SlugPattern().IsMatch(slug))
            Error($"`slug` must be lowercase letters, digits and single dashes (e.g. my-post), got `{slug}`", "slug");
        return slug;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

internal static partial class MetaYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .IgnoreUnmatchedProperties() // unknown keys are reported as warnings instead
        .Build();

    /// <returns>The fields, or <c>null</c> when the YAML itself is invalid (the error is in <paramref name="context"/>).</returns>
    public static T? Read<T>(string text, string path, IReadOnlySet<string> knownKeys, out MetaReadContext context)
        where T : class, new()
    {
        context = new MetaReadContext(path, FindKeyLines(text));

        foreach (var (key, line) in context.KeyLines.Where(k => !knownKeys.Contains(k.Key)))
            context.Diagnostics.Add(Diagnostic.Warning(path, $"unknown key `{key}` (ignored by the site)", line));

        try
        {
            return Deserializer.Deserialize<T?>(text) ?? new T();
        }
        catch (YamlException ex)
        {
            var line = (int)ex.Start.Line;
            var lines = text.ReplaceLineEndings("\n").Split('\n');
            var match = line >= 1 && line <= lines.Length ? TopLevelKey().Match(lines[line - 1]) : null;
            var key = match is { Success: true } ? match.Groups["key"].Value : null;
            context.Diagnostics.Add(Diagnostic.Error(path, DescribeYamlError(ex, key), line > 0 ? line : null));
            return null;
        }
    }

    /// <summary>Turns YamlDotNet's messages into ones that say what to fix in a metadata file.</summary>
    private static string DescribeYamlError(YamlException ex, string? key)
    {
        var message = YamlLocationPrefix().Replace(ex.GetBaseException().Message, string.Empty).TrimEnd('.');
        return (key, message) switch
        {
            (_, _) when message.Contains("Duplicate key", StringComparison.OrdinalIgnoreCase) => $"duplicate key `{key}`",
            (not null, _) when message.Contains("Mapping values are not allowed", StringComparison.Ordinal)
                               || message.Contains("found invalid mapping", StringComparison.Ordinal) =>
                $"`{key}`: a value containing `: ` must be quoted, e.g. title: \"Part 1: Intro\"",
            ("tags", _) => "`tags` must be a list of strings, e.g. tags: [\"rust\", \"backend\"]",
            (not null, _) when message.Contains("No node deserializer", StringComparison.Ordinal) =>
                $"`{key}` must be a single value, not a list or mapping",
            (not null, _) => $"`{key}`: {message}",
            _ => $"invalid YAML: {message}",
        };
    }

    /// <summary>Line number of every top-level <c>key:</c> (first occurrence wins).</summary>
    private static Dictionary<string, int> FindKeyLines(string text)
    {
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var all = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < all.Length; i++)
        {
            var m = TopLevelKey().Match(all[i]);
            if (m.Success) lines.TryAdd(m.Groups["key"].Value, i + 1);
        }

        return lines;
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_-]+)\s*:(\s|$)")]
    private static partial Regex TopLevelKey();

    [GeneratedRegex(@"^\(Line: \d+, Col: \d+, Idx: \d+\) - \(Line: \d+, Col: \d+, Idx: \d+\): ")]
    private static partial Regex YamlLocationPrefix();
}
