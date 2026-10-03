using AlenAlex.Generator.Validation;

namespace AlenAlex.Generator.Output;

internal enum Tone
{
    Plain,
    Success,
    Muted,
    Warning,
    Error,
    Info,
}

/// <summary>
/// Aligned status labels, ANSI colour (off when redirected or <c>NO_COLOR</c> is set, on in GitHub Actions)
/// and compiler-style diagnostics. In GitHub Actions diagnostics are also written as <c>::error file=...</c>
/// commands so they show up as annotations.
/// </summary>
internal sealed class ConsoleOutput(TextWriter writer, bool useColor, string? gitHubWorkspace, string currentDirectory)
{
    public const int LabelWidth = 14;

    public static ConsoleOutput CreateDefault()
    {
        var inGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        var noColor = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        var color = !noColor && (inGitHubActions || !Console.IsOutputRedirected);
        var workspace = inGitHubActions ? Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") : null;
        return new ConsoleOutput(Console.Out, color, workspace, Directory.GetCurrentDirectory());
    }

    public void Line(string text = "") => writer.WriteLine(text);

    public string Label(string label, Tone tone) => Paint(label.PadRight(LabelWidth), tone);

    public string Paint(string text, Tone tone)
    {
        if (!useColor || tone == Tone.Plain) return text;
        var code = tone switch
        {
            Tone.Success => "32",
            Tone.Muted => "90",
            Tone.Warning => "33",
            Tone.Error => "31",
            Tone.Info => "36",
            _ => "0",
        };
        return $"\e[{code}m{text}\e[0m";
    }

    /// <summary>Relative to the working directory, unless that would climb more than two levels.</summary>
    public string DisplayPath(string fullPath)
    {
        var relative = Path.GetRelativePath(currentDirectory, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        return relative.StartsWith("../../../", StringComparison.Ordinal) ? fullPath : relative;
    }

    public void Diagnostic(Diagnostic diagnostic, string indent)
    {
        var location = DisplayPath(diagnostic.FilePath) + (diagnostic.Line is { } line ? $":{line}" : string.Empty);
        var severity = diagnostic.IsError ? Paint("error", Tone.Error) : Paint("warning", Tone.Warning);
        writer.WriteLine($"{indent}{location}: {severity}: {diagnostic.Message}");

        if (gitHubWorkspace is not null)
        {
            var kind = diagnostic.IsError ? "error" : "warning";
            var file = Path.GetRelativePath(gitHubWorkspace, diagnostic.FilePath).Replace(Path.DirectorySeparatorChar, '/');
            var lineProperty = diagnostic.Line is { } l ? $",line={l}" : string.Empty;
            writer.WriteLine($"::{kind} file={EscapeProperty(file)}{lineProperty}::{EscapeData(diagnostic.Message)}");
        }
    }

    public void Error(string message) => writer.WriteLine($"{Paint("error", Tone.Error)}: {message}");

    public void Notice(string message) => writer.WriteLine(Paint(message, Tone.Info));

    private static string EscapeData(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

    private static string EscapeProperty(string value) =>
        EscapeData(value).Replace(":", "%3A").Replace(",", "%2C");
}
